using System.Diagnostics;
using Actum.Shared.Infrastructure.Processos;

namespace Actum.Shared.Infrastructure.Tests.Processos;

/// <summary>
/// Execução de processos externos, usando o Windows PowerShell como processo filho
/// </summary>
public sealed class ExecutorDeProcessosTests
{
    private const string PowerShell = "powershell.exe";

    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(30);

    private readonly ExecutorDeProcessos _executor = new();

    [Fact]
    public async Task CapturaCodigoDeSaidaESaidasAsync()
    {
        var resultado = await _executor.ExecutarAsync(
            // O PowerShell escreve na página de código do console; o git (o uso real) escreve em UTF-8.
            Comando("[Console]::OutputEncoding = [Text.Encoding]::UTF8; [Console]::Out.Write('saída'); [Console]::Error.Write('falha'); exit 3"),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, resultado.CodigoDeSaida);
        Assert.False(resultado.Sucesso);
        Assert.Equal("saída", resultado.Saida.Trim());
        Assert.Equal("falha", resultado.Erro.Trim());
    }

    [Fact]
    public async Task ArgumentoComEspacoEAspasChegaIntactoAsync()
    {
        var script = Path.Combine(Path.GetTempPath(), $"actum-args-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(script, "foreach ($a in $args) { [Console]::Out.WriteLine(\"[$a]\") }", TestContext.Current.CancellationToken);

        try
        {
            var resultado = await _executor.ExecutarAsync(
                new ComandoExterno(
                    PowerShell,
                    ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, @"E:\wt\Minha Empresa\feature-x", "a'b"],
                    Limite),
                TestContext.Current.CancellationToken);

            Assert.True(resultado.Sucesso, resultado.Erro);
            var linhas = resultado.Saida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Equal([@"[E:\wt\Minha Empresa\feature-x]", "[a'b]"], linhas);
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public async Task TempoEsgotadoEncerraOProcessoAsync()
    {
        var cronometro = Stopwatch.StartNew();

        var excecao = await Assert.ThrowsAsync<TempoEsgotadoException>(() => _executor.ExecutarAsync(
            Comando("Start-Sleep -Seconds 60", TimeSpan.FromSeconds(2)),
            TestContext.Current.CancellationToken));

        Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(20), $"Demorou {cronometro.Elapsed}");
        Assert.Contains("2 segundos", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecutavelInexistenteAsync()
    {
        await Assert.ThrowsAsync<ExecutavelNaoEncontradoException>(() => _executor.ExecutarAsync(
            new ComandoExterno("programa-que-nao-existe-actum.exe", [], Limite),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AmbienteDoActumNaoEhRepassadoAsync()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:5280");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "http://localhost:5273");
        try
        {
            var resultado = await _executor.ExecutarAsync(
                Comando(
                    "[Console]::Out.Write(\"[$env:ASPNETCORE_URLS][$env:Cors__AllowedOrigins__0][$env:ACTUM_TESTE]\")",
                    variaveis: new Dictionary<string, string> { ["ACTUM_TESTE"] = "ok" }),
                TestContext.Current.CancellationToken);

            Assert.Equal("[][][ok]", resultado.Saida.Trim());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", null);
            Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", null);
        }
    }

    [Fact]
    public async Task PastaDeTrabalhoAsync()
    {
        var pasta = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        var resultado = await _executor.ExecutarAsync(
            Comando("[Console]::Out.Write((Get-Location).Path)") with { PastaDeTrabalho = pasta },
            TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath(pasta), Path.GetFullPath(resultado.Saida.Trim()), ignoreCase: true);
    }

    private static ComandoExterno Comando(string script, TimeSpan? limite = null, IReadOnlyDictionary<string, string>? variaveis = null)
    {
        return new ComandoExterno(PowerShell, ["-NoProfile", "-NonInteractive", "-Command", script], limite ?? Limite, Variaveis: variaveis);
    }
}
