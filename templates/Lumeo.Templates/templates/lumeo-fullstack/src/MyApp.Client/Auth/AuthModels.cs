namespace MyApp.Auth;

/// <summary>Payload for <c>POST /api/auth/login</c> and <c>/register</c>.</summary>
public sealed record Credentials(string Email, string Password);

/// <summary>Body of <c>POST /api/auth/refresh</c>.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>The token bundle returned by <c>/login</c> and <c>/refresh</c>.</summary>
public sealed record AccessTokenResponse(string TokenType, string AccessToken, long ExpiresIn, string RefreshToken);

/// <summary>Shape of <c>GET /api/auth/manage/info</c> — enough to identify the signed-in user.</summary>
public sealed record UserInfo(string Email, bool IsEmailConfirmed);

/// <summary>
/// Result of a sign-in attempt, so the UI can distinguish "wrong credentials" from
/// "email not confirmed yet" (a 401 whose problem detail is <c>NotAllowed</c>).
/// </summary>
public sealed record AuthResult(bool Succeeded, string? Error = null, bool EmailNotConfirmed = false)
{
    public static AuthResult Ok() => new(true);
    public static AuthResult Fail(string error) => new(false, error);
    public static AuthResult NotConfirmed() => new(false, "Please confirm your email address first — check your inbox for the confirmation link.", true);
}
