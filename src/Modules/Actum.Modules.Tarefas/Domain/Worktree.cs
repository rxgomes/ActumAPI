using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Local worktrees of an implementation branch: one folder per task (<c>&lt;pastaWorktrees&gt;\&lt;branch&gt;</c>) with
/// one git worktree per part (<c>front</c>, <c>back</c>) inside it
/// </summary>
public sealed class Worktree : Entity
{
    public const int CaminhoMaxLength = 260;

    private static readonly char[] CaracteresTrocados = ['/', '\\', '<', '>', ':', '"', '|', '?', '*'];

    private readonly List<WorktreeParte> _partes = [];

    /// <summary>
    /// Used by the EF Core when reading
    /// </summary>
    private Worktree(string pasta)
    {
        Pasta = pasta;
    }

    internal Worktree(string pasta, IEnumerable<ParteBranch> partes)
        : this(pasta)
    {
        foreach (var parte in partes)
        {
            _partes.Add(new WorktreeParte(parte, Path.Combine(pasta, NomeDaPasta(parte))));
        }
    }

    /// <summary>
    /// Branch of the worktrees
    /// </summary>
    public long TarefaBranchId { get; private set; }

    /// <summary>
    /// Folder of the task, with the folders of the parts inside it
    /// </summary>
    public string Pasta { get; private set; }

    /// <summary>
    /// When the worktrees were removed; <see langword="null"/> while active
    /// </summary>
    public DateTime? RemovidoEm { get; private set; }

    /// <summary>
    /// Whether the worktrees were not removed
    /// </summary>
    public bool Ativo => RemovidoEm is null;

    /// <summary>
    /// Parts of the worktree
    /// </summary>
    public IReadOnlyCollection<WorktreeParte> Partes => _partes;

    /// <summary>
    /// Parts in the order front, back
    /// </summary>
    public IReadOnlyList<WorktreeParte> PartesEmOrdem => _partes.OrderBy(parte => parte.Parte).ToList();

    /// <summary>
    /// <see cref="SituacaoWorktree.Removido"/> when removed; otherwise <see cref="SituacaoWorktree.Falhou"/> when a
    /// part is not created; otherwise <see cref="SituacaoWorktree.Criado"/>
    /// </summary>
    public SituacaoWorktree Situacao
    {
        get
        {
            if (!Ativo)
            {
                return SituacaoWorktree.Removido;
            }

            return _partes.All(parte => parte.Situacao == SituacaoWorktree.Criado)
                ? SituacaoWorktree.Criado
                : SituacaoWorktree.Falhou;
        }
    }

    /// <summary>
    /// Activate again a removed worktree, in the same folder; the parts are created again by the next attempt
    /// </summary>
    internal void Reativar()
    {
        RemovidoEm = null;
    }

    /// <summary>
    /// Record that every part was removed
    /// </summary>
    /// <param name="agora">Local date and time</param>
    internal void MarcarRemovido(DateTime agora)
    {
        RemovidoEm = agora;
    }

    /// <summary>
    /// Name of the folder of a branch: <c>/</c> and the characters invalid on Windows become <c>-</c>, and dots and
    /// spaces at the end are removed (e.g. <c>feature/ipi-devolvido</c> → <c>feature-ipi-devolvido</c>)
    /// </summary>
    /// <param name="branch">Branch name</param>
    /// <returns>Folder name</returns>
    public static string PastaDaBranch(string branch)
    {
        var pasta = string.Concat(branch.Select(caractere => CaracteresTrocados.Contains(caractere) ? '-' : caractere));
        return pasta.TrimEnd('.', ' ');
    }

    /// <summary>
    /// Name of the folder of a part inside the folder of the task
    /// </summary>
    public static string NomeDaPasta(ParteBranch parte)
    {
        return parte == ParteBranch.Front ? "front" : "back";
    }
}
