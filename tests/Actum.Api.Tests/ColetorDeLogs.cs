using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Actum.Api.Tests;

/// <summary>
/// <see cref="ILoggerProvider"/> de teste que guarda as mensagens com seus escopos.
/// </summary>
public sealed class ColetorDeLogs : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _escopos = new LoggerExternalScopeProvider();

    public ConcurrentQueue<RegistroDeLog> Registros { get; } = new();

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(this, categoryName);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _escopos = scopeProvider;
    }

    public void Dispose()
    {
    }

    private sealed class Logger(ColetorDeLogs coletor, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return coletor._escopos.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var valores = new Dictionary<string, object?>();
            coletor._escopos.ForEachScope(
                (escopo, acumulado) =>
                {
                    if (escopo is IEnumerable<KeyValuePair<string, object?>> pares)
                    {
                        foreach (var (chave, valor) in pares)
                        {
                            acumulado[chave] = valor;
                        }
                    }
                },
                valores);

            coletor.Registros.Enqueue(new RegistroDeLog(categoria, logLevel, formatter(state, exception), exception, valores));
        }
    }
}

public sealed record RegistroDeLog(
    string Categoria,
    LogLevel Nivel,
    string Mensagem,
    Exception? Excecao,
    IReadOnlyDictionary<string, object?> Escopos);
