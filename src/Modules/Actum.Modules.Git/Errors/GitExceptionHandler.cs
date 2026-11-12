using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Git.Errors;

/// <summary>
/// Translates <see cref="GitException"/> thrown by any endpoint into Problem Details, with the pt-BR message in
/// <c>detail</c>: git missing, time limit exceeded or remote unreachable becomes <c>502</c>; command refused becomes
/// <c>400</c>.
/// </summary>
internal sealed class GitExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GitExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not GitException gitException)
        {
            return false;
        }

        var status = gitException is GitComandoRecusadoException
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status502BadGateway;

        logger.LogWarning(gitException.InnerException, "Falha ao executar o git: {Mensagem}", gitException.Message);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Detail = gitException.Message,
            },
        });
    }
}
