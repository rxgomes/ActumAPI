using System.Text.RegularExpressions;
using Actum.Modules.Git.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Company use cases over <see cref="TarefasDbContext"/>
/// </summary>
internal sealed partial class EmpresaService(TarefasDbContext context, IGitLocal git) : IEmpresaService
{
    public async Task<IReadOnlyList<EmpresaResponse>> ListarAsync(CancellationToken cancellationToken)
    {
        var empresas = await context.Set<Empresa>()
            .AsNoTracking()
            .OrderBy(empresa => empresa.Nome)
            .ToListAsync(cancellationToken);

        return empresas.Select(ParaResponse).ToList();
    }

    public async Task<EmpresaResponse?> AlterarAsync(Guid identifier, AlterarEmpresaRequest request, CancellationToken cancellationToken)
    {
        var empresa = await context.Set<Empresa>().SingleOrDefaultAsync(e => e.Identifier == identifier, cancellationToken);
        if (empresa is null)
        {
            return null;
        }

        // Tudo é validado antes de alterar a entidade: um campo inválido não deixa os outros gravados.
        var repositorioFront = request.RepositorioFront is null ? empresa.RepositorioFront : RepositorioOuNulo(request.RepositorioFront);
        var repositorioBack = request.RepositorioBack is null ? empresa.RepositorioBack : RepositorioOuNulo(request.RepositorioBack);
        var cloneFront = request.CloneFront is null ? empresa.CloneFront : CaminhoOuNulo(request.CloneFront, "O clone de front");
        var cloneBack = request.CloneBack is null ? empresa.CloneBack : CaminhoOuNulo(request.CloneBack, "O clone de back");
        var pastaWorktrees = request.PastaWorktrees is null ? empresa.PastaWorktrees : CaminhoOuNulo(request.PastaWorktrees, "A pasta dos worktrees");
        var arquivosFront = request.ArquivosFront is null ? empresa.ArquivosFront : Arquivos(request.ArquivosFront, "front");
        var arquivosBack = request.ArquivosBack is null ? empresa.ArquivosBack : Arquivos(request.ArquivosBack, "back");

        // O clone é conferido quando ele ou o repositório da parte muda.
        if (cloneFront is not null && (request.CloneFront is not null || request.RepositorioFront is not null))
        {
            await ConferirCloneAsync(cloneFront, repositorioFront, "front", cancellationToken);
        }

        if (cloneBack is not null && (request.CloneBack is not null || request.RepositorioBack is not null))
        {
            await ConferirCloneAsync(cloneBack, repositorioBack, "back", cancellationToken);
        }

        if (request.Cor is not null)
        {
            empresa.Cor = request.Cor.ToUpperInvariant();
        }

        if (request.Sincronizar is { } sincronizar)
        {
            empresa.Sincronizar = sincronizar;
        }

        empresa.RepositorioFront = repositorioFront;
        empresa.RepositorioBack = repositorioBack;
        empresa.CloneFront = cloneFront;
        empresa.CloneBack = cloneBack;
        empresa.PastaWorktrees = pastaWorktrees;
        empresa.ArquivosFront = arquivosFront;
        empresa.ArquivosBack = arquivosBack;

        await context.SaveChangesAsync(cancellationToken);

        return ParaResponse(empresa);
    }

    internal static EmpresaResponse ParaResponse(Empresa empresa)
    {
        return new EmpresaResponse(
            empresa.Identifier,
            empresa.Nome,
            empresa.Cor,
            empresa.Sincronizar,
            empresa.CargaInicialEm,
            empresa.RepositorioFront,
            empresa.RepositorioBack,
            empresa.CloneFront,
            empresa.CloneBack,
            empresa.PastaWorktrees,
            empresa.ArquivosFront,
            empresa.ArquivosBack);
    }

    /// <summary>
    /// <c>owner/repo</c> of a remote URL of the GitHub (<c>https://github.com/owner/repo(.git)</c> or
    /// <c>git@github.com:owner/repo(.git)</c>), or <see langword="null"/> when the URL is not of the GitHub
    /// </summary>
    internal static string? RepositorioDaUrl(string url)
    {
        var resultado = UrlDoGitHub().Match(url.Trim());
        return resultado.Success ? resultado.Groups["repositorio"].Value : null;
    }

    private async Task ConferirCloneAsync(string clone, string? repositorio, string parte, CancellationToken cancellationToken)
    {
        if (repositorio is null)
        {
            throw new DomainException($"Cadastre o repositório de {parte} no GitHub antes do clone local de {parte}.");
        }

        var origem = await git.ObterOrigemAsync(clone, cancellationToken)
            ?? throw new DomainException($"A pasta {clone} não é a raiz de um repositório git com o remoto origin (clone de {parte}).");

        var repositorioDoClone = RepositorioDaUrl(origem);
        if (!string.Equals(repositorioDoClone, repositorio, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                $"O clone de {parte} ({clone}) aponta para {repositorioDoClone ?? origem}, mas o repositório de {parte} da empresa é {repositorio}.");
        }
    }

    /// <summary>
    /// Repository without spaces at the ends; empty text removes it
    /// </summary>
    private static string? RepositorioOuNulo(string repositorio)
    {
        var valor = repositorio.Trim();
        return valor.Length == 0 ? null : valor;
    }

    /// <summary>
    /// Absolute path without spaces at the ends and without the final separator; empty text removes it
    /// </summary>
    private static string? CaminhoOuNulo(string caminho, string campo)
    {
        var valor = caminho.Trim();
        if (valor.Length == 0)
        {
            return null;
        }

        if (!Path.IsPathFullyQualified(valor) || valor.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new DomainException($"{campo} deve ser um caminho absoluto (ex.: E:\\wt\\aureum). Valor recebido: {valor}.");
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(valor));
    }

    /// <summary>
    /// Relative paths inside the repository, without spaces at the ends, blank items and repetitions
    /// </summary>
    private static List<string> Arquivos(IReadOnlyList<string> arquivos, string parte)
    {
        var lista = new List<string>();
        foreach (var arquivo in arquivos.Select(item => (item ?? string.Empty).Trim()).Where(item => item.Length > 0))
        {
            var segmentos = arquivo.Split(['/', '\\']);
            if (Path.IsPathRooted(arquivo)
                || arquivo.Contains(':', StringComparison.Ordinal)
                || segmentos.Any(segmento => segmento is ".." or "." or "")
                || arquivo.Length > Empresa.CaminhoMaxLength
                || arquivo.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                throw new DomainException(
                    $"Os arquivos de {parte} devem ser caminhos relativos ao repositório, sem '..' (ex.: .env.development). Valor recebido: {arquivo}.");
            }

            if (lista.Contains(arquivo, StringComparer.OrdinalIgnoreCase))
            {
                throw new DomainException($"O arquivo {arquivo} está repetido nos arquivos de {parte}.");
            }

            lista.Add(arquivo);
        }

        if (lista.Count > Empresa.ArquivosMaxCount)
        {
            throw new DomainException($"Informe no máximo {Empresa.ArquivosMaxCount} arquivos de {parte}.");
        }

        return lista;
    }

    [GeneratedRegex(@"github\.com[:/](?<repositorio>[A-Za-z0-9-]+/[A-Za-z0-9._-]+?)(\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlDoGitHub();
}
