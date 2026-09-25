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

    /// <summary>
    /// Toggles between the two RESOLVED appearances: if the theme is currently showing dark
    /// (whether because <see cref="CurrentMode"/> is <see cref="ThemeMode.Dark"/>, or
    /// <see cref="ThemeMode.System"/> resolved to dark via the OS preference) this switches to
    /// <see cref="ThemeMode.Light"/>; otherwise it switches to <see cref="ThemeMode.Dark"/>.
    /// A single click always flips the visible appearance — previously (rc.43) this cycled
    /// System → Dark → Light → System off <see cref="CurrentMode"/> alone, so a System→Dark
    /// click on a dark OS was visually a no-op and a light/dark switch needed two clicks
    /// (LU-22). Use <see cref="CycleModeAsync"/> for a control that intends to walk the full
    /// three-way cycle and offer System as one of its stops.
    /// </summary>
    public async Task ToggleModeAsync()
    {
        var next = IsDark ? ThemeMode.Light : ThemeMode.Dark;
        await SetModeAsync(next);
    }

    /// <summary>
    /// Cycles through all three modes in a fixed order: System → Dark → Light → System, based
    /// on <see cref="CurrentMode"/> (not the resolved appearance). Unlike
    /// <see cref="ToggleModeAsync"/>, which only flips between the two resolved appearances,
    /// this always visits System mode, so a control that lets the user get back to "follow the
    /// OS" through repeated clicks (e.g. <c>ThemeToggle</c> with <c>IncludeSystem="true"</c>)
    /// should call this instead of <see cref="ToggleModeAsync"/>.
    /// </summary>
    public async Task CycleModeAsync()
    {
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
