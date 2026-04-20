namespace Lumeo.Services;

/// <summary>
/// Provides theme mode and scheme management (dark/light/system, color schemes).
/// Inject this interface in consumers to enable mocking in tests.
/// </summary>
public interface IThemeService
{
    event Action? OnThemeChanged;

    ThemeMode CurrentMode { get; }
    string CurrentScheme { get; }
    bool IsDark { get; }

    Task InitializeAsync();
    Task SetModeAsync(ThemeMode mode);
    Task SetSchemeAsync(string scheme);
    Task ToggleModeAsync();
}
