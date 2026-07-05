using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyApp.Api.Orders;

namespace MyApp.Api.Data;

/// <summary>
/// EF Core context. Inherits every ASP.NET Core Identity table (users, roles, claims,
/// tokens, …) from <see cref="IdentityDbContext{TUser}"/> and adds the sample
/// <see cref="Order"/> table that the client dashboard reads.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Order>(e =>
        {
            e.Property(o => o.Customer).HasMaxLength(120).IsRequired();
            e.Property(o => o.Status).HasMaxLength(32).IsRequired();
            e.Property(o => o.Total).HasColumnType("numeric(12,2)");
        });
    }
}
