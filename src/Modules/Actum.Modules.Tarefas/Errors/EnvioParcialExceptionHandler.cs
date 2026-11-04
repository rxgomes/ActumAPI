using Actum.Modules.Asana.Errors;
using Actum.Modules.Tarefas.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Tarefas.Errors;

/// <summary>
/// Translates <see cref="EnvioParcialException"/> into Problem Details with the status of the failed step
/// (<c>502</c> Asana unavailable, <c>400</c> otherwise), the pt-BR message and the <c>anexosEnviados</c> extension
/// </summary>
internal sealed class EnvioParcialExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<EnvioParcialExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not EnvioParcialException envioParcial)
        {
            return false;
        }

        var status = envioParcial.Falha is AsanaIndisponivelException
            ? StatusCodes.Status502BadGateway
            : StatusCodes.Status400BadRequest;

        logger.LogWarning(envioParcial.Falha, "Envio ao Asana parcial: {Quantidade} anexos enviados antes da falha", envioParcial.AnexosEnviados.Count);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Detail = envioParcial.Message,
                Extensions = { ["anexosEnviados"] = envioParcial.AnexosEnviados },
            },
        });
    }
}
