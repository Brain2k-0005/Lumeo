using Microsoft.JSInterop;

namespace Lumeo.Services;

public sealed class ThemeService
{
    private readonly IJSRuntime _jsRuntime;

    public event Action? OnThemeChanged;
    public ThemeMode CurrentMode { get; private set; } = ThemeMode.System;
    public string CurrentScheme { get; private set; } = "orange";
    public bool IsDark { get; private set; }

    public static readonly IReadOnlyList<ThemeSchemeInfo> AvailableSchemes =
    [
        new("orange", "Orange", "hsl(14 70% 50%)"),
        new("zinc", "Zinc", "hsl(240 5% 26%)"),
        new("blue", "Blue", "hsl(221 83% 53%)"),
        new("green", "Green", "hsl(142 71% 45%)"),
        new("rose", "Rose", "hsl(347 77% 50%)"),
    ];

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        var modeStr = await _jsRuntime.InvokeAsync<string>("themeManager.getMode");
        CurrentMode = modeStr switch
        {
            "dark" => ThemeMode.Dark,
            "light" => ThemeMode.Light,
            _ => ThemeMode.System,
        };

        CurrentScheme = await _jsRuntime.InvokeAsync<string>("themeManager.getScheme");
        IsDark = await _jsRuntime.InvokeAsync<bool>("themeManager.isDark");
    }

    public async Task SetModeAsync(ThemeMode mode)
    {
        CurrentMode = mode;
        var modeStr = mode switch
        {
            ThemeMode.Dark => "dark",
            ThemeMode.Light => "light",
            _ => "system",
        };
        await _jsRuntime.InvokeVoidAsync("themeManager.setMode", modeStr);
        IsDark = await _jsRuntime.InvokeAsync<bool>("themeManager.isDark");
        OnThemeChanged?.Invoke();
    }

    public async Task SetSchemeAsync(string scheme)
    {
        CurrentScheme = scheme;
        await _jsRuntime.InvokeVoidAsync("themeManager.setScheme", scheme);
        OnThemeChanged?.Invoke();
    }

    public async Task ToggleModeAsync()
    {
        await _jsRuntime.InvokeVoidAsync("themeManager.toggle");
        IsDark = await _jsRuntime.InvokeAsync<bool>("themeManager.isDark");
        CurrentMode = IsDark ? ThemeMode.Dark : ThemeMode.Light;
        OnThemeChanged?.Invoke();
    }
}

public enum ThemeMode { System, Light, Dark }

public record ThemeSchemeInfo(string Id, string DisplayName, string PreviewColor);
