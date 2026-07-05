using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace MyApp.Auth;

/// <summary>
/// An <see cref="AuthenticationStateProvider"/> backed by the real ASP.NET Core Identity
/// API. Sign-in exchanges credentials for a bearer <c>accessToken</c> + <c>refreshToken</c>
/// (persisted in <c>localStorage</c>); the token is attached to the shared
/// <see cref="HttpClient"/> and the signed-in identity is resolved by calling
/// <c>GET /api/auth/manage/info</c>. When the access token expires the provider silently
/// refreshes once before falling back to anonymous.
/// </summary>
public sealed class ApiAuthenticationStateProvider(HttpClient http, IJSRuntime js) : AuthenticationStateProvider
{
    private const string AccessKey = "myapp.auth.access";
    private const string RefreshKey = "myapp.auth.refresh";

    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private string? _refreshToken;
    private bool _loaded;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await EnsureTokenLoadedAsync();

        if (http.DefaultRequestHeaders.Authorization is null)
            return Anonymous;

        var info = await FetchInfoAsync();
        if (info is null)
            return Anonymous;

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, info.Email), new Claim(ClaimTypes.Email, info.Email)],
            authenticationType: "Identity.Bearer");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>Signs in via <c>/api/auth/login</c>, distinguishing an unconfirmed email from bad credentials.</summary>
    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        using var resp = await http.PostAsJsonAsync("api/auth/login", new Credentials(email, password));

        if (resp.IsSuccessStatusCode)
        {
            var tokens = await resp.Content.ReadFromJsonAsync<AccessTokenResponse>();
            if (tokens is null)
                return AuthResult.Fail("Unexpected response from the server.");

            await StoreTokensAsync(tokens);
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            return AuthResult.Ok();
        }

        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            var detail = await ReadProblemDetailAsync(resp);
            return string.Equals(detail, "NotAllowed", StringComparison.OrdinalIgnoreCase)
                ? AuthResult.NotConfirmed()
                : AuthResult.Fail("Invalid email or password.");
        }

        return AuthResult.Fail("Sign-in failed. Please try again.");
    }

    /// <summary>Registers via <c>/api/auth/register</c>; the API emails a confirmation link.</summary>
    public async Task<AuthResult> RegisterAsync(string email, string password)
    {
        using var resp = await http.PostAsJsonAsync("api/auth/register", new Credentials(email, password));
        if (resp.IsSuccessStatusCode)
            return AuthResult.Ok();

        return AuthResult.Fail(await ReadFirstValidationErrorAsync(resp) ?? "Registration failed. Please try again.");
    }

    public async Task LogoutAsync()
    {
        _refreshToken = null;
        http.DefaultRequestHeaders.Authorization = null;
        await js.InvokeVoidAsync("localStorage.removeItem", AccessKey);
        await js.InvokeVoidAsync("localStorage.removeItem", RefreshKey);
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    // --- internals ---------------------------------------------------------------

    private async Task<UserInfo?> FetchInfoAsync(bool allowRefresh = true)
    {
        using var resp = await http.GetAsync("api/auth/manage/info");
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<UserInfo>();

        if (resp.StatusCode == HttpStatusCode.Unauthorized && allowRefresh && await TryRefreshAsync())
            return await FetchInfoAsync(allowRefresh: false);

        return null;
    }

    private async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken))
            return false;

        using var resp = await http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(_refreshToken));
        if (!resp.IsSuccessStatusCode)
        {
            await LogoutAsync();
            return false;
        }

        var tokens = await resp.Content.ReadFromJsonAsync<AccessTokenResponse>();
        if (tokens is null)
            return false;

        await StoreTokensAsync(tokens);
        return true;
    }

    private async Task EnsureTokenLoadedAsync()
    {
        if (_loaded)
            return;
        _loaded = true;

        var access = await js.InvokeAsync<string?>("localStorage.getItem", AccessKey);
        _refreshToken = await js.InvokeAsync<string?>("localStorage.getItem", RefreshKey);
        if (!string.IsNullOrEmpty(access))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
    }

    private async Task StoreTokensAsync(AccessTokenResponse tokens)
    {
        _refreshToken = tokens.RefreshToken;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        await js.InvokeVoidAsync("localStorage.setItem", AccessKey, tokens.AccessToken);
        await js.InvokeVoidAsync("localStorage.setItem", RefreshKey, tokens.RefreshToken);
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage resp)
    {
        try
        {
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
            return doc.TryGetProperty("detail", out var d) ? d.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string?> ReadFirstValidationErrorAsync(HttpResponseMessage resp)
    {
        try
        {
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (doc.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                foreach (var field in errors.EnumerateObject())
                    foreach (var msg in field.Value.EnumerateArray())
                        return msg.GetString();

            return doc.TryGetProperty("detail", out var d) ? d.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
