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

    /// <summary>
    /// Extended overload that opts a shortcut in to firing while focus is inside an
    /// editable element.
    /// </summary>
    /// <param name="allowInEditable">
    /// When <c>false</c> the shortcut does NOT fire while focus is inside an editable
    /// element (<c>input</c>/<c>textarea</c>/<c>select</c>/<c>contenteditable</c>), so a
    /// combo like <c>ctrl+b</c> yields to the browser's native bold instead of stealing
    /// it. Set <c>true</c> for global shortcuts that must fire everywhere, e.g. a
    /// <c>ctrl+k</c> command palette.
    /// </param>
    /// <remarks>
    /// Additive default interface member (mirrors <c>IComponentInteropService</c>'s
    /// additive DIM overloads): the default body delegates to the 3-parameter member —
    /// i.e. behaves as <c>allowInEditable: false</c> — so existing external implementors
    /// and test doubles keep compiling unchanged. Concrete services reimplement this to
    /// honor the flag.
    /// </remarks>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Func<Task> handler, bool preventDefault, bool allowInEditable)
        => RegisterAsync(keyCombo, handler, preventDefault);

    /// <summary>
    /// Extended overload for a synchronous handler that opts in to firing while focus is
    /// inside an editable element. See the async overload.
    /// </summary>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Action handler, bool preventDefault, bool allowInEditable)
        => RegisterAsync(keyCombo, handler, preventDefault);

    ValueTask UnregisterAsync(string id);
}
