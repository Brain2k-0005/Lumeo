using Microsoft.AspNetCore.Identity;

namespace MyApp.Api.Data;

/// <summary>
/// The application user. Extends <see cref="IdentityUser"/> so you can add your own
/// profile columns (display name, avatar, …) without touching the Identity plumbing.
/// </summary>
public sealed class AppUser : IdentityUser
{
    /// <summary>Optional friendly name shown in the UI. Populated at registration time is left to you.</summary>
    public string? DisplayName { get; set; }
}
