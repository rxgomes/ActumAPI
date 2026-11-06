using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.GitHub.Errors;

/// <summary>
/// Translates <see cref="GitHubException"/> thrown by any endpoint into Problem Details, with the pt-BR message
/// in <c>detail</c>: token missing or rejected, repository inaccessible or branch rejected becomes <c>400</c>;
/// GitHub unreachable becomes <c>502</c>.
/// </summary>
internal sealed class GitHubExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GitHubExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not GitHubException gitHubException)
        {
            return false;
        }

        var status = gitHubException is GitHubIndisponivelException
            ? StatusCodes.Status502BadGateway
            : StatusCodes.Status400BadRequest;

        logger.LogWarning(gitHubException.InnerException, "Falha na comunicação com o GitHub: {Mensagem}", gitHubException.Message);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Detail = gitHubException.Message,
            },
        });
    }
}
