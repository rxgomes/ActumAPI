namespace Actum.Api.Errors;

/// <summary>
/// Respostas de erro no formato Problem Details (RFC 9457), com títulos em pt-BR e <c>traceId</c>.
/// </summary>
internal static class ErrorHandlingExtensions
{
    public const string TituloValidacao = "Um ou mais campos são inválidos.";

    private static readonly Dictionary<int, string> TitulosPorStatus = new()
    {
        [StatusCodes.Status400BadRequest] = "Requisição inválida.",
        [StatusCodes.Status404NotFound] = "Recurso não encontrado.",
        [StatusCodes.Status405MethodNotAllowed] = "Método não permitido.",
        [StatusCodes.Status409Conflict] = "Conflito.",
        [StatusCodes.Status500InternalServerError] = "Ocorreu um erro inesperado.",
        [StatusCodes.Status502BadGateway] = "Falha na comunicação com um serviço externo.",
    };

    public static IServiceCollection AddActumErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Personalizar);
        services.AddValidation();

        // DomainException vira 400 com a mensagem da regra; as demais seguem para o 500 padrão.
        services.AddExceptionHandler<DomainExceptionHandler>();

        // Erros de binding (JSON malformado etc.) viram 400 sem corpo em todos os ambientes;
        // o UseStatusCodePages converte em Problem Details.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

        return services;
    }

    public static IApplicationBuilder UseActumErrorHandling(this IApplicationBuilder app)
    {
        // Registra a exceção no log e responde com o IProblemDetailsService.
        app.UseExceptionHandler();

        // Só atua em respostas de erro sem corpo (ex.: 404/405 do roteamento).
        app.UseStatusCodePages();

        return app;
    }

    private static void Personalizar(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;

        if (problem is HttpValidationProblemDetails)
        {
            problem.Title = TituloValidacao;
        }
        else if (problem.Status is { } status && TitulosPorStatus.TryGetValue(status, out var titulo))
        {
            problem.Title = titulo;
        }

        var ambiente = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (context.Exception is { } exception && ambiente.IsDevelopment())
        {
            problem.Detail = exception.Message;
        }
    }
}
