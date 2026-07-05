using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace MyApp.Auth;

/// <summary>
/// A demo <see cref="AuthenticationStateProvider"/> backed entirely by the browser's
/// <c>localStorage</c> — there is NO backend. The demo sign-in rule is deliberately trivial:
/// any email plus any password of six characters or more signs in; registration also stores
/// the display name; sign-out clears the stored user.
/// </summary>
/// <remarks>
/// <para><b>This class is the swap seam.</b> Everything else in the app depends only on the
/// Blazor authorization abstractions — <c>&lt;AuthorizeView&gt;</c>, the <c>[Authorize]</c>
/// attribute, <c>&lt;AuthorizeRouteView&gt;</c> and <c>&lt;RedirectToLogin&gt;</c>. Replace
/// this provider (and its DI registration in <c>Program.cs</c>) with a real one and none of
/// that has to change. See the README section "Demo auth — swap the provider" for pointers to
/// the OIDC option, an ASP.NET Core Identity API, and hosted providers such as Auth0 / Entra.</para>
/// </remarks>
public sealed class DemoAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string StorageKey = "myapp.demo.user";
    private const int MinPasswordLength = 6;

    private readonly IJSRuntime _js;
    private static readonly Task<AuthenticationState> Anonymous =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    public DemoAuthenticationStateProvider(IJSRuntime js) => _js = js;

    /// <summary>The persisted demo user (name + email). Serialized to localStorage as JSON.</summary>
    public sealed record DemoUser(string Name, string Email);

    /// <inheritdoc />
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = await ReadAsync();
        return user is null
            ? await Anonymous
            : new AuthenticationState(BuildPrincipal(user));
    }

    /// <summary>Demo sign-in: any email + any password of six characters or more.</summary>
    public async Task<bool> SignInAsync(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || (password?.Length ?? 0) < MinPasswordLength)
            return false;

        await PersistAsync(new DemoUser(DisplayNameFrom(email), email));
        return true;
    }

    /// <summary>Demo registration: stores the display name alongside the email, then signs in.</summary>
    public async Task<bool> RegisterAsync(string? name, string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || (password?.Length ?? 0) < MinPasswordLength)
            return false;

        var display = string.IsNullOrWhiteSpace(name) ? DisplayNameFrom(email) : name.Trim();
        await PersistAsync(new DemoUser(display, email));
        return true;
    }

    /// <summary>Clears the stored user and notifies the app that the state changed.</summary>
    public async Task SignOutAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        NotifyAuthenticationStateChanged(Anonymous);
    }

    private async Task PersistAsync(DemoUser user)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(user));
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(BuildPrincipal(user))));
    }

    private async Task<DemoUser?> ReadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<DemoUser>(json);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private static ClaimsPrincipal BuildPrincipal(DemoUser user)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
            },
            authenticationType: "Demo");
        return new ClaimsPrincipal(identity);
    }

    private static string DisplayNameFrom(string email)
    {
        var local = email.Split('@')[0];
        return string.IsNullOrWhiteSpace(local) ? email : local;
    }
}
