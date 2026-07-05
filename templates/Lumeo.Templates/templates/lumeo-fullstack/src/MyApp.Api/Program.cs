using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.Api.Data;
using MyApp.Api.Email;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Persistence -----------------------------------------------------------------
// PostgreSQL via Npgsql. The connection string comes from configuration
// ("ConnectionStrings:Default") and is overridden per-environment (compose sets it
// to the `postgres` service; hybrid dev points it at localhost — see README).
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// --- Identity + the built-in Identity API endpoints -------------------------------
// AddIdentityApiEndpoints wires up BOTH a bearer-token scheme and a cookie scheme and
// maps them behind one policy scheme, so `MapIdentityApi` below exposes register /
// login / refresh / confirmEmail / manage/info / 2fa out of the box. We require a
// confirmed email, which makes the MailHog confirmation flow real: login is blocked
// with a 401 "NotAllowed" until the emailed link is clicked.
builder.Services
    .AddIdentityApiEndpoints<AppUser>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthorization();

// --- Transactional email ----------------------------------------------------------
// Identity resolves IEmailSender<AppUser> to send the confirmation / reset links.
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IEmailSender<AppUser>, SmtpEmailSender>();

// --- CORS -------------------------------------------------------------------------
// The WASM client is served from a different origin in hybrid dev, so it needs CORS.
// Bearer tokens (not cookies) carry the credential, so AllowCredentials is unnecessary.
var clientOrigins = builder.Configuration["ClientOrigin"]?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? ["http://localhost:5431"];
builder.Services.AddCors(o => o.AddPolicy("client", p => p
    .WithOrigins(clientOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// --- OpenAPI ----------------------------------------------------------------------
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations + seed sample data on startup in Development so the app is usable
// immediately after `docker compose up` / `dotnet run`. In production you would run
// `dotnet ef database update` as a deploy step instead of migrating at boot.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(db);
}

app.UseCors("client");

// OpenAPI document + Scalar interactive reference UI at /scalar.
app.MapOpenApi();
app.MapScalarApiReference(o => o.WithTitle("MyApp API"));

app.UseAuthentication();
app.UseAuthorization();

// All Identity endpoints live under /api/auth (login, register, refresh, confirmEmail,
// manage/info, …). Grouping them keeps the client + nginx proxy config to a single
// /api/ prefix, and the emailed confirmation link is generated within this group.
app.MapGroup("/api/auth")
   .WithTags("Auth")
   .MapIdentityApi<AppUser>();

// Liveness probe (used by docker-compose healthchecks and load balancers).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithTags("Health");

// Sample protected data the dashboard consumes. Requires a valid bearer token.
app.MapGet("/api/orders", async (AppDbContext db) =>
        await db.Orders.OrderByDescending(o => o.Date).ToListAsync())
   .RequireAuthorization()
   .WithTags("Orders");

app.Run();
