using Actum.Modules.Claude.Application;
using Actum.Modules.Claude.Contracts;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// <see cref="IAnalisadorDeDemanda"/> de teste: registra as demandas recebidas e devolve o resultado (ou a falha)
/// configurado, sem chamar a Anthropic. <see cref="Bloqueio"/> segura a resposta para testar concorrência.
/// </summary>
public sealed class AnalisadorDeDemandaFalso : IAnalisadorDeDemanda
{
    private readonly List<DemandaParaAnalise> _demandas = [];

    /// <summary>
    /// Demandas recebidas, na ordem
    /// </summary>
    public IReadOnlyList<DemandaParaAnalise> Demandas => _demandas;

    /// <summary>
    /// Resultado devolvido pela próxima análise
    /// </summary>
    public DemandaEstruturada Resultado { get; set; } = Estruturada("Objetivo", ["Item 1"], ["Dúvida 1"]);

    /// <summary>
    /// Falha lançada em vez do resultado; <see langword="null"/> para responder normalmente
    /// </summary>
    public Exception? Falha { get; set; }

    /// <summary>
    /// Quando definido, a análise só responde depois que a tarefa terminar
    /// </summary>
    public TaskCompletionSource? Bloqueio { get; set; }

    /// <summary>
    /// Avisado quando uma análise começa
    /// </summary>
    public TaskCompletionSource Iniciada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static DemandaEstruturada Estruturada(string objetivo, string[] checklist, string[] duvidas) =>
        new(objetivo, checklist.Select(texto => new ItemDoChecklist("Geral", texto, null)).ToList(), [], duvidas, "claude-opus-5-5", 1000, 200);

    public async Task<DemandaEstruturada> AnalisarAsync(DemandaParaAnalise demanda, CancellationToken cancellationToken)
    {
        lock (_demandas)
        {
            _demandas.Add(demanda);
        }

        Iniciada.TrySetResult();

        if (Bloqueio is { } bloqueio)
        {
            await bloqueio.Task.WaitAsync(cancellationToken);
        }

        if (Falha is { } falha)
        {
            throw falha;
        }

        return Resultado;
    }
}
