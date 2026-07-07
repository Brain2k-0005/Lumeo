namespace Lumeo.Services;

/// <summary>
/// Provides keyboard shortcut registration and removal.
/// Inject this interface in consumers to enable mocking in tests.
/// </summary>
public interface IKeyboardShortcutService : IAsyncDisposable
{
    // The 3-parameter members carry NO default on preventDefault, exactly like the
    // concrete KeyboardShortcutService. The 4-parameter DIM overloads below own the
    // defaults, so a bare 2-argument call routes UNAMBIGUOUSLY to the 4-parameter
    // overload (identical behavior: preventDefault=true, allowInEditable=false) while a
    // 3-argument positional call still prefers these (all arguments supplied beats a
    // default-substituted 4-parameter candidate). Adding `= true` here instead would make
    // the 2-argument call CS0121-ambiguous against the defaulted 4-parameter overload —
    // the round-9 regression. A default value is NOT part of the CLR signature, so
    // dropping it keeps these members' abstract contract byte-for-byte identical and every
    // round-3 legacy implementor / test double keeps compiling unchanged.

    /// <summary>
    /// Register a keyboard shortcut with an async handler.
    /// Key combo format: "ctrl+k", "ctrl+shift+p", "escape", "alt+n".
    /// </summary>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Func<Task> handler, bool preventDefault);

    /// <summary>
    /// Register a keyboard shortcut with a synchronous handler.
    /// </summary>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Action handler, bool preventDefault);

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
    /// <para>
    /// Both trailing parameters carry defaults so the ergonomic call
    /// <c>RegisterAsync("ctrl+k", handler, allowInEditable: true)</c> compiles against the
    /// INTERFACE (the round-9 finding: a required <c>preventDefault</c> ahead of a named
    /// <c>allowInEditable</c> did not — and C# forbids an optional parameter before a
    /// required one, so <c>allowInEditable</c> must be optional too). Because the
    /// 3-parameter members above deliberately have NO default, a bare 2-argument call binds
    /// unambiguously to THIS overload (defaults fill both trailing params) while a
    /// 3-argument positional call prefers the 3-parameter member (all arguments supplied
    /// beats a default-substituted candidate) — no CS0121.
    /// </para>
    /// </remarks>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Func<Task> handler, bool preventDefault = true, bool allowInEditable = false)
        => RegisterAsync(keyCombo, handler, preventDefault);

    /// <summary>
    /// Extended overload for a synchronous handler that opts in to firing while focus is
    /// inside an editable element. See the async overload.
    /// </summary>
    ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Action handler, bool preventDefault = true, bool allowInEditable = false)
        => RegisterAsync(keyCombo, handler, preventDefault);

    ValueTask UnregisterAsync(string id);
}
