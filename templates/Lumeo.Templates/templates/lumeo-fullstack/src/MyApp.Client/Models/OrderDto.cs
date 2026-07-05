namespace MyApp;

/// <summary>
/// Client-side shape of a row from <c>GET /api/orders</c>. Matches the API's Order
/// entity JSON; kept as a small local record so the client has no dependency on the API
/// project (a shared contracts project is an easy next step if you prefer).
/// </summary>
public sealed record OrderDto(int Id, string Customer, decimal Total, string Status, DateOnly Date);
