using System.Text.Json;
using System.Text.Json.Serialization;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Serializes an enum as lowercase snake_case text in the API (<c>EmAndamento</c> → <c>em_andamento</c>), the same
/// values stored in the database
/// </summary>
/// <typeparam name="TEnum">Enum type</typeparam>
public sealed class SnakeCaseEnumJsonConverter<TEnum>() : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false)
    where TEnum : struct, Enum;
