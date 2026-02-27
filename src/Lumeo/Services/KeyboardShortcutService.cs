using Microsoft.JSInterop;

namespace Lumeo.Services;

public sealed class KeyboardShortcutService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private DotNetObjectReference<KeyboardShortcutService>? _selfRef;
    private readonly Dictionary<string, ShortcutRegistration> _shortcuts = new();
    private bool _initialized;

    public KeyboardShortcutService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Lumeo/js/components.js");
        return _module;
    }

    private DotNetObjectReference<KeyboardShortcutService> GetSelfRef()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        return _selfRef;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerKeyboardShortcuts", GetSelfRef());
        _initialized = true;
    }

    /// <summary>
    /// Register a keyboard shortcut. Key combo format: "ctrl+k", "ctrl+shift+p", "escape", "alt+n"
    /// Modifiers: ctrl, shift, alt, meta. Separate with +. Key names use KeyboardEvent.key values.
    /// </summary>
    public async ValueTask<IDisposable> RegisterAsync(string keyCombo, Func<Task> handler, bool preventDefault = true)
    {
        await EnsureInitializedAsync();
        var id = Guid.NewGuid().ToString("N");
        var normalized = NormalizeCombo(keyCombo);
        _shortcuts[id] = new ShortcutRegistration(normalized, handler, preventDefault);

        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("addShortcut", id, normalized, preventDefault);

        return new ShortcutHandle(this, id);
    }

    /// <summary>
    /// Register a keyboard shortcut with a synchronous handler.
    /// </summary>
    public async ValueTask<IDisposable> RegisterAsync(string keyCombo, Action handler, bool preventDefault = true)
    {
        return await RegisterAsync(keyCombo, () => { handler(); return Task.CompletedTask; }, preventDefault);
    }

    public async ValueTask UnregisterAsync(string id)
    {
        if (_shortcuts.Remove(id) && _module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("removeShortcut", id);
            }
            catch (JSDisconnectedException) { }
        }
    }

    [JSInvokable]
    public async Task OnShortcutTriggered(string id)
    {
        if (_shortcuts.TryGetValue(id, out var registration))
        {
            await registration.Handler();
        }
    }

    private static string NormalizeCombo(string combo)
    {
        var parts = combo.ToLowerInvariant().Split('+').Select(p => p.Trim()).OrderBy(p => p switch
        {
            "ctrl" => 0,
            "alt" => 1,
            "shift" => 2,
            "meta" => 3,
            _ => 4
        }).ToArray();
        return string.Join("+", parts);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unregisterKeyboardShortcuts");
            }
            catch (JSDisconnectedException) { }

            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }

        _selfRef?.Dispose();
        _shortcuts.Clear();
    }

    private record ShortcutRegistration(string NormalizedCombo, Func<Task> Handler, bool PreventDefault);

    private sealed class ShortcutHandle(KeyboardShortcutService service, string id) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ = service.UnregisterAsync(id);
        }
    }
}
