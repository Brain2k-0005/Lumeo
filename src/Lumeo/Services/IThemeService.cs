namespace Lumeo.Services;

/// <summary>
/// Layout direction. Mirrors the <c>dir</c> attribute on <c>&lt;html&gt;</c>.
/// </summary>
public enum LayoutDirection
{
    /// <summary>Left-to-right (default, e.g. English, German, Spanish).</summary>
    Ltr,
    /// <summary>Right-to-left (e.g. Arabic, Hebrew, Persian).</summary>
    Rtl,
}

/// <summary>
/// Provides theme mode and scheme management (dark/light/system, color schemes, layout direction).
/// Inject this interface in consumers to enable mocking in tests.
/// </summary>
public interface IThemeService
{
    event Action? OnThemeChanged;

    ThemeMode CurrentMode { get; }
    string CurrentScheme { get; }
    bool IsDark { get; }
    LayoutDirection CurrentDirection { get; }

    Task InitializeAsync();
    Task SetModeAsync(ThemeMode mode);
    Task SetSchemeAsync(string scheme);

    /// <summary>Toggles between the two RESOLVED appearances (dark/light) — see
    /// <see cref="ThemeService.ToggleModeAsync"/> for the full contract.</summary>
    Task ToggleModeAsync();

    /// <summary>Cycles System → Dark → Light → System — see
    /// <see cref="ThemeService.CycleModeAsync"/> for the full contract.</summary>
    Task CycleModeAsync();

    /// <summary>Set the page layout direction (LTR / RTL). Persisted to localStorage.</summary>
    Task SetDirectionAsync(LayoutDirection direction);

    /// <summary>Read the current direction (reads from <c>document.documentElement.dir</c>).</summary>
    Task<LayoutDirection> GetDirectionAsync();
}
