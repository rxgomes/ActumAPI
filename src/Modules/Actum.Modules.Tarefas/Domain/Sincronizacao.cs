using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Record of one synchronization run, with its totals and outcome
/// </summary>
public sealed class Sincronizacao : Entity
{
    /// <summary>
    /// Start a synchronization record
    /// </summary>
    /// <param name="iniciadaEm">Local date and time of the start</param>
    public Sincronizacao(DateTime iniciadaEm)
    {
        IniciadaEm = iniciadaEm;
    }

    /// <summary>
    /// Local date and time of the start
    /// </summary>
    public DateTime IniciadaEm { get; private set; }

    /// <summary>
    /// Local date and time of the end; <see langword="null"/> while running
    /// </summary>
    public DateTime? FinalizadaEm { get; private set; }

    /// <summary>
    /// Whether every company was synchronized
    /// </summary>
    public bool Sucesso { get; private set; }

    /// <summary>
    /// Tasks created
    /// </summary>
    public int TarefasNovas { get; set; }

    /// <summary>
    /// Existing tasks changed in the Asana since the previous synchronization, or brought back from the archive
    /// </summary>
    public int TarefasAtualizadas { get; set; }

    /// <summary>
    /// Tasks archived
    /// </summary>
    public int TarefasArquivadas { get; set; }

    /// <summary>
    /// Reworks detected
    /// </summary>
    public int Reaberturas { get; set; }

    /// <summary>
    /// Failure message (pt-BR) when <see cref="Sucesso"/> is <see langword="false"/>
    /// </summary>
    public string? Erro { get; private set; }

    /// <summary>
    /// Finish the run successfully
    /// </summary>
    /// <param name="agora">Local date and time</param>
    public void Concluir(DateTime agora)
    {
        FinalizadaEm = agora;
        Sucesso = true;
        Erro = null;
    }

    /// <summary>
    /// Finish the run with a failure
    /// </summary>
    /// <param name="agora">Local date and time</param>
    /// <param name="erro">Failure message, in pt-BR</param>
    public void Falhar(DateTime agora, string erro)
    {
        FinalizadaEm = agora;
        Sucesso = false;
        Erro = erro;
    }
}
