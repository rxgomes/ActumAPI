using Actum.Shared.Abstractions.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace Actum.Api.Errors;

/// <summary>
/// Converte <see cref="DomainException"/> em <c>400</c> no formato Problem Details, com a mensagem da regra
/// (pt-BR, voltada ao usuário) no <c>detail</c> em qualquer ambiente. Não é registrada como erro no log,
/// pois é entrada inválida e não falha da API.
/// </summary>
internal sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        logger.LogInformation("Regra de negócio violada: {Mensagem}", domainException.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = domainException.Message,
            },
        });
    }
}
