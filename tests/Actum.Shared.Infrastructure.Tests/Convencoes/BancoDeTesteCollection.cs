namespace Actum.Shared.Infrastructure.Tests.Convencoes;

/// <summary>
/// Classes de teste que aplicam as migrations no banco temporário compartilhado. Ficam na mesma
/// collection para não rodarem em paralelo, senão duas aplicam a mesma migration ao mesmo tempo.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class BancoDeTesteCollection
{
    public const string Nome = "Banco de teste";
}
