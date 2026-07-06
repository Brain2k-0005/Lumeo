using Lumeo.Services;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// Test double for <see cref="IKeyboardShortcutService"/> that records each
/// registration (combo + handler) so tests can assert a component wired a
/// shortcut and can invoke the captured handler to simulate the key press —
/// without a real DOM/JS keydown. Unregistration removes the entry.
/// </summary>
public sealed class TrackingKeyboardShortcutService : IKeyboardShortcutService
{
    private readonly Dictionary<string, (string Combo, Func<Task> Handler)> _byId = new();

    public IReadOnlyCollection<string> RegisteredCombos =>
        _byId.Values.Select(v => v.Combo).ToList();

    public int RegistrationCount => _byId.Count;

    /// <summary>Invokes the handler registered for the given (normalized-lower)
    /// combo, simulating a key press. Returns false when no such combo is wired.</summary>
    public async Task<bool> TriggerAsync(string combo)
    {
        var entry = _byId.Values.FirstOrDefault(v => string.Equals(v.Combo, combo, StringComparison.OrdinalIgnoreCase));
        if (entry.Handler is null) return false;
        await entry.Handler();
        return true;
    }

    // Implements only the ORIGINAL 3-parameter interface members — the additive
    // allowInEditable overloads are default interface members, so a test double written
    // against the pre-wave shape keeps compiling and routes through here (flag dropped).
    public ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Func<Task> handler, bool preventDefault = true)
    {
        var id = Guid.NewGuid().ToString("N");
        _byId[id] = (keyCombo, handler);
        return ValueTask.FromResult<IAsyncDisposable>(new Handle(this, id));
    }

    public ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Action handler, bool preventDefault = true)
        => RegisterAsync(keyCombo, () => { handler(); return Task.CompletedTask; }, preventDefault);

    public ValueTask UnregisterAsync(string id)
    {
        _byId.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _byId.Clear();
        return ValueTask.CompletedTask;
    }

    private sealed class Handle(TrackingKeyboardShortcutService svc, string id) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => svc.UnregisterAsync(id);
    }
}
