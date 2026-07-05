# MyApp

A Blazor WebAssembly app scaffolded with **[Lumeo](https://lumeo.nativ.sh)**. It boots
styled and dark-mode-ready with zero manual setup:

- `AddLumeo()` is already wired in `Program.cs`.
- The prebuilt `lumeo.css` (OKLCH default theme) + `lumeo-utilities.css` + `theme.js` are
  linked in `wwwroot/index.html` straight from the Lumeo NuGet package — no Tailwind build
  and no asset copying required.
- A collapsible sidebar shell (`Layout/MainLayout.razor`) with a working dark-mode toggle.
- Five pages, including the **Microsoft bridge**: **Counter** and **Weather** are the pages
  you know from the standard Blazor template — rebuilt with Lumeo so you can diff the two
  worlds. Plus **Home** (KPI cards + a `DataGrid`), **Form** (validated `<Form>`), and
  **Settings** (`Tabs` + inputs).

## Run

```bash
dotnet run
```

Then open the printed URL. Toggle light/dark with the button in the top-right (or `Ctrl+B`
to collapse the sidebar).

## Project layout

| Path | What it is |
|---|---|
| `Program.cs` | Host builder — `AddLumeo()` registers all Lumeo services. |
| `wwwroot/index.html` | Links the prebuilt Lumeo CSS/JS shipped in the NuGet package. |
| `Layout/MainLayout.razor` | Sidebar + header shell with the dark-mode toggle. |
| `Pages/Dashboard.razor` | Home — KPI cards + a sortable, paginated `DataGrid`. |
| `Pages/Counter.razor` | The standard Blazor counter, rebuilt with Lumeo (`Button`, `Badge`, `Select`). |
| `Pages/Weather.razor` | The standard Blazor forecast, rebuilt with Lumeo — a sortable `DataGrid` with a loading skeleton and temperature `Badge`s. |
| `Pages/FormPage.razor` | `<Form>` + `<FormField>` with DataAnnotations validation. |
| `Pages/Settings.razor` | `Tabs` with form inputs, a `Select`, and `Switch` rows. |

**Coming from the Microsoft template?** `Counter` and `Weather` are the two pages you already
know — kept intentionally so you can compare vanilla Blazor with the Lumeo equivalent side by
side. The `@code` blocks are unchanged in spirit; only the markup swaps raw HTML for Lumeo
components.

## Authentication

This app was scaffolded with the `--auth` option (`dotnet new lumeo-app --auth <value>`):

| `--auth` | What you get |
|---|---|
| `demo` *(default)* | A full auth UI — **`/login`**, **`/register`**, **`/forgot-password`** built from Lumeo's auth blocks in a full-screen `AuthLayout` — backed by an in-browser **localStorage** provider. The whole app is protected; the sidebar footer shows the signed-in user with a dropdown (Profile → Settings, Sign out). No backend required. |
| `none` | No authentication. Public pages and a static sidebar user card. |
| `oidc` | Real OIDC/OAuth wiring via `Microsoft.AspNetCore.Components.WebAssembly.Authentication` and `RemoteAuthenticatorView`, inside the same Lumeo auth layout. Configure your authority in `wwwroot/appsettings.json` (login fails until you do — that's expected). |

**How protection is wired (demo & oidc):** `_Imports.razor` applies `@attribute [Authorize]`
to every page, so anonymous visitors hit `<AuthorizeRouteView>`'s `NotAuthorized` and
`RedirectToLogin` bounces them to the sign-in screen. The auth pages opt back out with
`@attribute [AllowAnonymous]`. The whole thing hangs off `<CascadingAuthenticationState>` in
`App.razor` and a single `AuthenticationStateProvider`.

### Demo auth — swap the provider

The demo variant is deliberately backend-free so you can click through the whole flow
immediately. **The one seam you replace is `Auth/DemoAuthenticationStateProvider.cs`** — an
`AuthenticationStateProvider` that reads/writes a user in `localStorage` (any email + any
password ≥ 6 chars signs in; register stores the display name; sign-out clears it). It's
registered in `Program.cs`:

```csharp
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<DemoAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<DemoAuthenticationStateProvider>());
```

Everything else in the app depends only on the Blazor authorization abstractions
(`<AuthorizeView>`, `[Authorize]`, `<AuthorizeRouteView>`, `<RedirectToLogin>`), so swapping the
provider leaves the UI untouched. Real options:

- **OIDC / OAuth** — re-scaffold with `--auth oidc`, or add
  `Microsoft.AspNetCore.Components.WebAssembly.Authentication` and call `AddOidcAuthentication`.
- **ASP.NET Core Identity API** — call your `/identity` endpoints from a custom provider.
- **Auth0 / Microsoft Entra ID / any hosted IdP** — point the OIDC options at their authority.

The `/login`, `/register` and `/forgot-password` pages are yours to keep or replace — they're
plain Lumeo forms in `Pages/Auth/`.

## Add more Lumeo pieces

Scaffold additional pages, forms, and components with the item templates:

```bash
dotnet new lumeo-page      --name Reports --route reports
dotnet new lumeo-form      --ModelName Feedback --PageName FeedbackPage --route feedback
dotnet new lumeo-component --ComponentName StatCard --namespace MyApp.Components
```

## Three ways to own Lumeo

This starter uses **path 1** (NuGet). You can switch at any time:

1. **NuGet package (default).** `Lumeo` + `Lumeo.DataGrid` are referenced in `MyApp.csproj`.
   Upgrade by bumping the version. You consume the components as a library.

2. **Vendor the source with the CLI.** Install the tool and copy individual components into
   your project as editable source (shadcn-style), while the shared runtime stays in the
   NuGet package:

   ```bash
   dotnet tool install -g Lumeo.Cli
   lumeo init
   lumeo add button card
   ```

3. **Eject to fully NuGet-free.** Vendor the whole Lumeo runtime as source and drop the
   package references entirely — you own 100% of the code:

   ```bash
   lumeo eject
   ```

See the [Lumeo docs](https://lumeo.nativ.sh/docs/introduction) for the full guide.
