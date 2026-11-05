using Actum.Modules.Asana.Contracts;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Tests.Shared;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>task-board</c>, com "hoje" fixo em quarta-feira <c>2026-11-04</c>. As tarefas são
/// montadas pela sincronização (Asana simulado) e pelas ações do ciclo de vida, e as consultas são filtradas pela
/// empresa do cenário, porque o banco de teste é compartilhado.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class PainelTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP", "aremas.com.br")
{
    private static readonly DateOnly Hoje = new(2026, 11, 4);

    private readonly Dictionary<string, AsanaTarefa> _abertas = [];

    private readonly RelogioFixo _relogio = new(new DateTime(2026, 11, 4, 9, 30, 0));

    [Fact]
    public async Task CadaTarefaAbertaFicaEmUmUnicoGrupoNaOrdemDoPainelAsync()
    {
        Abrir("atrasada", new DateOnly(2026, 11, 3));
        Abrir("hoje", Hoje);
        Abrir("semana", new DateOnly(2026, 11, 8));
        Abrir("proxima", new DateOnly(2026, 11, 9));
        Abrir("sem-prazo", null);
        Abrir("reaberta", new DateOnly(2026, 10, 20));
        Abrir("reaberta-bloqueada", new DateOnly(2026, 11, 20));
        Abrir("concluida", new DateOnly(2026, 11, 5));
        Abrir("arquivada", new DateOnly(2026, 11, 5));
        await SincronizarAsync();

        Abrir("entrada", new DateOnly(2026, 11, 6));
        await SincronizarAsync();

        await ConcluirNoActumAsync(Base, "reaberta", "reaberta-bloqueada", "concluida");
        Fechar("arquivada");
        Asana.Tarefas.Remove(Gid("arquivada"));
        await SincronizarAsync();

        Reabrir("reaberta", Base.AddDays(1));
        Reabrir("reaberta-bloqueada", Base.AddDays(2));
        await SincronizarAsync();
        await TarefasAsync(service => service.BloquearAsync(Identifier("reaberta-bloqueada"), Ct));

        var painel = await PainelAsync();

        Assert.Equal(Hoje, painel.Hoje);
        Assert.Equal(
            [
                ("retrabalho", new[] { "reaberta", "reaberta-bloqueada" }),
                ("entrada", ["entrada"]),
                ("atrasadas", ["atrasada"]),
                ("hoje", ["hoje"]),
                ("esta_semana", ["semana"]),
                ("proximas", ["proxima"]),
                ("sem_prazo", ["sem-prazo"]),
                ("concluidas", ["concluida"]),
            ],
            painel.Grupos.Select(grupo => (grupo.Grupo, grupo.Tarefas.Select(Nome).ToArray())));

        var bloqueada = painel.Grupos[0].Tarefas.Single(cartao => Nome(cartao) == "reaberta-bloqueada");
        Assert.True(bloqueada.Bloqueada);
        Assert.Equal(StatusTarefa.Bloqueada, bloqueada.Status);
        Assert.False(painel.Grupos[0].Tarefas.Single(cartao => Nome(cartao) == "reaberta").Bloqueada);
    }

    [Fact]
    public async Task EmpresaSemTarefasDevolveTodosOsGruposVaziosAsync()
    {
        await ConcluirCargaInicialAsync();

        var painel = await PainelAsync(new PainelFiltro { Empresa = (await LerEmpresaAsync(Workspaces[1].Gid)).Identifier });

        Assert.Equal(
            ["retrabalho", "entrada", "atrasadas", "hoje", "esta_semana", "proximas", "sem_prazo", "concluidas"],
            painel.Grupos.Select(grupo => grupo.Grupo));
        Assert.All(painel.Grupos, grupo => Assert.Empty(grupo.Tarefas));
    }

    [Fact]
    public async Task FiltroPorStatusMantemTodosOsGruposSoComAsTarefasDoStatusAsync()
    {
        Abrir("atrasada", new DateOnly(2026, 11, 3));
        Abrir("bloqueada", Hoje);
        Abrir("concluida", new DateOnly(2026, 11, 5));
        await SincronizarAsync();
        Abrir("entrada", new DateOnly(2026, 11, 6));
        await SincronizarAsync();
        await ConcluirNoActumAsync(Base, "concluida");
        await TarefasAsync(service => service.BloquearAsync(Identifier("bloqueada"), Ct));
        var empresa = (await LerEmpresaAsync(Workspace)).Identifier;

        foreach (var (status, esperadas) in new[]
        {
            ("em_andamento", new[] { "atrasada" }),
            ("bloqueada", ["bloqueada"]),
            ("entrada", ["entrada"]),
            ("concluida", ["concluida"]),
            ("retrabalho", []),
        })
        {
            var painel = await PainelAsync(new PainelFiltro { Empresa = empresa, Status = status });

            Assert.Equal(8, painel.Grupos.Count);
            Assert.Equal(esperadas, Todas(painel).Select(Nome));
        }
    }

    [Fact]
    public async Task FiltroPorEmpresaEBuscaSemAcentoAsync()
    {
        _abertas[Gid("ipi")] = TarefaAsana("ipi", Hoje) with { Nome = "Devolução de IPI" };
        _abertas[Gid("outra")] = TarefaAsana("outra", Hoje);
        Abertas(Workspaces[0].Gid, [.. _abertas.Values]);
        Abertas(Workspaces[1].Gid, TarefaAsana("aremas", Hoje));
        await SincronizarAsync();
        var aureum = await LerEmpresaAsync(Workspaces[0].Gid);

        var daAureum = Todas(await PainelAsync(new PainelFiltro { Empresa = aureum.Identifier }));
        Assert.Equal(["ipi", "outra"], daAureum.Select(Nome).Order());
        Assert.All(daAureum, cartao => Assert.Equal(("Aureum-ERP", aureum.Cor), (cartao.Empresa.Nome, cartao.Empresa.Cor)));

        var semFiltro = Todas(await PainelAsync(new PainelFiltro())).Select(cartao => cartao.Identifier).ToHashSet();
        Assert.Contains(Identifier("ipi"), semFiltro);
        Assert.Contains(Identifier("aremas"), semFiltro);

        foreach (var busca in new[] { "devolucao", "DEVOLUÇÃO", " Devol " })
        {
            var encontradas = Todas(await PainelAsync(new PainelFiltro { Empresa = aureum.Identifier, Busca = busca }));
            Assert.Equal(["ipi"], encontradas.Select(Nome));
        }

        var porDescricao = Todas(await PainelAsync(new PainelFiltro { Empresa = aureum.Identifier, Busca = "notas outra" }));
        Assert.Equal(["outra"], porDescricao.Select(Nome));
        Assert.Empty(Todas(await PainelAsync(new PainelFiltro { Empresa = aureum.Identifier, Busca = "100%" })));
    }

    [Fact]
    public async Task ConcluidasPeloActumFicamNoUltimoGrupoDaMaisRecenteParaAMaisAntigaAsync()
    {
        Abrir("antiga", new DateOnly(2026, 11, 1));
        Abrir("recente", new DateOnly(2026, 11, 20));
        Abrir("no-asana", new DateOnly(2026, 11, 5));
        await SincronizarAsync();

        await ConcluirNoActumAsync(Base, "antiga");
        await ConcluirNoActumAsync(Base.AddDays(1), "recente");
        Fechar("no-asana");
        Asana.Tarefas.Remove(Gid("no-asana"));
        await SincronizarAsync();

        var painel = await PainelAsync();

        Assert.Equal("concluidas", painel.Grupos[^1].Grupo);
        Assert.Equal(["recente", "antiga"], Grupo(painel, "concluidas").Select(Nome));
        Assert.Equal(["antiga", "recente"], Todas(painel).Select(Nome).Order());
    }

    [Fact]
    public async Task PrioridadeOrdenaDentroDoGrupoSemMudarDeGrupoAsync()
    {
        var atrasada = new DateOnly(2026, 11, 1);
        Abrir("prioridade-2", atrasada);
        Abrir("prioridade-1", atrasada);
        Abrir("sem-prioridade", atrasada);
        Abrir("mais-atrasada", new DateOnly(2026, 10, 30));
        Abrir("proxima-urgente", new DateOnly(2026, 11, 20));
        await SincronizarAsync();
        await PrioridadeAsync("prioridade-2", 2);
        await PrioridadeAsync("prioridade-1", 1);
        await PrioridadeAsync("mais-atrasada", 3);
        await PrioridadeAsync("proxima-urgente", 1);

        var painel = await PainelAsync();

        Assert.Equal(
            ["mais-atrasada", "prioridade-1", "prioridade-2", "sem-prioridade"],
            Grupo(painel, "atrasadas").Select(Nome));
        Assert.Equal(["proxima-urgente"], Grupo(painel, "proximas").Select(Nome));
    }

    [Fact]
    public async Task CartaoTrazRetrabalhosEComentariosNaoLidosEMarcarLidosZeraAsync()
    {
        Abrir("ipi", new DateOnly(2026, 11, 10));
        await SincronizarAsync();

        await ConcluirNoActumAsync(Base, "ipi");
        Reabrir("ipi", Base.AddDays(1));
        await SincronizarAsync();

        await ConcluirNoActumAsync(Base.AddDays(2), "ipi");
        Reabrir(
            "ipi",
            Base.AddDays(3),
            Comentario("c1", "Maria", "Faltou o IPI", Base.AddDays(3)),
            Comentario("c2", "Maria", "Na devolução também", Base.AddDays(3).AddMinutes(5)),
            Comentario("c3", "João", "Confirmo", Base.AddDays(3).AddMinutes(9)));
        await SincronizarAsync();

        var cartao = Todas(await PainelAsync()).Single();
        Assert.Equal((2, 3), (cartao.QuantidadeRetrabalhos, cartao.ComentariosNaoLidos));
        Assert.Equal(StatusTarefa.Retrabalho, cartao.Status);
        Assert.True(cartao.PrazoInternoPendente);
        Assert.Equal(("Tarefa ipi", "Projeto A, Projeto B", (DateOnly?)new DateOnly(2026, 11, 10), (DateOnly?)new DateOnly(2026, 11, 10)),
            (cartao.Titulo, cartao.Projetos, cartao.PrazoInterno, cartao.PrazoAsana));

        Assert.True(await PainelServiceAsync(service => service.MarcarComentariosLidosAsync(cartao.Identifier, Ct)));

        Assert.Equal(0, Todas(await PainelAsync()).Single().ComentariosNaoLidos);
        Assert.False(await PainelServiceAsync(service => service.MarcarComentariosLidosAsync(Guid.CreateVersion7(), Ct)));
    }

    [Fact]
    public async Task CartaoTrazDataEAutorDoUltimoComentarioLidoOuNaoAsync()
    {
        var anaEm = new DateTime(2026, 11, 3, 9, 0, 0);
        var brunoEm = new DateTime(2026, 11, 4, 14, 30, 0);
        var semAutorEm = new DateTime(2026, 11, 2, 16, 0, 0);
        Abrir("com", Hoje);
        Abrir("sem-autor", Hoje);
        Abrir("sem", Hoje);
        // Carga inicial: os comentários entram como lidos.
        Historico("com", Comentario("bruno", "Bruno", "Pode subir", brunoEm));
        Historico(
            "sem-autor",
            Comentario("conhecido", "Maria", "Primeiro", semAutorEm.AddHours(-1)),
            new AsanaStory(Gid("desconhecido"), AsanaStoryTipo.Comentario, semAutorEm, null, "Sem autor"));
        await SincronizarAsync();

        // Comentário novo (não lido), mas mais antigo que o de Bruno: não muda o último. A tarefa é marcada como
        // modificada para a sincronização ler o histórico de novo.
        _abertas[Gid("com")] = _abertas[Gid("com")] with { ModificadaEm = new DateTime(2026, 11, 4, 15, 0, 0) };
        Publicar();
        Historico("com", Comentario("bruno", "Bruno", "Pode subir", brunoEm), Comentario("ana", "Ana", "Revisei", anaEm));
        await SincronizarAsync();

        var cartoes = Todas(await PainelAsync()).ToDictionary(Nome);
        Assert.Equal(1, cartoes["com"].ComentariosNaoLidos);
        Assert.Equal(((DateTime?)brunoEm, (string?)"Bruno"), (cartoes["com"].UltimoComentarioEm, cartoes["com"].UltimoComentarioAutor));
        Assert.Equal(((DateTime?)semAutorEm, (string?)null), (cartoes["sem-autor"].UltimoComentarioEm, cartoes["sem-autor"].UltimoComentarioAutor));
        Assert.Equal(((DateTime?)null, (string?)null), (cartoes["sem"].UltimoComentarioEm, cartoes["sem"].UltimoComentarioAutor));

        var empresa = (await LerEmpresaAsync(Workspace)).Identifier;
        var listagem = await PainelServiceAsync(service => service.ListarAsync(new ListarTarefasFiltro { Empresa = empresa }, Ct));
        Assert.Equal(
            new (string, DateTime?, string?)[] { ("com", brunoEm, "Bruno"), ("sem", null, null), ("sem-autor", semAutorEm, null) },
            listagem.Items.Select(cartao => (Nome(cartao), cartao.UltimoComentarioEm, cartao.UltimoComentarioAutor)).OrderBy(item => item.Item1));
    }

    [Fact]
    public async Task ListagemFiltraPorStatusEPaginaComOTotalAsync()
    {
        foreach (var nome in new[] { "a", "b", "c", "d" })
        {
            Abrir(nome, Hoje);
        }

        Abrir("x", Hoje);
        Abrir("y", Hoje);
        await SincronizarAsync();
        foreach (var nome in new[] { "x", "y" })
        {
            Fechar(nome);
            Asana.Tarefas.Remove(Gid(nome));
        }

        await SincronizarAsync();
        var empresa = (await LerEmpresaAsync(Workspace)).Identifier;

        var arquivadas = await PainelServiceAsync(service => service.ListarAsync(new ListarTarefasFiltro { Empresa = empresa, Status = "arquivada" }, Ct));
        Assert.Equal(2, arquivadas.TotalCount);
        Assert.Equal(["x", "y"], arquivadas.Items.Select(Nome).Order());
        Assert.All(arquivadas.Items, cartao => Assert.Equal(StatusTarefa.Arquivada, cartao.Status));

        var emAndamento = await PainelServiceAsync(service => service.ListarAsync(new ListarTarefasFiltro { Empresa = empresa, Status = "em_andamento" }, Ct));
        Assert.Equal(4, emAndamento.TotalCount);

        var todas = await PainelServiceAsync(service => service.ListarAsync(new ListarTarefasFiltro { Empresa = empresa }, Ct));
        Assert.Equal((6, 1, ListarTarefasFiltro.TamanhoPadrao), (todas.TotalCount, todas.Page, todas.PageSize));

        var segundaPagina = await PainelServiceAsync(service => service.ListarAsync(new ListarTarefasFiltro { Empresa = empresa, Pagina = 2, Tamanho = 4 }, Ct));
        Assert.Equal((6, 2, 4, 2), (segundaPagina.TotalCount, segundaPagina.Page, segundaPagina.PageSize, segundaPagina.Items.Count));
        Assert.Equal(todas.Items.Skip(4).Select(c => c.Identifier), segundaPagina.Items.Select(c => c.Identifier));
    }

    [Fact]
    public async Task DetalheTrazOsComentariosDeCadaRetrabalhoAsync()
    {
        Abrir("ipi", new DateOnly(2026, 11, 10));
        Historico("ipi", Comentario("antigo", "Maria", "Pode começar", new DateTime(2026, 10, 25, 9, 0, 0)));
        await SincronizarAsync();

        await ConcluirNoActumAsync(new DateTime(2026, 10, 31, 17, 0, 0), "ipi");
        var reabertaEm = new DateTime(2026, 11, 1, 10, 0, 0);
        Reabrir(
            "ipi",
            reabertaEm,
            Comentario("antigo", "Maria", "Pode começar", new DateTime(2026, 10, 25, 9, 0, 0)),
            Comentario("motivo", "Maria", "O IPI da devolução não somou", new DateTime(2026, 11, 1, 9, 50, 0)),
            Comentario("detalhe", "Maria", "Print em anexo", new DateTime(2026, 11, 2, 8, 0, 0)));
        await SincronizarAsync();
        await TarefasAsync(service => service.AdicionarBranchAsync(Identifier("ipi"), new AdicionarBranchRequest(TipoBranch.Base, "main"), Ct));

        var detalhe = await PainelServiceAsync(service => service.ObterDetalheAsync(Identifier("ipi"), Ct));

        Assert.NotNull(detalhe);
        var retrabalho = Assert.Single(detalhe.Retrabalhos);
        Assert.Equal(((short)1, (string?)"Maria", reabertaEm), (retrabalho.Numero, retrabalho.ReabertaPor, retrabalho.ReabertaEm));
        Assert.Equal(["O IPI da devolução não somou", "Print em anexo"], retrabalho.Comentarios.Select(c => c.Texto));
        Assert.Equal(3, detalhe.Comentarios.Count);
        Assert.Equal("Pode começar", detalhe.Comentarios[0].Texto);
        Assert.True(detalhe.Comentarios[0].Lido);
        Assert.Equal([("main", false)], detalhe.Branches.Select(b => (b.Nome, b.Enviada)));
        Assert.Equal(("Tarefa ipi", "Aureum-ERP", StatusTarefa.Retrabalho), (detalhe.Titulo, detalhe.Empresa.Nome, detalhe.Status));

        Assert.Null(await PainelServiceAsync(service => service.ObterDetalheAsync(Guid.CreateVersion7(), Ct)));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Coloca (ou mantém) a tarefa na lista de abertas do primeiro workspace
    /// </summary>
    private void Abrir(string nome, DateOnly? prazo)
    {
        _abertas[Gid(nome)] = TarefaAsana(nome, prazo);
        Publicar();
    }

    /// <summary>
    /// A tarefa sai da lista de abertas e fica concluída no Asana
    /// </summary>
    private void Fechar(string nome)
    {
        if (_abertas.Remove(Gid(nome), out var tarefa))
        {
            Asana.Tarefas[tarefa.Gid] = tarefa with { Concluida = true };
        }

        Publicar();
    }

    /// <summary>
    /// A tarefa volta à lista de abertas, reaberta no Asana por "Maria" em <paramref name="em"/>, com o histórico
    /// acumulado e os comentários informados
    /// </summary>
    private void Reabrir(string nome, DateTime em, params AsanaStory[] comentarios)
    {
        var gid = Gid(nome);
        var tarefa = Asana.Tarefas[gid] with { Concluida = false, ModificadaEm = em };
        _abertas[gid] = tarefa;
        Publicar();

        // Cada reabertura vem depois de uma conclusão, como no Asana.
        var anteriores = Asana.Stories.TryGetValue(gid, out var lista) ? lista.Where(s => s.Tipo is AsanaStoryTipo.MarcadaIncompleta or AsanaStoryTipo.MarcadaConcluida) : [];
        Asana.Stories[gid] =
        [
            .. anteriores,
            Conclusao($"c-{nome}-{em:yyyyMMddHHmm}", "Ricardo", em.AddHours(-1)),
            Reabertura($"r-{nome}-{em:yyyyMMddHHmm}", "Maria", em),
            .. comentarios,
        ];
    }

    private void Publicar()
    {
        Abertas(Workspace, [.. _abertas.Values]);
    }

    /// <summary>
    /// Simula a conclusão pelo Actum (status e data) e a saída da lista de abertas, e sincroniza
    /// </summary>
    private async Task ConcluirNoActumAsync(DateTime concluidaEm, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            await AlterarTarefaAsync(nome, status: StatusTarefa.Concluida, concluidaEm: concluidaEm);
            Fechar(nome);
        }

        await SincronizarAsync();
    }

    private async Task PrioridadeAsync(string nome, short prioridade)
    {
        await TarefasAsync(service => service.AlterarAsync(Identifier(nome), new AlterarTarefaRequest(null, prioridade), Ct));
    }

    private async Task<PainelResponse> PainelAsync(PainelFiltro? filtro = null)
    {
        filtro ??= new PainelFiltro { Empresa = (await LerEmpresaAsync(Workspace)).Identifier };
        return await PainelServiceAsync(service => service.ObterPainelAsync(filtro, Ct));
    }

    private async Task<T> PainelServiceAsync<T>(Func<IPainelService, Task<T>> consulta)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await consulta(new PainelService(contexto, _relogio));
    }

    private async Task<T> TarefasAsync<T>(Func<ITarefaService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await acao(new TarefaService(contexto));
    }

    private Guid Identifier(string nome)
    {
        return LerTarefaAsync(nome).GetAwaiter().GetResult()!.Identifier;
    }

    private string Nome(CartaoTarefaResponse cartao)
    {
        return cartao.Titulo.StartsWith("Tarefa ", StringComparison.Ordinal) ? cartao.Titulo["Tarefa ".Length..] : NomePorTitulo(cartao.Titulo);
    }

    private string NomePorTitulo(string titulo)
    {
        return _abertas.Values.Concat(Asana.Tarefas.Values)
            .First(tarefa => tarefa.Nome == titulo).Gid.Split('-')[0];
    }

    private static List<CartaoTarefaResponse> Todas(PainelResponse painel)
    {
        return painel.Grupos.SelectMany(grupo => grupo.Tarefas).ToList();
    }

    private static IReadOnlyList<CartaoTarefaResponse> Grupo(PainelResponse painel, string grupo)
    {
        return painel.Grupos.Single(g => g.Grupo == grupo).Tarefas;
    }

    /// <summary>
    /// Relógio fixo na data e hora local informadas
    /// </summary>
    private sealed class RelogioFixo(DateTime agoraLocal) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(agoraLocal, TimeZoneInfo.Local.GetUtcOffset(agoraLocal)).ToUniversalTime();
        }
    }
}
