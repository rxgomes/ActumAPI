using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Actum.Modules.Tarefas.Persistence;

/// <summary>
/// Stores an enum as lowercase snake_case text (<c>EmAndamento</c> → <c>em_andamento</c>), so the column can be
/// read and filtered in SQL without knowing the numeric values
/// </summary>
/// <typeparam name="TEnum">Enum type</typeparam>
internal sealed class EnumSnakeCaseConverter<TEnum>() : ValueConverter<TEnum, string>(
    valor => ParaTexto(valor),
    texto => ParaEnum(texto))
    where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> PorTexto = Enum.GetValues<TEnum>()
        .ToDictionary(ParaTexto, valor => valor, StringComparer.Ordinal);

    public static string ParaTexto(TEnum valor)
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(valor.ToString());
    }

    public static TEnum ParaEnum(string texto)
    {
        if (!PorTexto.TryGetValue(texto, out var valor))
        {
            throw new InvalidOperationException($"Valor '{texto}' inválido para {typeof(TEnum).Name}.");
        }

        return valor;
    }
}
