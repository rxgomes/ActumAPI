using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Actum.Shared.Infrastructure.Processos;

/// <summary>
/// <see cref="IExecutorDeProcessos"/> over <see cref="Process"/>
/// </summary>
public sealed class ExecutorDeProcessos : IExecutorDeProcessos
{
    /// <summary>
    /// Environment variables of the Actum process that are never passed to a child: a <c>dotnet run</c> of another
    /// project would otherwise listen on the port of the Actum
    /// </summary>
    public static readonly IReadOnlyList<string> VariaveisRemovidas =
        ["ASPNETCORE_URLS", "ASPNETCORE_ENVIRONMENT", "ASPNETCORE_HTTP_PORTS", "ASPNETCORE_HTTPS_PORTS"];

    /// <summary>
    /// Prefixes of environment variables of the Actum process that are never passed to a child
    /// </summary>
    public static readonly IReadOnlyList<string> PrefixosRemovidos = ["Cors__"];

    /// <inheritdoc />
    public async Task<ResultadoDoProcesso> ExecutarAsync(ComandoExterno comando, CancellationToken cancellationToken)
    {
        using var processo = new Process { StartInfo = Configurar(comando) };

        try
        {
            processo.Start();
        }
        catch (Win32Exception exception)
        {
            throw new ExecutavelNaoEncontradoException(comando, exception);
        }

        // Nada é escrito na entrada: fechar evita que um programa fique esperando o teclado.
        processo.StandardInput.Close();

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(comando.TempoLimite);

        // As duas saídas são lidas ao mesmo tempo: ler uma até o fim antes da outra trava quando o buffer enche.
        var saida = processo.StandardOutput.ReadToEndAsync(limite.Token);
        var erro = processo.StandardError.ReadToEndAsync(limite.Token);

        try
        {
            await Task.WhenAll(saida, erro);
            await processo.WaitForExitAsync(limite.Token);
        }
        catch (OperationCanceledException)
        {
            Encerrar(processo);

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TempoEsgotadoException(comando);
        }

        return new ResultadoDoProcesso(processo.ExitCode, await saida, await erro);
    }

    private static ProcessStartInfo Configurar(ComandoExterno comando)
    {
        var info = new ProcessStartInfo(comando.Executavel)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argumento in comando.Argumentos)
        {
            info.ArgumentList.Add(argumento);
        }

        if (comando.PastaDeTrabalho is not null)
        {
            info.WorkingDirectory = comando.PastaDeTrabalho;
        }

        var removidas = info.Environment.Keys
            .Where(nome => VariaveisRemovidas.Contains(nome, StringComparer.OrdinalIgnoreCase)
                || PrefixosRemovidos.Any(prefixo => nome.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        foreach (var nome in removidas)
        {
            info.Environment.Remove(nome);
        }

        foreach (var (nome, valor) in comando.Variaveis ?? new Dictionary<string, string>())
        {
            info.Environment[nome] = valor;
        }

        return info;
    }

    private static void Encerrar(Process processo)
    {
        try
        {
            if (!processo.HasExited)
            {
                processo.Kill(entireProcessTree: true);
                processo.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }
        catch (InvalidOperationException)
        {
            // O processo terminou entre a verificação e o Kill.
        }
        catch (Win32Exception)
        {
            // Sem permissão para encerrar algum filho; o principal já foi sinalizado.
        }
    }
}
