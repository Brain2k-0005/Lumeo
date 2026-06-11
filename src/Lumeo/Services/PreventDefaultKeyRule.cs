namespace Lumeo.Services;

/// <summary>
/// One key rule for <see cref="IComponentInteropService.RegisterPreventDefaultKeys"/>.
/// The browser default is suppressed only when every set condition matches.
/// Property names serialize camelCased across the JS interop boundary.
/// </summary>
/// <param name="Key">The <c>KeyboardEvent.key</c> value, e.g. "Enter" or "ArrowLeft".</param>
/// <param name="RequireNoModifiers">Only match while Shift/Ctrl/Alt/Meta are all up —
/// lets Shift+Enter keep inserting a newline while plain Enter submits.</param>
/// <param name="SkipComposing">Never suppress while an IME composition is active
/// (<c>isComposing</c> / keyCode 229) so confirming a CJK composition isn't swallowed.</param>
/// <param name="SkipEditable">Never suppress when the event target sits inside an
/// input, textarea, select or contenteditable — keeps caret/typing keys alive in
/// interactive content nested under the registered element.</param>
public sealed record PreventDefaultKeyRule(
    string Key,
    bool RequireNoModifiers = false,
    bool SkipComposing = false,
    bool SkipEditable = false);
