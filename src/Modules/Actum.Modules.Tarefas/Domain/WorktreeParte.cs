using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Git worktree of one part (<c>front</c> or <c>back</c>) of an implementation branch
/// </summary>
public sealed class WorktreeParte : Entity
{
    public const int ParteMaxLength = 10;

    public const int SituacaoMaxLength = 20;

    public const int ErroMaxLength = 1000;

    /// <summary>
    /// Used by the EF Core when reading
    /// </summary>
    private WorktreeParte(ParteBranch parte, string caminho, SituacaoWorktree situacao)
    {
        Parte = parte;
        Caminho = caminho;
        Situacao = situacao;
    }

    /// <summary>
    /// New part, not created yet (<see cref="SituacaoWorktree.Falhou"/> until the first attempt succeeds)
    /// </summary>
    internal WorktreeParte(ParteBranch parte, string caminho)
        : this(parte, caminho, SituacaoWorktree.Falhou)
    {
    }

    /// <summary>
    /// Worktree the part belongs to
    /// </summary>
    public long WorktreeId { get; private set; }

    /// <summary>
    /// Part of the system
    /// </summary>
    public ParteBranch Parte { get; private set; }

    /// <summary>
    /// Folder of the worktree
    /// </summary>
    public string Caminho { get; private set; }

    /// <summary>
    /// Situation of the worktree of the part
    /// </summary>
    public SituacaoWorktree Situacao { get; private set; }

    /// <summary>
    /// Error of the last attempt, in pt-BR; <see langword="null"/> when it succeeded
    /// </summary>
    public string? UltimoErro { get; private set; }

    /// <summary>
    /// When the worktree was created; <see langword="null"/> until then
    /// </summary>
    public DateTime? CriadoEm { get; private set; }

    /// <summary>
    /// Record that the worktree exists in the clone
    /// </summary>
    /// <param name="agora">Local date and time</param>
    internal void MarcarCriado(DateTime agora)
    {
        Situacao = SituacaoWorktree.Criado;
        CriadoEm = agora;
        UltimoErro = null;
    }

    /// <summary>
    /// Record a failed attempt
    /// </summary>
    /// <param name="erro">Error, in pt-BR (trimmed to <see cref="ErroMaxLength"/>)</param>
    internal void MarcarFalha(string erro)
    {
        Situacao = SituacaoWorktree.Falhou;
        UltimoErro = erro.Length <= ErroMaxLength ? erro : string.Concat(erro.AsSpan(0, ErroMaxLength - 1), "…");
    }

    /// <summary>
    /// Record that the worktree was removed from the clone
    /// </summary>
    internal void MarcarRemovido()
    {
        Situacao = SituacaoWorktree.Removido;
        UltimoErro = null;
    }
}
