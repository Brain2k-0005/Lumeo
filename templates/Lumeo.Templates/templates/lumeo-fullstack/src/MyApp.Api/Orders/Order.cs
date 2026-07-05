namespace MyApp.Api.Orders;

/// <summary>
/// A sample business entity persisted in PostgreSQL and exposed (read-only) at
/// <c>GET /api/orders</c>. The client dashboard renders these rows in a Lumeo DataGrid.
/// </summary>
public sealed class Order
{
    public int Id { get; set; }
    public required string Customer { get; set; }
    public decimal Total { get; set; }
    public required string Status { get; set; }
    public DateOnly Date { get; set; }
}
