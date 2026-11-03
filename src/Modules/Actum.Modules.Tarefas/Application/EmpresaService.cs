using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Company use cases over <see cref="TarefasDbContext"/>
/// </summary>
internal sealed class EmpresaService(TarefasDbContext context) : IEmpresaService
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

        if (request.Cor is not null)
        {
            empresa.Cor = request.Cor.ToUpperInvariant();
        }

        if (request.Sincronizar is { } sincronizar)
        {
            empresa.Sincronizar = sincronizar;
        }

        await context.SaveChangesAsync(cancellationToken);

        return ParaResponse(empresa);
    }

    internal static EmpresaResponse ParaResponse(Empresa empresa)
    {
        return new EmpresaResponse(empresa.Identifier, empresa.Nome, empresa.Cor, empresa.Sincronizar, empresa.CargaInicialEm);
    }
}
