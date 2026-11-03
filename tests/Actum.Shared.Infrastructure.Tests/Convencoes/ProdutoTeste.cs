using Actum.Shared.Abstractions.Domain;

namespace Actum.Shared.Infrastructure.Tests.Convencoes;

public sealed class ProdutoTeste : Entity
{
    public string Nome { get; set; } = string.Empty;
}
