using System.ComponentModel.DataAnnotations;
using Actum.Modules.Asana.Application;
using Actum.Shared.Abstractions;
using Actum.Shared.Abstractions.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Actum.Api.Tests;

/// <summary>
/// Endpoints usados só nos testes, em <c>/__testes/*</c>, para exercitar o pipeline de erros
/// como um endpoint de módulo (declarado fora do projeto Web).
/// </summary>
public static class EndpointsDeTeste
{
    public const string MensagemDaExcecao = "detalhe interno";

    public const string MensagemDaRegra = "CPF inválido.";

    public static void Mapear(IEndpointRouteBuilder endpoints)
    {
        var grupo = endpoints.MapGroup("/__testes");

        grupo.MapGet("/ok", () => Results.Ok("ok"));
        grupo.MapGet("/excecao", string () => throw new InvalidOperationException(MensagemDaExcecao));
        grupo.MapGet("/somente-get", () => Results.Ok());
        grupo.MapPost("/validacao", (DadosDeTeste dados) => Results.Ok(dados));
        grupo.MapGet("/regra", string () => throw new DomainException(MensagemDaRegra));
        grupo.MapGet("/conflito", () => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict));

        // Depende do Asana sem tratar falhas, como um endpoint do módulo Tarefas.
        grupo.MapGet("/asana", async (IAsanaClient asana, CancellationToken cancellationToken) =>
            Results.Ok(await asana.ObterUsuarioAtualAsync(cancellationToken)));
    }
}

public sealed record DadosDeTeste(
    [property: Required(ErrorMessage = "O nome é obrigatório.")] string? Nome,
    [property: Range(1, 10, ErrorMessage = "A quantidade deve estar entre {1} e {2}.")] int Quantidade);

/// <summary>
/// Módulo de teste que expõe <see cref="EndpointsDeTeste"/> pelo mesmo caminho dos módulos reais.
/// </summary>
internal sealed class ModuloDeTeste : IModule
{
    public string Name => "Testes";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Como num módulo real: registra a validação dos tipos desta compilação.
        services.AddValidation();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        EndpointsDeTeste.Mapear(endpoints);
    }
}
