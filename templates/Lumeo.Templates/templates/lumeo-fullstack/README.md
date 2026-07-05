# MyApp

A **batteries-included full-stack starter** scaffolded with **[Lumeo](https://lumeo.nativ.sh)**:

- **`src/MyApp.Api`** — an ASP.NET Core API with real **ASP.NET Core Identity**
  (`MapIdentityApi`: register / login / refresh / confirm-email / 2FA), **EF Core +
  PostgreSQL** (Npgsql, auto-migrated on startup in Development), a **Scalar** OpenAPI
  reference at `/scalar`, and a real **SMTP email sender** (confirmation links land in
  **MailHog** during development). Email confirmation is **required**, so the sign-up flow
  is real end to end.
- **`src/MyApp.Client`** — the polished Lumeo Blazor WebAssembly shell (collapsible
  sidebar, dark mode, Counter/Weather/Form/Settings pages) whose auth talks to the **real
  Identity endpoints** and whose dashboard renders **live data** from `GET /api/orders`.

---

## Quickstart — everything in Docker

```bash
docker compose up -d --build
```

| What | URL |
|---|---|
| **App** (Blazor client) | http://localhost:8081 |
| **API** (Scalar reference) | http://localhost:8080/scalar |
| **MailHog** (captured email) | http://localhost:8025 |

Then: open the app → **Register** → open **MailHog** → click the confirmation link →
**Sign in** → the dashboard grid loads live orders. (Ports are configurable — see below.)

> In Docker the client is served by **nginx**, which reverse-proxies `/api` to the API
> container, so the browser is **same-origin** (no CORS to configure).

---

## Everyday dev — hybrid mode (recommended)

Run only the infrastructure in Docker and the two apps on the host, for fast hot-reload:

```bash
# 1. Infra only (Postgres + MailHog)
docker compose up -d postgres mailhog

# 2. API  (http://localhost:5433)  — migrates + seeds on first run
dotnet run --project src/MyApp.Api

# 3. Client (http://localhost:5431) — in a second terminal
dotnet run --project src/MyApp.Client
```

Open http://localhost:5431 and run the same register → confirm (via
http://localhost:8025) → sign-in journey.

> In hybrid mode the client and API are on **different origins**, so the API enables
> **CORS** for `http://localhost:5431` (`ClientOrigin` in `appsettings.json`). The client
> reads the API address from `wwwroot/appsettings.Development.json` (`ApiBaseUrl`).

---

## Key design decisions

- **Bearer tokens, not cookies.** `MapIdentityApi` supports both; the WASM client uses the
  **bearer** flow (`/api/auth/login` returns an `accessToken` + `refreshToken`, persisted in
  `localStorage`). Bearer avoids the `SameSite`/`Secure` cross-origin cookie friction that
  plagues local HTTP + multi-origin setups, and the `ApiAuthenticationStateProvider`
  transparently **refreshes** an expired token before falling back to anonymous.
- **Proxy in Docker, CORS in hybrid.** Same-origin via nginx is the simplest reliable
  production shape; CORS is only needed for the two-origin hybrid dev loop.
- **One auth seam.** Everything hangs off a single `AuthenticationStateProvider`
  (`<AuthorizeView>`, `[Authorize]`, `<RedirectToLogin>`). Swap it for OIDC / Auth0 / Entra
  without touching the UI.
- **Identity under `/api/auth`.** All Identity endpoints are grouped under one prefix, so
  the client and the nginx proxy only need to know about `/api`.

---

## Change ports & credentials

Everything is driven by **`.env`** — edit a value and re-run `docker compose up`:

```dotenv
CLIENT_PORT=8081        # app
API_PORT=8080           # API + /scalar
MAILHOG_UI_PORT=8025    # MailHog web UI
POSTGRES_PORT=5432
POSTGRES_USER=myapp
POSTGRES_PASSWORD=myapp_dev_password
POSTGRES_DB=myapp
```

For **hybrid** dev the two app ports live in each project's `Properties/launchSettings.json`
(client `5431`, API `5433`; the API avoids `5432` so it never clashes with Postgres). If you
change the client port, update the API's `ClientOrigin`
(`src/MyApp.Api/appsettings.json`); if you change the API port, update the client's
`ApiBaseUrl` (`src/MyApp.Client/wwwroot/appsettings.Development.json`).

---

## Database migrations

The API **applies migrations on startup in Development** (`db.Database.MigrateAsync()` in
`Program.cs`) and seeds sample orders. To evolve the schema:

```bash
dotnet tool install --global dotnet-ef        # once, matching your SDK
dotnet ef migrations add <Name>  --project src/MyApp.Api
dotnet ef database update        --project src/MyApp.Api   # or just restart in Dev
```

In production, run `dotnet ef database update` as a deploy step instead of migrating at boot.

---

## Project layout

| Path | What it is |
|---|---|
| `src/MyApp.Api/Program.cs` | Host: EF/Npgsql, `AddIdentityApiEndpoints`, CORS, OpenAPI/Scalar, `MapIdentityApi`, `/api/orders`. |
| `src/MyApp.Api/Data/` | `AppUser`, `AppDbContext` (Identity + Orders), `DataSeeder`. |
| `src/MyApp.Api/Email/SmtpEmailSender.cs` | `IEmailSender<AppUser>` → SMTP → MailHog. |
| `src/MyApp.Api/Migrations/` | EF `InitialCreate` (Identity + Orders tables). |
| `src/MyApp.Client/Auth/` | `ApiAuthenticationStateProvider` (bearer + refresh), `RedirectToLogin`. |
| `src/MyApp.Client/Pages/Auth/` | `/login`, `/register` (+ check-inbox), `/forgot-password`. |
| `src/MyApp.Client/Pages/Dashboard.razor` | Live `/api/orders` grid with loading + error states. |
| `docker-compose.yml` · `.env` | Postgres + MailHog + API + nginx client; all config in `.env`. |

## Adding more Lumeo pieces

```bash
dotnet new lumeo-page      --name Reports --route reports
dotnet new lumeo-form      --ModelName Feedback --PageName FeedbackPage --route feedback
dotnet new lumeo-component --ComponentName StatCard --namespace MyApp.Client.Components
```
