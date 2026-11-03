using Actum.Modules.Asana.Tests;
using Actum.Shared.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Actum.Api.Tests;

/// <summary>
/// Sobe a API em memória com o ambiente, a connection string e as configurações extras informados.
/// Inclui os endpoints de <see cref="EndpointsDeTeste"/>. As chamadas ao Asana vão para <paramref name="asana"/>
/// (por padrão, um handler sem respostas): nenhum teste acessa o Asana real.
/// </summary>
public sealed class ActumApiFactory(
    string environment,
    string connectionString,
    IReadOnlyDictionary<string, string>? settings = null,
    ColetorDeLogs? logs = null,
    HttpMessageHandlerFalso? asana = null) : WebApplicationFactory<Program>
{
    /// <summary>
    /// Connection string para uma porta sem servidor, para simular o banco inacessível.
    /// </summary>
    public const string BancoInacessivel = "Host=localhost;Port=1;Database=actum;Username=actum;Password=actum;Timeout=2";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Actum", connectionString);

        foreach (var (chave, valor) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(chave, valor);
        }

        builder.ConfigureServices(services =>
        {
            var modulo = new ModuloDeTeste();
            services.AddSingleton<IModule>(modulo);
            modulo.RegisterServices(services, new ConfigurationBuilder().Build());

            var handlerDoAsana = asana ?? new HttpMessageHandlerFalso();
            services.ConfigureHttpClientDefaults(cliente => cliente.ConfigurePrimaryHttpMessageHandler(() => handlerDoAsana));
        });

        if (logs is not null)
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        }
    }
}
