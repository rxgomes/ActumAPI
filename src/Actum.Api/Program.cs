using Actum.Api.Cors;
using Actum.Api.Errors;
using Actum.Api.HealthChecks;
using Actum.Api.Modules;
using Actum.Shared.Abstractions;
using Actum.Shared.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Actum API";
        return Task.CompletedTask;
    });
});

builder.Services.AddActumErrorHandling();
builder.Services.AddActumCors(builder.Configuration);
builder.Services.AddActumDatabase(builder.Configuration);
builder.Services.AddActumHealthChecks();

foreach (var module in ModuleCatalog.All)
{
    builder.Services.AddSingleton(module);
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseActumErrorHandling();
app.UseActumCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapActumHealthChecks();

// Endpoints vêm dos módulos registrados no container (ModuleCatalog; nos testes, também módulos de teste).
foreach (var module in app.Services.GetServices<IModule>())
{
    module.MapEndpoints(app);
}

app.Run();

/// <summary>
/// Exposto para os testes de integração (<c>WebApplicationFactory&lt;Program&gt;</c>).
/// </summary>
public partial class Program;
