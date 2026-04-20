namespace Lumeo.Services;

/// <summary>
/// Provides keyboard shortcut registration and removal.
/// Inject this interface in consumers to enable mocking in tests.
/// </summary>
public interface IKeyboardShortcutService : IAsyncDisposable
{
    /// <summary>
    /// Register a keyboard shortcut with an async handler.
    /// Key combo format: "ctrl+k", "ctrl+shift+p", "escape", "alt+n".
    /// </summary>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Func<Task> handler, bool preventDefault = true);

    /// <summary>
    /// Register a keyboard shortcut with a synchronous handler.
    /// </summary>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Action handler, bool preventDefault = true);

    ValueTask UnregisterAsync(string id);
}
