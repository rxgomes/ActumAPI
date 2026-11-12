using System.Collections.Concurrent;

namespace Actum.Modules.Git.Application;

/// <summary>
/// One lock per local clone: commands that change the worktrees of the same clone run one at a time, because the
/// git uses lock files in <c>.git/worktrees</c> and in the index
/// </summary>
internal sealed class TravasDosClones
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _travas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Wait for the lock of the clone; dispose the result to release it
    /// </summary>
    public async Task<IDisposable> AdquirirAsync(string clone, CancellationToken cancellationToken)
    {
        var chave = Path.TrimEndingDirectorySeparator(Path.GetFullPath(clone));
        var trava = _travas.GetOrAdd(chave, _ => new SemaphoreSlim(1, 1));

        await trava.WaitAsync(cancellationToken);

        return new Liberacao(trava);
    }

    private sealed class Liberacao(SemaphoreSlim trava) : IDisposable
    {
        private int _liberada;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _liberada, 1) == 0)
            {
                trava.Release();
            }
        }
    }
}
