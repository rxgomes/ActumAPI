using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Board, list and detail queries over <see cref="TarefasDbContext"/> (design of <c>painel-de-tarefas</c>).
/// The open tasks are few (tens to a few hundred), so the board is read in a single query and grouped in memory.
/// </summary>
internal sealed class PainelService(TarefasDbContext context, TimeProvider timeProvider) : IPainelService
{
    public const string GrupoRetrabalho = "retrabalho";

    public const string GrupoEntrada = "entrada";

    public const string GrupoAtrasadas = "atrasadas";

    public const string GrupoHoje = "hoje";

    public const string GrupoEstaSemana = "esta_semana";

    public const string GrupoProximas = "proximas";

    public const string GrupoSemPrazo = "sem_prazo";

    public const string GrupoConcluidas = "concluidas";

    private const char EscapeDoLike = '\\';

    /// <summary>
    /// Null priority is ordered after 3
    /// </summary>
    private const short PrioridadeAusente = 4;

    private static readonly string[] OrdemDosGrupos =
        [GrupoRetrabalho, GrupoEntrada, GrupoAtrasadas, GrupoHoje, GrupoEstaSemana, GrupoProximas, GrupoSemPrazo, GrupoConcluidas];

    public async Task<PainelResponse> ObterPainelAsync(PainelFiltro filtro, CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var domingo = hoje.AddDays((7 - (int)hoje.DayOfWeek) % 7);

        // Concluídas pelo Actum vão para o último grupo; as arquivadas (inclusive as concluídas direto no Asana) ficam de fora.
        var doPainel = context.Set<Tarefa>()
            .Where(tarefa => tarefa.Status != StatusTarefa.Arquivada);
        if (filtro.Status is { } texto)
        {
            var status = EnumSnakeCaseConverter<StatusTarefa>.ParaEnum(texto);
            doPainel = doPainel.Where(tarefa => tarefa.Status == status);
        }

        var linhas = await LerCartoes(Filtrar(doPainel, filtro.Busca), filtro.Empresa).ToListAsync(cancellationToken);

        var porGrupo = linhas.ToLookup(linha => Grupo(linha, hoje, domingo));
        var grupos = OrdemDosGrupos
            .Select(grupo => new GrupoPainelResponse(grupo, Ordenar(grupo, porGrupo[grupo]).Select(ParaCartao).ToList()))
            .ToList();

        return new PainelResponse(hoje, grupos);
    }

    public async Task<PagedResult<CartaoTarefaResponse>> ListarAsync(ListarTarefasFiltro filtro, CancellationToken cancellationToken)
    {
        var pagina = filtro.Pagina ?? 1;
        var tamanho = filtro.Tamanho ?? ListarTarefasFiltro.TamanhoPadrao;

        var tarefas = Filtrar(context.Set<Tarefa>(), filtro.Busca);
        if (filtro.Status is { } texto)
        {
            var status = EnumSnakeCaseConverter<StatusTarefa>.ParaEnum(texto);
            tarefas = tarefas.Where(tarefa => tarefa.Status == status);
        }

        var cartoes = LerCartoes(tarefas, filtro.Empresa);
        var total = await cartoes.CountAsync(cancellationToken);
        var linhas = await cartoes
            .OrderByDescending(linha => linha.CreateIn)
            .ThenByDescending(linha => linha.Id)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(cancellationToken);

        return new PagedResult<CartaoTarefaResponse>(linhas.Select(ParaCartao).ToList(), pagina, tamanho, total);
    }

    public async Task<TarefaDetalheResponse?> ObterDetalheAsync(Guid identifier, CancellationToken cancellationToken)
    {
        var tarefa = await context.Set<Tarefa>()
            .AsNoTracking()
            .Include(t => t.Branches)
            .Include(t => t.Retrabalhos)
            .Include(t => t.Comentarios)
            .AsSplitQuery()
            .SingleOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);
        if (tarefa is null)
        {
            return null;
        }

        var empresa = await context.Set<Empresa>()
            .AsNoTracking()
            .SingleAsync(e => e.Id == tarefa.EmpresaId, cancellationToken);

        var comentarios = tarefa.Comentarios
            .OrderBy(comentario => comentario.CriadoEm)
            .Select(ParaComentario)
            .ToList();

        var branches = tarefa.Branches
            .OrderBy(branch => branch.Tipo)
            .ThenBy(branch => branch.Id)
            .Select(branch => TarefaCiclo.ParaResponse(tarefa, branch))
            .ToList();

