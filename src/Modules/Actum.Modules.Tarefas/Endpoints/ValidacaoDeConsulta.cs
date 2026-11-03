using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// Validation of query objects bound with <c>[AsParameters]</c>, answered as <c>400</c> with <c>errors</c> per field
/// </summary>
internal static class ValidacaoDeConsulta
{
    public static ValidationProblem? Invalida(object consulta)
    {
        var resultados = new List<ValidationResult>();
        if (Validator.TryValidateObject(consulta, new ValidationContext(consulta), resultados, validateAllProperties: true))
        {
            return null;
        }

        var erros = resultados
            .SelectMany(resultado => resultado.MemberNames.Select(campo => (Campo: campo, Mensagem: resultado.ErrorMessage ?? string.Empty)))
            .GroupBy(erro => erro.Campo)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(erro => erro.Mensagem).Distinct().ToArray());

        return TypedResults.ValidationProblem(erros);
    }
}
