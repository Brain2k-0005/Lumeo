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
    /// <param name="allowInEditable">
    /// When <c>false</c> (default) the shortcut does NOT fire while focus is inside an
    /// editable element (<c>input</c>/<c>textarea</c>/<c>select</c>/<c>contenteditable</c>),
    /// so a combo like <c>ctrl+b</c> yields to the browser's native bold instead of
    /// stealing it. Set <c>true</c> for global shortcuts that must fire everywhere,
    /// e.g. a <c>ctrl+k</c> command palette.
    /// </param>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Func<Task> handler, bool preventDefault = true, bool allowInEditable = false);

    /// <summary>
    /// Register a keyboard shortcut with a synchronous handler.
    /// </summary>
    /// <param name="allowInEditable">See the async overload.</param>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Action handler, bool preventDefault = true, bool allowInEditable = false);

    ValueTask UnregisterAsync(string id);
}
