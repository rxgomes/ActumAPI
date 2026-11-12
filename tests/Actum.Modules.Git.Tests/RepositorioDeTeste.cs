using Actum.Modules.Git.Application;
using Actum.Shared.Infrastructure.Processos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.Git.Tests;

/// <summary>
/// Repositório git real numa pasta temporária: um repositório <c>--bare</c> no papel do GitHub (<c>origin</c>) e um
/// clone dele no papel do clone local do usuário, com um commit inicial em <c>main</c> (<c>README.md</c> e um
/// <c>.gitignore</c> com <c>.env.development</c> e <c>node_modules/</c>). A pasta raiz tem espaço no nome.
/// </summary>
public sealed class RepositorioDeTeste : IAsyncDisposable
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(60);

    private readonly ExecutorDeProcessos _executor = new();

    private RepositorioDeTeste(string raiz)
    {
        Raiz = raiz;
        Origem = Path.Combine(raiz, "origem.git");
        Clone = Path.Combine(raiz, "clone");
        Worktrees = Path.Combine(raiz, "Minha Empresa");
    }

    /// <summary>
    /// Pasta temporária com tudo
    /// </summary>
    public string Raiz { get; }

    /// <summary>
    /// Repositório <c>--bare</c> usado como <c>origin</c>
    /// </summary>
    public string Origem { get; }

    /// <summary>
    /// Clone de <see cref="Origem"/>
    /// </summary>
    public string Clone { get; }

    /// <summary>
    /// Pasta (com espaço no nome) onde os testes criam os worktrees
    /// </summary>
    public string Worktrees { get; }

    /// <summary>
    /// <see cref="GitLocal"/> real sobre o executor de processos
    /// </summary>
    public IGitLocal Git { get; } = new GitLocal(
        new ExecutorDeProcessos(),
        new TravasDosClones(),
        new ConfigurationBuilder().Build(),
        NullLogger<GitLocal>.Instance);

    /// <summary>
    /// Cria a origem, o clone e o commit inicial
    /// </summary>
    public static async Task<RepositorioDeTeste> CriarAsync()
    {
        var raiz = Path.Combine(Path.GetTempPath(), $"actum git {Guid.NewGuid():N}");
        Directory.CreateDirectory(raiz);

        var repositorio = new RepositorioDeTeste(raiz);
        await repositorio.ExecutarAsync(raiz, "init", "--bare", "-b", "main", repositorio.Origem);
        await repositorio.ExecutarAsync(raiz, "clone", repositorio.Origem, repositorio.Clone);

        await File.WriteAllTextAsync(Path.Combine(repositorio.Clone, "README.md"), "teste\n");
        await File.WriteAllTextAsync(Path.Combine(repositorio.Clone, ".gitignore"), ".env.development\nnode_modules/\n");
        await repositorio.ExecutarAsync(repositorio.Clone, "checkout", "-b", "main");
        await repositorio.ExecutarAsync(repositorio.Clone, "add", ".");
        await repositorio.CommitAsync(repositorio.Clone, "inicial");
        await repositorio.ExecutarAsync(repositorio.Clone, "push", "-u", "origin", "main");

        return repositorio;
    }

    /// <summary>
    /// Cria uma branch direto na origem a partir de <c>main</c>, como o Actum faz no GitHub
    /// </summary>
    public Task CriarBranchNaOrigemAsync(string branch)
    {
        return ExecutarAsync(Origem, "branch", branch, "main");
    }

    /// <summary>
    /// Commit de tudo que está no índice, com autor fixo
    /// </summary>
    public Task CommitAsync(string pasta, string mensagem)
    {
        return ExecutarAsync(pasta, "-c", "user.name=Teste", "-c", "user.email=teste@actum.local", "commit", "-m", mensagem);
    }

    /// <summary>
    /// Executa o git numa pasta e devolve a saída; falha o teste se o git falhar
    /// </summary>
    public async Task<string> ExecutarAsync(string pasta, params string[] argumentos)
    {
        var resultado = await _executor.ExecutarAsync(
            new ComandoExterno("git", ["-C", pasta, .. argumentos], Limite),
            TestContext.Current.CancellationToken);

        Assert.True(resultado.Sucesso, $"git {string.Join(' ', argumentos)}: {resultado.Erro}");

        return resultado.Saida.Trim();
    }

    public ValueTask DisposeAsync()
    {
        // Os objetos do git são somente leitura: sem tirar o atributo, o Directory.Delete falha.
        try
        {
            foreach (var arquivo in Directory.EnumerateFiles(Raiz, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(arquivo, FileAttributes.Normal);
            }

            Directory.Delete(Raiz, recursive: true);
        }
        catch (IOException)
        {
            // Pasta temporária: sobra no %TEMP% se algum processo ainda segurar um arquivo.
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ValueTask.CompletedTask;
    }
}