        return new TarefaDetalheResponse(
            tarefa.Identifier,
            tarefa.Titulo,
            tarefa.Descricao,
            tarefa.DescricaoHtml,
            tarefa.Projetos,
            tarefa.LinkAsana,
            new EmpresaResumoResponse(empresa.Identifier, empresa.Nome, empresa.Cor),
            tarefa.Status,
            tarefa.StatusAnterior,
            tarefa.EmAndamento,
            tarefa.PrazoInterno,
            tarefa.PrazoInternoPendente,
            tarefa.PrazoAsana,
            tarefa.Prioridade,
            tarefa.ConcluidaEm,
            tarefa.ArquivadaEm,
            tarefa.MotivoArquivamento,
            tarefa.SincronizadaEm,
            branches,
            Retrabalhos(tarefa.Retrabalhos, comentarios),
            comentarios);
    }

    public async Task<bool> MarcarComentariosLidosAsync(Guid identifier, CancellationToken cancellationToken)
    {
        var tarefa = await context.Set<Tarefa>()
            .Include(t => t.Comentarios.Where(comentario => !comentario.Lido))
            .SingleOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);
        if (tarefa is null)
        {
            return false;
        }

        foreach (var comentario in tarefa.Comentarios)
        {
            comentario.Lido = true;
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Text filter on title and description, ignoring case (<c>ILIKE</c>) and accents (<c>unaccent</c>)
    /// </summary>
    private static IQueryable<Tarefa> Filtrar(IQueryable<Tarefa> tarefas, string? busca)
    {
        if (string.IsNullOrWhiteSpace(busca))
        {
            return tarefas;
        }

        var padrao = $"%{EscaparLike(busca.Trim())}%";
        var escape = EscapeDoLike.ToString();

        return tarefas.Where(tarefa =>
            EF.Functions.ILike(EF.Functions.Unaccent(tarefa.Titulo), EF.Functions.Unaccent(padrao), escape)
            || EF.Functions.ILike(EF.Functions.Unaccent(tarefa.Descricao), EF.Functions.Unaccent(padrao), escape));
    }

    private IQueryable<LinhaDoCartao> LerCartoes(IQueryable<Tarefa> tarefas, Guid? empresa)
    {
        var empresas = context.Set<Empresa>().AsQueryable();
        if (empresa is { } empresaIdentifier)
        {
            empresas = empresas.Where(e => e.Identifier == empresaIdentifier);
        }

        return from tarefa in tarefas.AsNoTracking()
               join e in empresas on tarefa.EmpresaId equals e.Id
               // Inicializador de objeto (não construtor): o EF consegue ordenar e paginar sobre a projeção.
               select new LinhaDoCartao
               {
                   Id = tarefa.Id,
                   Identifier = tarefa.Identifier,
                   Titulo = tarefa.Titulo,
                   EmpresaIdentifier = e.Identifier,
                   EmpresaNome = e.Nome,
                   EmpresaCor = e.Cor,
                   Projetos = tarefa.Projetos,
                   Status = tarefa.Status,
                   StatusAnterior = tarefa.StatusAnterior,
                   EmAndamento = tarefa.EmAndamento,
                   PrazoInterno = tarefa.PrazoInterno,
                   PrazoAsana = tarefa.PrazoAsana,
                   PrazoInternoPendente = tarefa.PrazoInternoPendente,
                   Prioridade = tarefa.Prioridade,
                   QuantidadeRetrabalhos = tarefa.Retrabalhos.Count(),
                   ComentariosNaoLidos = tarefa.Comentarios.Count(comentario => !comentario.Lido),
                   UltimaReaberturaEm = tarefa.Retrabalhos.Max(retrabalho => (DateTime?)retrabalho.ReabertaEm),
                   // Mais recente pela data; o id desempata comentários com o mesmo horário.
                   UltimoComentarioEm = tarefa.Comentarios
                       .OrderByDescending(comentario => comentario.CriadoEm)
                       .ThenByDescending(comentario => comentario.Id)
                       .Select(comentario => (DateTime?)comentario.CriadoEm)
                       .FirstOrDefault(),
                   UltimoComentarioAutor = tarefa.Comentarios
                       .OrderByDescending(comentario => comentario.CriadoEm)
                       .ThenByDescending(comentario => comentario.Id)
                       .Select(comentario => comentario.Autor)
                       .FirstOrDefault(),
                   ConcluidaEm = tarefa.ConcluidaEm,
                   CreateIn = tarefa.CreateIn,
               };
    }

    /// <summary>
    /// The first group whose rule the task meets (section 1 of the design)
    /// </summary>
    private static string Grupo(LinhaDoCartao linha, DateOnly hoje, DateOnly domingo)
    {
        if (linha.Status == StatusTarefa.Concluida)
        {
            return GrupoConcluidas;
        }

        if (EmRetrabalho(linha))
        {
            return GrupoRetrabalho;
        }

        if (linha.Status == StatusTarefa.Entrada)
        {
            return GrupoEntrada;
        }

        return linha.PrazoInterno switch
        {
            null => GrupoSemPrazo,
            { } prazo when prazo < hoje => GrupoAtrasadas,
            { } prazo when prazo == hoje => GrupoHoje,
            { } prazo when prazo <= domingo => GrupoEstaSemana,
            _ => GrupoProximas,
        };
    }

    private static IEnumerable<LinhaDoCartao> Ordenar(string grupo, IEnumerable<LinhaDoCartao> linhas)
    {
        var ordenadas = grupo switch
        {
            // Prazo pendente primeiro, depois a reabertura mais antiga.
            GrupoRetrabalho => linhas
                .OrderByDescending(linha => linha.PrazoInternoPendente)
                .ThenBy(linha => linha.UltimaReaberturaEm ?? DateTime.MaxValue),
            GrupoEntrada => linhas
                .OrderBy(linha => linha.PrazoAsana ?? DateOnly.MaxValue)
                .ThenBy(linha => linha.CreateIn),
            GrupoHoje => linhas
                .OrderBy(linha => linha.Prioridade ?? PrioridadeAusente),
            GrupoSemPrazo => linhas
                .OrderBy(linha => linha.CreateIn),
            // Concluída mais recentemente primeiro.
            GrupoConcluidas => linhas
                .OrderByDescending(linha => linha.ConcluidaEm ?? DateTime.MinValue),
            _ => linhas
                .OrderBy(linha => linha.PrazoInterno)
                .ThenBy(linha => linha.Prioridade ?? PrioridadeAusente),
        };

        return ordenadas.ThenBy(linha => linha.CreateIn).ThenBy(linha => linha.Id);
    }

    /// <summary>
    /// Each rework gets the comments made from the day of its reopening until the day before the next reopening
    /// (or until now). Day-level windows keep the comment written just before or after the reopening, which
    /// usually explains the reason.
    /// </summary>
    private static List<RetrabalhoResponse> Retrabalhos(IEnumerable<Retrabalho> retrabalhos, IReadOnlyList<ComentarioResponse> comentarios)
    {
        var ordenados = retrabalhos.OrderBy(retrabalho => retrabalho.Numero).ToList();

        return ordenados
            .Select((retrabalho, indice) =>
            {
                var inicio = retrabalho.ReabertaEm.Date;
                var fim = indice + 1 < ordenados.Count ? ordenados[indice + 1].ReabertaEm.Date : DateTime.MaxValue;
                var daJanela = comentarios.Where(comentario => comentario.CriadoEm >= inicio && comentario.CriadoEm < fim).ToList();

                return new RetrabalhoResponse(
                    retrabalho.Identifier,
                    retrabalho.Numero,
                    retrabalho.ReabertaEm,
                    retrabalho.ReabertaPor,
                    retrabalho.PrazoInternoAnterior,
                    retrabalho.PrazoInternoNovo,
                    retrabalho.ResolvidoEm,
                    daJanela,
                    retrabalho.ConcluidaEm);
            })
            .ToList();
    }

    private static bool EmRetrabalho(LinhaDoCartao linha)
    {
        return linha.Status == StatusTarefa.Retrabalho
            || (linha.Status == StatusTarefa.Bloqueada && linha.StatusAnterior == StatusTarefa.Retrabalho);
    }

    private static CartaoTarefaResponse ParaCartao(LinhaDoCartao linha)
    {
        return new CartaoTarefaResponse(
            linha.Identifier,
            linha.Titulo,
            new EmpresaResumoResponse(linha.EmpresaIdentifier, linha.EmpresaNome, linha.EmpresaCor),
            linha.Projetos,
            linha.Status,
            linha.PrazoInterno,
            linha.PrazoAsana,
            linha.PrazoInternoPendente,
            linha.Prioridade,
            linha.QuantidadeRetrabalhos,
            linha.ComentariosNaoLidos,
            linha.Status == StatusTarefa.Bloqueada,
            linha.EmAndamento,
            linha.UltimoComentarioEm,
            linha.UltimoComentarioAutor);
    }

    private static ComentarioResponse ParaComentario(Comentario comentario)
    {
        return new ComentarioResponse(comentario.Identifier, comentario.Autor, comentario.Texto, comentario.CriadoEm, comentario.Lido);
    }

    private static string EscaparLike(string texto)
    {
        return texto
            .Replace(EscapeDoLike.ToString(), $"{EscapeDoLike}{EscapeDoLike}", StringComparison.Ordinal)
            .Replace("%", $"{EscapeDoLike}%", StringComparison.Ordinal)
            .Replace("_", $"{EscapeDoLike}_", StringComparison.Ordinal);
    }

    private sealed class LinhaDoCartao
    {
        public long Id { get; init; }

        public Guid Identifier { get; init; }

        public required string Titulo { get; init; }

        public Guid EmpresaIdentifier { get; init; }

        public required string EmpresaNome { get; init; }

        public required string EmpresaCor { get; init; }

        public required string Projetos { get; init; }

        public StatusTarefa Status { get; init; }

        public StatusTarefa? StatusAnterior { get; init; }

        public bool EmAndamento { get; init; }

        public DateOnly? PrazoInterno { get; init; }

        public DateOnly? PrazoAsana { get; init; }

        public bool PrazoInternoPendente { get; init; }

        public short? Prioridade { get; init; }

        public int QuantidadeRetrabalhos { get; init; }

        public int ComentariosNaoLidos { get; init; }

        public DateTime? UltimaReaberturaEm { get; init; }

        public DateTime? UltimoComentarioEm { get; init; }

        public string? UltimoComentarioAutor { get; init; }

        public DateTime? ConcluidaEm { get; init; }

        public DateTime CreateIn { get; init; }
    }
}
