namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Process-wide lock (singleton) that allows a single synchronization at a time. The Actum runs in a single
/// process, so an in-memory lock is enough.
/// </summary>
internal sealed class SincronizacaoEmAndamento : IDisposable
{
    private readonly SemaphoreSlim _semaforo = new(1, 1);

    /// <summary>
    /// Try to take the lock without waiting
    /// </summary>
    /// <returns><see langword="true"/> when taken; <see langword="false"/> when another synchronization holds it</returns>
    public bool TentarIniciar()
    {
        return _semaforo.Wait(0);
    }

    /// <summary>
    /// Release the lock taken by <see cref="TentarIniciar"/>
    /// </summary>
    public void Finalizar()
    {
        _semaforo.Release();
    }

    public void Dispose()
    {
        _semaforo.Dispose();
    }
}
