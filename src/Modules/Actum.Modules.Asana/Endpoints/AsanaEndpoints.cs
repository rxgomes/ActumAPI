using Actum.Modules.Asana.Application;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Asana.Endpoints;

/// <summary>
/// HTTP endpoints of the Asana integration (<c>/asana</c>)
/// </summary>
public static class AsanaEndpoints
{
    /// <summary>
    /// Map the <c>/asana</c> endpoints
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapAsanaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/asana").WithTags("Asana");

        group.MapGet("/conexao", VerificarConexaoAsync)
            .WithName("VerificarConexaoAsana")
            .WithSummary("Verificar conexão com o Asana")
            .WithDescription("Informa se o token do Asana está configurado e é válido, com o nome do usuário e os workspaces acessíveis. Token ausente ou inválido não é erro: responde configurado=false ou valido=false.")
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return endpoints;
    }

    private static async Task<Ok<AsanaConexaoResponse>> VerificarConexaoAsync(IAsanaClient asanaClient, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await asanaClient.ObterUsuarioAtualAsync(cancellationToken);

            return TypedResults.Ok(new AsanaConexaoResponse(true, true, usuario.Nome, usuario.Workspaces));
        }
        catch (AsanaNaoConfiguradoException)
        {
            return TypedResults.Ok(new AsanaConexaoResponse(false, false, null, []));
        }
        catch (AsanaAcessoNegadoException)
        {
            return TypedResults.Ok(new AsanaConexaoResponse(true, false, null, []));
        }
    }
}
