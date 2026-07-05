using Microsoft.EntityFrameworkCore;
using MyApp.Api.Orders;

namespace MyApp.Api.Data;

/// <summary>
/// Seeds deterministic sample <see cref="Order"/> rows on first run so the client
/// dashboard has something to render. Idempotent — does nothing once rows exist.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Orders.AnyAsync(ct))
            return;

        db.Orders.AddRange(
            new Order { Customer = "Acme Corp",         Total = 1299m, Status = "Paid",     Date = new DateOnly(2026, 6, 28) },
            new Order { Customer = "Globex",            Total = 480m,  Status = "Pending",  Date = new DateOnly(2026, 6, 27) },
            new Order { Customer = "Initech",           Total = 2150m, Status = "Paid",     Date = new DateOnly(2026, 6, 25) },
            new Order { Customer = "Umbrella",          Total = 90m,   Status = "Refunded", Date = new DateOnly(2026, 6, 24) },
            new Order { Customer = "Hooli",             Total = 760m,  Status = "Paid",     Date = new DateOnly(2026, 6, 22) },
            new Order { Customer = "Stark Industries",  Total = 3400m, Status = "Paid",     Date = new DateOnly(2026, 6, 21) },
            new Order { Customer = "Wayne Enterprises", Total = 220m,  Status = "Pending",  Date = new DateOnly(2026, 6, 20) },
            new Order { Customer = "Cyberdyne",         Total = 1875m, Status = "Paid",     Date = new DateOnly(2026, 6, 19) },
            new Order { Customer = "Soylent",           Total = 640m,  Status = "Refunded", Date = new DateOnly(2026, 6, 18) },
            new Order { Customer = "Massive Dynamic",   Total = 5120m, Status = "Paid",     Date = new DateOnly(2026, 6, 17) },
            new Order { Customer = "Tyrell",            Total = 305m,  Status = "Pending",  Date = new DateOnly(2026, 6, 16) },
            new Order { Customer = "Wonka",             Total = 1490m, Status = "Paid",     Date = new DateOnly(2026, 6, 15) });

        await db.SaveChangesAsync(ct);
    }
}
