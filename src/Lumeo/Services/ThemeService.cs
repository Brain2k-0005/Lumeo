using Microsoft.JSInterop;

namespace Lumeo.Services;

public sealed class ThemeService : IThemeService, IAsyncDisposable, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<ThemeService>? _selfRef;
    private bool _listenerRegistered;

    public event Action? OnThemeChanged;
    public ThemeMode CurrentMode { get; private set; } = ThemeMode.System;
    public string CurrentScheme { get; private set; } = "zinc";
    public bool IsDark { get; private set; }
    public LayoutDirection CurrentDirection { get; private set; } = LayoutDirection.Ltr;

    public static readonly IReadOnlyList<ThemeSchemeInfo> AvailableSchemes =
    [
        new("zinc", "Zinc", "hsl(240 5.9% 10%)"),
        new("blue", "Blue", "hsl(221 83% 53%)"),
        new("green", "Green", "hsl(142 71% 45%)"),
        new("rose", "Rose", "hsl(347 77% 50%)"),
        new("orange", "Orange", "hsl(14 70% 50%)"),
        new("violet", "Violet", "hsl(263 70% 50%)"),
        new("amber", "Amber", "hsl(38 92% 50%)"),
        new("teal", "Teal", "hsl(173 80% 40%)"),
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
        var dir = await _jsRuntime.InvokeAsync<string>("themeManager.getDirection");
        CurrentDirection = dir == "rtl" ? LayoutDirection.Rtl : LayoutDirection.Ltr;

        await EnsureListenerRegisteredAsync();
    }

    // Subscribe (once) to OS prefers-color-scheme flips + cross-tab storage
    // events on the JS side. The JS calls OnExternalThemeChange back, which
    // re-reads state and raises OnThemeChanged so System mode live-updates with
    // the OS and theme choices sync across tabs (#312/#313). Registered from
    // InitializeAsync — already invoked by ThemeSwitcher/ThemeToggle on first
    // render — so no new public API is needed.
    private async Task EnsureListenerRegisteredAsync()
    {
        if (_listenerRegistered) return;
        _listenerRegistered = true;
        _selfRef ??= DotNetObjectReference.Create(this);
        try
        {
            await _jsRuntime.InvokeVoidAsync("themeManager.registerThemeListener", _selfRef);
        }
        catch (JSDisconnectedException) { }
        catch (JSException) { /* older host without the listener API — ignore */ }
    }

    /// <summary>
    /// Invoked from JS when the OS color scheme changes (in System mode) or
    /// another tab updates the theme. Re-reads the live state and raises
    /// <see cref="OnThemeChanged"/> so subscribed components repaint.
    /// </summary>
    [JSInvokable]
    public async Task OnExternalThemeChange()
    {
        try
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
            var dir = await _jsRuntime.InvokeAsync<string>("themeManager.getDirection");
            CurrentDirection = dir == "rtl" ? LayoutDirection.Rtl : LayoutDirection.Ltr;
        }
        catch (JSDisconnectedException) { }
        OnThemeChanged?.Invoke();
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
        // Bug G — the old toggle called themeManager.toggle() (a JS boolean flip) and mapped the result
        // back to Dark|Light only, permanently losing System mode. The fix cycles System→Dark→Light→System
        // in C# so the caller never escapes the three-way cycle, and calls SetModeAsync to keep JS in sync.
        var next = CurrentMode switch
        {
            ThemeMode.System => ThemeMode.Dark,
            ThemeMode.Dark   => ThemeMode.Light,
            _                => ThemeMode.System,
        };
        await SetModeAsync(next);
    }

    public async Task SetDirectionAsync(LayoutDirection direction)
    {
        CurrentDirection = direction;
        var value = direction == LayoutDirection.Rtl ? "rtl" : "ltr";
        await _jsRuntime.InvokeVoidAsync("themeManager.setDirection", value);
        OnThemeChanged?.Invoke();
    }

    public async Task<LayoutDirection> GetDirectionAsync()
    {
        var dir = await _jsRuntime.InvokeAsync<string>("themeManager.getDirection");
        CurrentDirection = dir == "rtl" ? LayoutDirection.Rtl : LayoutDirection.Ltr;
        return CurrentDirection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_listenerRegistered && _selfRef is not null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("themeManager.unregisterThemeListener", _selfRef);
            }
            catch (JSDisconnectedException) { }
            catch (JSException) { }
        }
        _selfRef?.Dispose();
        _selfRef = null;
    }

    // Synchronous Dispose so DI containers that tear down synchronously (e.g.
    // bUnit's BunitContext.Dispose) can release this scoped service without
    // throwing "type only implements IAsyncDisposable". We fire-and-forget the
    // JS unregister (detached) — there's no caller to await — and drop the ref.
    public void Dispose()
    {
        if (_listenerRegistered && _selfRef is not null)
        {
            _ = UnregisterListenerDetachedAsync();
        }
        else
        {
            _selfRef?.Dispose();
            _selfRef = null;
        }
    }

    private async Task UnregisterListenerDetachedAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("themeManager.unregisterThemeListener", _selfRef!);
        }
        catch (JSDisconnectedException) { }
        catch (JSException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            _selfRef?.Dispose();
            _selfRef = null;
        }
    }
}

public enum ThemeMode { System, Light, Dark }

public record ThemeSchemeInfo(string Id, string DisplayName, string PreviewColor);
