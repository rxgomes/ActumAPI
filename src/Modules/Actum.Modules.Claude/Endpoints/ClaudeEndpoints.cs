using Actum.Modules.Claude.Application;
using Actum.Modules.Claude.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Actum.Modules.Claude.Endpoints;

/// <summary>
/// HTTP endpoints of the Claude API integration (<c>/claude</c>)
/// </summary>
public static class ClaudeEndpoints
{
    /// <summary>
    /// Map the <c>/claude</c> endpoints
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapClaudeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/claude").WithTags("Claude");

        group.MapGet("/situacao", ObterSituacao)
            .WithName("ObterSituacaoClaude")
            .WithSummary("Situação da configuração da Claude API")
            .WithDescription("Informa se a chave da Claude API (Claude:ApiKey) está configurada e qual modelo será usado nas análises. Não chama a Anthropic e nunca devolve a chave.")
            .Produces<ClaudeSituacaoResponse>();

        return endpoints;
    }

    private static Ok<ClaudeSituacaoResponse> ObterSituacao(IConfiguration configuration)
    {
        var opcoes = ClaudeOptions.Ler(configuration);

        return TypedResults.Ok(new ClaudeSituacaoResponse(opcoes.Configurado, opcoes.Modelo));
    }
}
