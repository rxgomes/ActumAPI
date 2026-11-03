namespace Actum.Api.Cors;

/// <summary>
/// Política CORS única do host, com as origens permitidas vindas de <c>Cors:AllowedOrigins</c>.
/// Sem origens configuradas, nenhuma requisição cross-origin é liberada.
/// </summary>
internal static class CorsExtensions
{
    public const string AllowedOriginsKey = "Cors:AllowedOrigins";

    private readonly static TimeSpan PreflightMaxAge = TimeSpan.FromMinutes(10);

    public static IServiceCollection AddActumCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];
        if (allowedOrigins.Any(origin => origin.Contains('*')))
        {
            throw new InvalidOperationException(
                $"'{AllowedOriginsKey}' não aceita curinga (*). Liste as origens explicitamente.");
        }

        // Sem AllowCredentials: será revisto junto com a autenticação.
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .SetPreflightMaxAge(PreflightMaxAge)));

        return services;
    }

    public static IApplicationBuilder UseActumCors(this IApplicationBuilder app)
    {
        return app.UseCors();
    }
}
