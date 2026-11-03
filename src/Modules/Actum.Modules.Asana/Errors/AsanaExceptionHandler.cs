using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Asana.Errors;

/// <summary>
/// Translates <see cref="AsanaException"/> thrown by any endpoint into Problem Details, with the pt-BR message
/// in <c>detail</c>: token missing or rejected becomes <c>400</c>; Asana unreachable becomes <c>502</c>.
/// </summary>
internal sealed class AsanaExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<AsanaExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AsanaException asanaException)
        {
            return false;
        }

        var status = asanaException is AsanaIndisponivelException
            ? StatusCodes.Status502BadGateway
            : StatusCodes.Status400BadRequest;

        logger.LogWarning(asanaException.InnerException, "Falha na comunicação com o Asana: {Mensagem}", asanaException.Message);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Detail = asanaException.Message,
            },
        });
    }
}
