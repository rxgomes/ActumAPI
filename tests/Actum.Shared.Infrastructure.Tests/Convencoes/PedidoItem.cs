namespace Actum.Shared.Infrastructure.Tests.Convencoes;

public sealed class PedidoItem
{
    public int Id { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateOnly DataEntrega { get; set; }

    public DateTime? DataCancelamento { get; set; }
}
