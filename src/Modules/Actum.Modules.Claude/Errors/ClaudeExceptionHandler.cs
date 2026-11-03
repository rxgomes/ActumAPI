using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Claude.Errors;

/// <summary>
/// Translates <see cref="ClaudeException"/> thrown by any endpoint into Problem Details, with the pt-BR message
/// in <c>detail</c>: key missing or rejected and model refusal become <c>400</c>; Anthropic unreachable and
/// incomplete answers become <c>502</c>.
/// </summary>
internal sealed class ClaudeExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ClaudeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ClaudeException claudeException)
        {
            return false;
        }

        var status = claudeException is ClaudeIndisponivelException or ClaudeRespostaIncompletaException
            ? StatusCodes.Status502BadGateway
            : StatusCodes.Status400BadRequest;

        logger.LogWarning(claudeException.InnerException, "Falha na análise com a Claude API: {Mensagem}", claudeException.Message);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Detail = claudeException.Message,
            },
        });
    }
}
