using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// HTTP endpoints of the companies (<c>/empresas</c>)
/// </summary>
public static class EmpresaEndpoints
{
    /// <summary>
    /// Map the <c>/empresas</c> endpoints
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapEmpresaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/empresas").WithTags("Empresas");

        group.MapGet("/", ListarAsync)
            .WithName("ListarEmpresas")
            .WithSummary("Listar empresas")
            .WithDescription("Lista as empresas (workspaces do Asana), ordenadas por nome. As empresas são criadas pela sincronização.");

        group.MapPatch("/{identifier:guid}", AlterarAsync)
            .WithName("AlterarEmpresa")
            .WithSummary("Alterar empresa")
            .WithDescription("Altera a cor (#RRGGBB) e/ou se a empresa entra na sincronização. Campos não enviados são mantidos.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<EmpresaResponse>>> ListarAsync(
        IEmpresaService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.ListarAsync(cancellationToken));
    }

    private static async Task<Results<Ok<EmpresaResponse>, ProblemHttpResult>> AlterarAsync(
        Guid identifier,
        AlterarEmpresaRequest request,
        IEmpresaService service,
        CancellationToken cancellationToken)
    {
        var empresa = await service.AlterarAsync(identifier, request, cancellationToken);
        if (empresa is null)
        {
            return TypedResults.Problem(detail: "Empresa não encontrada.", statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(empresa);
    }
}
