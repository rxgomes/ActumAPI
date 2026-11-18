using System.Collections.Concurrent;
using Actum.Modules.Asana.Application;
using Actum.Modules.Claude.Application;
using Actum.Modules.Claude.Contracts;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Tasks with an analysis running (singleton). The Actum runs in a single process, so an in-memory set is enough.
/// </summary>
internal sealed class AnalisesEmAndamento
{
    private readonly ConcurrentDictionary<long, byte> _tarefas = new();

    /// <summary>
    /// Try to start the analysis of the task without waiting
    /// </summary>
    /// <returns><see langword="false"/> when an analysis of the task is already running</returns>
    public bool TentarIniciar(long tarefaId) => _tarefas.TryAdd(tarefaId, 0);

    /// <summary>
    /// Release the task taken by <see cref="TentarIniciar"/>
    /// </summary>
    public void Finalizar(long tarefaId) => _tarefas.TryRemove(tarefaId, out _);
}

/// <summary>
/// <see cref="IAnaliseService"/> over <see cref="TarefasDbContext"/>, the public <see cref="IAnalisadorDeDemanda"/>
/// contract of the Claude module and the public <see cref="IAsanaClient"/> contract of the Asana module (to bring
/// the comments up to date before the analysis)
/// </summary>
internal sealed class AnaliseService(
    TarefasDbContext context,
    IAnalisadorDeDemanda analisador,
    IAsanaClient asana,
    IArquivosDaAnalise arquivos,
    AnalisesEmAndamento emAndamento,
    TimeProvider timeProvider) : IAnaliseService
{
    private const string NenhumaFonte = "Escolha ao menos uma fonte para a análise.";

    private const string PromptVazio = "Escreva o prompt da análise.";

    private const string AnexoRepetido = "O mesmo anexo foi escolhido mais de uma vez.";

    private const string AnexoForaDaTarefa = "O anexo não está mais na tarefa no Asana.";

    private const string TarefaExcluidaNoAsana = "A tarefa não existe mais no Asana.";

    private const string TarefaArquivada = "Tarefas arquivadas não podem ser analisadas.";

    private const string ItemDeVersaoAnterior = "Só os itens da análise atual podem ser marcados.";

    private const string GrupoPadrao = "Geral";

    public async Task<AnaliseResultado> AnalisarAsync(Guid tarefaIdentifier, FontesDaAnaliseRequest? fontes, CancellationToken cancellationToken)
    {
        // Com tracking: os comentários trazidos do Asana são gravados na tarefa.
        var tarefa = await context.Set<Tarefa>()
            .Include(tarefa => tarefa.Comentarios)
            .Include(tarefa => tarefa.Retrabalhos)
            .AsSplitQuery()
            .SingleOrDefaultAsync(tarefa => tarefa.Identifier == tarefaIdentifier, cancellationToken);
        if (tarefa is null)
        {
            return AnaliseResultado.NaoEncontrada;
        }

        if (tarefa.Status == StatusTarefa.Arquivada)
        {
            throw new DomainException(TarefaArquivada);
        }

        // Validação do pedido antes de qualquer chamada ao Asana ou à Anthropic.
        var (pedido, erros) = LerFontes(fontes);
        if (pedido is null)
        {
            return AnaliseResultado.FontesInvalidas(erros!);
        }

        if (!emAndamento.TentarIniciar(tarefa.Id))
        {
            return AnaliseResultado.EmAndamento;
        }

        try
        {
            // Comentários feitos no Asana depois da última sincronização entram como novidade (não lidos), mesmo
            // quando não vão para a IA. Se o Asana falhar, a exceção sobe antes de chamar a IA e nada é gravado.
            var stories = await asana.ListarStoriesAsync(tarefa.AsanaGid, cancellationToken);
            if (tarefa.AdicionarComentarios(stories, lidos: false) > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            var (anexos, errosDosAnexos) = await BaixarAnexosAsync(tarefa.AsanaGid, pedido.Anexos, cancellationToken);
            if (anexos is null)
            {
                return AnaliseResultado.FontesInvalidas(errosDosAnexos!);
            }

            var empresa = await context.Set<Empresa>()
                .Where(empresa => empresa.Id == tarefa.EmpresaId)
                .Select(empresa => empresa.Nome)
                .SingleAsync(cancellationToken);

            var demanda = new DemandaParaAnalise(
                empresa,
                tarefa.Titulo,
                pedido.Descricao ? tarefa.Descricao : null,
                tarefa.Projetos,
                pedido.Comentarios
                    ? tarefa.Comentarios
                        .OrderBy(comentario => comentario.CriadoEm)
                        .ThenBy(comentario => comentario.Id)
                        .Select(comentario => new ComentarioDaDemanda(comentario.Autor, comentario.CriadoEm, comentario.Texto))
                        .ToList()
                    : null,
                pedido.Comentarios
                    ? tarefa.Retrabalhos
                        .OrderBy(retrabalho => retrabalho.Numero)
                        .Select(retrabalho => new RetrabalhoDaDemanda(retrabalho.Numero, retrabalho.ReabertaEm, retrabalho.ReabertaPor))
                        .ToList()
                    : null,
                pedido.Prompt,
                anexos.Select(anexo => new ArquivoDaDemanda(anexo.Nome, anexo.Conteudo)).ToList());

            // Fora de transação: a chamada à IA pode levar dezenas de segundos. Se falhar, nada é gravado.
            var estruturada = await analisador.AnalisarAsync(demanda, cancellationToken);

            var anterior = await AtualAsync(tarefa.Id, cancellationToken);
            var analise = Analise.Criar(
                tarefa.Id,
                anterior,
                estruturada.Objetivo,
                estruturada.Checklist.Select(item => ((string?)item.Grupo, item.Texto, item.Vago, item.Origem, item.Trecho)).ToList(),
                estruturada.Testes,
                estruturada.Duvidas.Select(duvida => (duvida.Texto, duvida.Origem, duvida.Trecho)).ToList(),
                estruturada.Regras.Select(regra => (regra.Texto, regra.Origem)).ToList(),
                estruturada.Modelo,
                estruturada.TokensEntrada,
                estruturada.TokensSaida,
                Agora(),
                pedido with { Anexos = anexos.Select(anexo => (anexo.Gid, anexo.Nome)).ToList() });

            context.Add(analise);
            await context.SaveChangesAsync(cancellationToken);

            return new AnaliseResultado(AnaliseResultadoStatus.Sucesso, ParaResposta(analise, true));
        }
        finally
        {
            emAndamento.Finalizar(tarefa.Id);
        }
    }

    /// <summary>
    /// Read the requested sources with their defaults (no body: description and comments), or the messages per
    /// field when they are invalid. In the prompt mode the description is never sent.
    /// </summary>
    private static (FontesDaAnalise? Fontes, Dictionary<string, string[]>? Erros) LerFontes(FontesDaAnaliseRequest? fontes)
    {
        var modo = fontes?.Modo ?? ModoDaAnalise.Tarefa;
        var descricao = modo == ModoDaAnalise.Tarefa && (fontes?.Descricao ?? true);
        var comentarios = fontes?.Comentarios ?? modo == ModoDaAnalise.Tarefa;
        var anexos = (fontes?.Anexos ?? [])
            .Where(gid => !string.IsNullOrWhiteSpace(gid))
            .Select(gid => gid.Trim())
            .ToList();
        var prompt = modo == ModoDaAnalise.Prompt && !string.IsNullOrWhiteSpace(fontes?.Prompt) ? fontes.Prompt.Trim() : null;

        var erros = new Dictionary<string, string[]>();
        if (modo == ModoDaAnalise.Prompt && prompt is null)
        {
            erros["Prompt"] = [PromptVazio];
        }
        else if (prompt is { Length: > Analise.PromptMaxLength })
        {
            erros["Prompt"] = [$"O prompt deve ter no máximo {Analise.PromptMaxLength} caracteres."];
        }

        if (anexos.Distinct(StringComparer.Ordinal).Count() != anexos.Count)
        {
            erros["Anexos"] = [AnexoRepetido];
        }

        if (modo == ModoDaAnalise.Tarefa && !descricao && !comentarios && anexos.Count == 0)
        {
            erros["Fontes"] = [NenhumaFonte];
        }

        if (erros.Count > 0)
        {
            return (null, erros);
        }

        return (new FontesDaAnalise(modo, descricao, comentarios, prompt, anexos.Select(gid => (gid, string.Empty)).ToList()), null);
    }

    /// <summary>
    /// Check that every requested attachment is in the task and readable, within the total limit, and download
    /// them now from the Asana, in order; or the messages of the <c>Anexos</c> field
    /// </summary>
    private async Task<(List<(string Gid, string Nome, byte[] Conteudo)>? Anexos, Dictionary<string, string[]>? Erros)> BaixarAnexosAsync(
        string tarefaGid,
        IReadOnlyList<(string Gid, string Nome)> pedidos,
        CancellationToken cancellationToken)
    {
        if (pedidos.Count == 0)
        {
            return ([], null);
        }

        var daTarefa = await asana.ListarAnexosAsync(tarefaGid, cancellationToken)
            ?? throw new DomainException(TarefaExcluidaNoAsana);
        var porGid = daTarefa.ToDictionary(anexo => anexo.Gid, StringComparer.Ordinal);

        var mensagens = new List<string>();
        long total = 0;
        foreach (var (gid, _) in pedidos)
        {
            if (!porGid.TryGetValue(gid, out var anexo))
            {
                mensagens.Add(AnexoForaDaTarefa);
                continue;
            }

            var legibilidade = arquivos.Avaliar(anexo.Nome, anexo.Tamanho, anexo.Hospedagem);
            if (!legibilidade.Legivel)
            {
                mensagens.Add($"{anexo.Nome}: {legibilidade.Motivo}");
            }

            total += anexo.Tamanho ?? 0;
        }

        if (mensagens.Count == 0 && total > arquivos.TamanhoTotalMaximo)
        {
            mensagens.Add(arquivos.MensagemDoTotalMaximo);
        }

        if (mensagens.Count > 0)
        {
            return (null, new Dictionary<string, string[]> { ["Anexos"] = [.. mensagens.Distinct()] });
        }

        // Em sequência: o Asana limita as requisições por minuto.
        var baixados = new List<(string Gid, string Nome, byte[] Conteudo)>();
        total = 0;
        foreach (var (gid, _) in pedidos)
        {
            var conteudo = await asana.BaixarAnexoAsync(gid, arquivos.TamanhoMaximo(porGid[gid].Nome), cancellationToken);
            if (conteudo is null || conteudo.TarefaGid != tarefaGid)
            {
                return (null, new Dictionary<string, string[]> { ["Anexos"] = [AnexoForaDaTarefa] });
            }

            total += conteudo.Conteudo.LongLength;
            if (total > arquivos.TamanhoTotalMaximo)
            {
                return (null, new Dictionary<string, string[]> { ["Anexos"] = [arquivos.MensagemDoTotalMaximo] });
            }

            var nome = string.IsNullOrEmpty(conteudo.Nome) ? porGid[gid].Nome : conteudo.Nome;
            baixados.Add((gid, nome, conteudo.Conteudo));
        }

        return (baixados, null);
    }

    public async Task<IReadOnlyList<AnaliseResponse>?> ListarAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        var tarefaId = await TarefaIdAsync(tarefaIdentifier, cancellationToken);
        if (tarefaId is null)
        {
            return null;
        }

        var analises = await context.Set<Analise>()
            .AsNoTracking()
            .Include(analise => analise.Itens)
            .Where(analise => analise.TarefaId == tarefaId)
            .OrderByDescending(analise => analise.Versao)
            .ToListAsync(cancellationToken);

        return analises.Select((analise, indice) => ParaResposta(analise, indice == 0)).ToList();
    }

    public async Task<AnaliseResultado> MarcarItemAsync(Guid tarefaIdentifier, Guid itemIdentifier, bool concluido, CancellationToken cancellationToken)
    {
        var tarefaId = await TarefaIdAsync(tarefaIdentifier, cancellationToken);
        if (tarefaId is null)
        {
            return AnaliseResultado.NaoEncontrada;
        }

        var analise = await context.Set<Analise>()
            .Include(analise => analise.Itens)
            .Where(analise => analise.TarefaId == tarefaId && analise.Itens.Any(item => item.Identifier == itemIdentifier))
            .SingleOrDefaultAsync(cancellationToken);
        if (analise is null)
        {
            return AnaliseResultado.NaoEncontrada;
        }

        var ultimaVersao = await context.Set<Analise>()
            .Where(outra => outra.TarefaId == tarefaId)
            .MaxAsync(outra => outra.Versao, cancellationToken);
        if (analise.Versao != ultimaVersao)
        {
            throw new DomainException(ItemDeVersaoAnterior);
        }

        var item = analise.Itens.Single(item => item.Identifier == itemIdentifier);
        item.Marcar(concluido, Agora());
        await context.SaveChangesAsync(cancellationToken);

        return new AnaliseResultado(AnaliseResultadoStatus.Sucesso, Item: ParaResposta(item));
    }

    private Task<Analise?> AtualAsync(long tarefaId, CancellationToken cancellationToken)
    {
        return context.Set<Analise>()
            .AsNoTracking()
            .Include(analise => analise.Itens)
            .Where(analise => analise.TarefaId == tarefaId)
            .OrderByDescending(analise => analise.Versao)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<long?> TarefaIdAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        return await context.Set<Tarefa>()
            .Where(tarefa => tarefa.Identifier == tarefaIdentifier)
            .Select(tarefa => (long?)tarefa.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private DateTime Agora() => timeProvider.GetLocalNow().DateTime;

    private static AnaliseResponse ParaResposta(Analise analise, bool atual)
    {
        List<AnaliseItemResponse> Itens(TipoItemAnalise tipo) => analise.Itens
            .Where(item => item.Tipo == tipo)
            .OrderBy(item => item.Ordem)
            .Select(ParaResposta)
            .ToList();

        // GroupBy mantém a ordem da primeira ocorrência de cada grupo; análises antigas, sem grupo, ficam em "Geral".
        var checklist = Itens(TipoItemAnalise.Checklist)
            .GroupBy(item => item.Grupo ?? GrupoPadrao)
            .Select(grupo => new GrupoDoChecklistResponse(grupo.Key, grupo.ToList()))
            .ToList();

        return new AnaliseResponse(
            analise.Identifier,
            analise.Versao,
            atual,
            analise.Objetivo,
            checklist,
            Itens(TipoItemAnalise.Teste),
            Itens(TipoItemAnalise.Duvida),
            Itens(TipoItemAnalise.Regra),
            analise.Modelo,
            analise.TokensEntrada,
            analise.TokensSaida,
            analise.AnalisadaEm,
            analise.Fontes is { } fontes
                ? new FontesDaAnaliseResponse(
                    fontes.Modo,
                    fontes.Descricao,
                    fontes.Comentarios,
                    fontes.Anexos.Select(anexo => new AnexoDaFonteResponse(anexo.Gid, anexo.Nome)).ToList(),
                    fontes.Prompt)
                : null);
    }

    private static AnaliseItemResponse ParaResposta(AnaliseItem item)
    {
        return new AnaliseItemResponse(
            item.Identifier,
            item.Tipo,
            item.Ordem,
            item.Texto,
            item.Grupo,
            item.Vago,
            item.ConcluidoEm is not null,
            item.ConcluidoEm,
            item.Origem,
            item.Trecho);
    }
}
