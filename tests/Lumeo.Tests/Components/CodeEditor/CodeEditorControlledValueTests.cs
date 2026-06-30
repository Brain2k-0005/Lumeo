using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.CodeEditor;

/// <summary>
/// #25 (battle-wave2, state-on-data-change) — a controlled / normalizing parent
/// (or plain uncontrolled use) must not have the user's in-progress edit reverted
/// and the caret snapped to position 0 on an unrelated re-render.
///
/// Mechanism: CodeEditor used to push <c>Value</c> back into CodeMirror whenever
/// the editor's own reported value (<c>_lastSyncedValue</c>, mutated by
/// <c>OnEditorChange</c>) diverged from the <c>Value</c> [Parameter]. In
/// uncontrolled use the parameter stays at its old literal, so the next unrelated
/// re-render saw the divergence and called the JS <c>setValue</c> — a full-doc
/// replace that reverts the typed text and resets the caret.
///
/// The fix tracks the last value the PARENT actually supplied and only pushes
/// <c>setValue</c> when the parameter genuinely changes, so an echo-back / same
/// value re-render leaves the editor (and its caret) untouched. The companion JS
/// fix in code-editor.js additionally preserves the caret when a real change is
/// pushed, but that lives in JS; here we assert the C# half — the bug's fix — by
/// observing whether a destructive <c>setValue</c> interop call is emitted.
/// </summary>
public class CodeEditorControlledValueTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo.CodeEditor/js/code-editor.js");
        module.Mode = JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task EditorReportedValue_Then_SameValue_Rerender_Does_Not_Push_SetValue()
    {
        var cut = _ctx.Render<L.CodeEditor>(p => p.Add(c => c.Value, "initial"));

        // The editor (CodeMirror, via JS) reports the user typed -> the bound
        // value the parent holds is unchanged here (uncontrolled / not yet
        // echoed). This mutates the internal _lastSyncedValue.
        await cut.InvokeAsync(() => cut.Instance.OnEditorChange("initialX"));

        var setValueCallsBefore = _ctx.JSInterop.Invocations.Count(i => i.Identifier == "setValue");

        // An unrelated parent re-render (changes Class, NOT Value). Without the
        // fix, OnAfterRenderAsync sees _lastSyncedValue ("initialX") != Value
        // ("initial") and pushes a destructive setValue("initial"), reverting the
        // user's typing and snapping the caret. With the fix, the Value parameter
        // did not change, so no setValue is emitted.
        cut.Render(p => p
            .Add(c => c.Value, "initial")
            .Add(c => c.Class, "changed"));

        var setValueCallsAfter = _ctx.JSInterop.Invocations.Count(i => i.Identifier == "setValue");
        Assert.Equal(setValueCallsBefore, setValueCallsAfter);
    }

    [Fact]
    public void Genuine_Parent_Value_Change_Still_Pushes_SetValue()
    {
        // Guards against the fix over-correcting: when the PARENT genuinely
        // changes Value, the editor must still receive the new text via setValue.
        var cut = _ctx.Render<L.CodeEditor>(p => p.Add(c => c.Value, "initial"));

        cut.Render(p => p.Add(c => c.Value, "totally new doc"));

        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "setValue"
                 && i.Arguments.Count >= 2
                 && (string?)i.Arguments[1] == "totally new doc");
    }
}
