using Actum.Modules.Asana.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// <see cref="IAnexoService"/> over <see cref="TarefasDbContext"/> (to find the Asana task) and the public
/// <see cref="IAsanaClient"/> contract of the Asana module
/// </summary>
internal sealed class AnexoService(TarefasDbContext context, IAsanaClient asana) : IAnexoService
{
    private const string TarefaExcluidaNoAsana = "A tarefa não existe mais no Asana.";

    private const string SemEndereco = "O Asana não oferece um endereço para baixar este anexo.";

    public async Task<IReadOnlyList<AnexoResponse>?> ListarAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        var asanaGid = await AsanaGidAsync(tarefaIdentifier, cancellationToken);
        if (asanaGid is null)
        {
            return null;
        }

        var anexos = await asana.ListarAnexosAsync(asanaGid, cancellationToken)
            ?? throw new DomainException(TarefaExcluidaNoAsana);

        return anexos
            .OrderBy(anexo => anexo.CriadoEm)
            .Select(anexo => new AnexoResponse(anexo.Gid, anexo.Nome, anexo.CriadoEm, anexo.Tamanho, anexo.Hospedagem))
            .ToList();
    }

    public async Task<Uri?> ObterEnderecoDeDownloadAsync(Guid tarefaIdentifier, string anexoGid, CancellationToken cancellationToken)
    {
        var asanaGid = await AsanaGidAsync(tarefaIdentifier, cancellationToken);
        if (asanaGid is null || string.IsNullOrWhiteSpace(anexoGid))
        {
            return null;
        }

        // Só baixa anexos desta tarefa: o gid vem da URL e poderia apontar para qualquer arquivo do Asana.
        var download = await asana.ObterDownloadDoAnexoAsync(anexoGid, cancellationToken);
        if (download is null || download.TarefaGid != asanaGid)
        {
            return null;
        }

        return download.Endereco ?? throw new DomainException(SemEndereco);
    }

    private async Task<string?> AsanaGidAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        return await context.Set<Tarefa>()
            .Where(tarefa => tarefa.Identifier == tarefaIdentifier)
            .Select(tarefa => tarefa.AsanaGid)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
