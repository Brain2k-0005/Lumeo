using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.RichTextEditor;

/// <summary>
/// Regression tests for the two "state-on-data-change" battle-test findings on the
/// RichTextEditor (triage #59 and #60):
///
///   #59 — An unrelated parent re-render that re-supplies a stale one-way <c>Value</c>
///         must NOT push <c>rte.setContent</c> and wipe the user's in-progress edits.
///         The live document is tracked separately from the last value the PARENT
///         supplied, so only a real parent-driven Value change re-syncs content.
///
///   #60 — Changing the <c>Triggers</c> parameter after init must rebuild the editor's
///         suggestion extensions (they are registered at JS construction time), so an
///         added activation char fires and a removed one stops firing. The component
///         detects a char-set change and re-inits the JS instance.
///
/// These mirror <see cref="RichTextEditorBehaviorTests"/>: the JS module is mocked in
/// Loose mode and <c>rte.init</c> is stubbed to return a non-empty instance id so the
/// component flips to its initialized state and the interop path actually runs.
/// </summary>
public class RichTextEditorStateOnDataChangeTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Editor/js/rich-text-editor.js";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();

        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;

        // rte.init returns the instance id used by every subsequent command — a
        // non-empty string flips the component's _initialized flag on.
        _module.Setup<string>("rte.init", _ => true).SetResult("rte-instance-1");

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static L.EditorTrigger Trigger(char c) =>
        new(c, _ => new ValueTask<IReadOnlyList<L.TriggerItem>>(Array.Empty<L.TriggerItem>()));

    // ----------------------------------------------------------------- #59

    [Fact]
    public async Task StaleOneWayValue_reSupplied_doesNotClobber_inProgressEdit()
    {
        // One-way binding: the parent passes Value but does NOT write ValueChanged back
        // into its own field, so its Value stays at the original literal across renders.
        const string parentValue = "<p>initial</p>";
        var cut = _ctx.Render<L.RichTextEditor>(p => p.Add(c => c.Value, parentValue));

        // User types — JS reports the edit back through OnContentUpdate. This becomes the
        // live document; it is NOT written back into the [Parameter] Value.
        await cut.InvokeAsync(() => cut.Instance.OnContentUpdate("<p>edited</p>"));

        var setContentBefore = _module.Invocations.Count(i => i.Identifier == "rte.setContent");

        // Parent re-renders for an unrelated reason and re-supplies its STILL-STALE Value
        // (one-way binding never captured the edit). This must not re-sync content.
        cut.Render(p => p.Add(c => c.Value, parentValue));

        var setContentAfter = _module.Invocations.Count(i => i.Identifier == "rte.setContent");

        // The bug: OnParametersSetAsync compared the stale Value against the live edit and
        // pushed rte.setContent("<p>initial</p>"), wiping the edit. The fix compares the
        // stale Value against the LAST PARENT-SUPPLIED value (unchanged) -> no setContent.
        Assert.Equal(setContentBefore, setContentAfter);
        Assert.DoesNotContain(
            _module.Invocations,
            i => i.Identifier == "rte.setContent" && i.Arguments.Contains(parentValue));
    }

    [Fact]
    public async Task RealParentValueChange_stillReSyncsContent()
    {
        // Guard the controlled path is preserved: when the PARENT genuinely changes Value,
        // the editor must still push the new content to JS.
        var cut = _ctx.Render<L.RichTextEditor>(p => p.Add(c => c.Value, "<p>initial</p>"));

        await cut.InvokeAsync(() => cut.Instance.OnContentUpdate("<p>edited</p>"));

        cut.Render(p => p.Add(c => c.Value, "<p>from-parent</p>"));

        Assert.Contains(
            _module.Invocations,
            i => i.Identifier == "rte.setContent" && i.Arguments.Contains("<p>from-parent</p>"));
    }

    // ----------------------------------------------------------------- #60

    [Fact]
    public void TriggersChange_afterInit_reInitsEditor()
    {
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(c => c.Triggers, new List<L.EditorTrigger> { Trigger('@') }));

        var initsBefore = _module.Invocations.Count(i => i.Identifier == "rte.init");
        Assert.Equal(1, initsBefore);

        // Add a new activation char ('#'). Because suggestion extensions are wired at JS
        // construction time, the editor must tear down and re-init to register it.
        cut.Render(p => p.Add(c => c.Triggers,
            new List<L.EditorTrigger> { Trigger('@'), Trigger('#') }));

        // The bug: Triggers was read once on first render and never again, so '#' would
        // never fire. The fix detects the char-set change, destroys, and re-inits.
        Assert.Contains(_module.Invocations, i => i.Identifier == "rte.destroy");
        Assert.Equal(2, _module.Invocations.Count(i => i.Identifier == "rte.init"));
    }

    [Fact]
    public void UnchangedTriggers_acrossReRender_doesNotReInit()
    {
        // A re-render that does not change the trigger char-set must NOT re-init.
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(c => c.Triggers, new List<L.EditorTrigger> { Trigger('@') }));

        cut.Render(p => p.Add(c => c.Triggers, new List<L.EditorTrigger> { Trigger('@') }));

        Assert.Equal(1, _module.Invocations.Count(i => i.Identifier == "rte.init"));
        Assert.DoesNotContain(_module.Invocations, i => i.Identifier == "rte.destroy");
    }

    // ----------------------------------------------------------------- Codex P2 — controlled rollback

    /// <summary>
    /// When ValueChanged IS bound and the parent REJECTS an edit by leaving Value at the
    /// prior HTML, the editor must be re-synced to the accepted value. Before the fix,
    /// OnParametersSet compared Value against _lastParamValue (which equalled the accepted
    /// value, unchanged), so the re-sync condition was false and TipTap kept the rejected
    /// edit. The fix tracks _lastPushed (the value WE emitted) instead: after a rejection
    /// Value != _lastPushed, triggering the authoritative setContent rollback.
    /// </summary>
    [Fact]
    public async Task ControlledValue_rejectedByParent_rollsBackEditorContent()
    {
        const string accepted = "<p>clean</p>";
        const string rejected = "<p>dirty</p>";

        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(c => c.Value, accepted)
            .Add(c => c.ValueChanged, (string? _) => { /* parent rejects: never adopts */ }));

        var setContentBefore = _module.Invocations.Count(i => i.Identifier == "rte.setContent");

        // JS editor reports the user's edit via the [JSInvokable] callback.
        await cut.InvokeAsync(() => cut.Instance.OnContentUpdate(rejected));

        // Parent re-renders: ValueChanged was called but parent kept Value = accepted.
        cut.Render(p => p
            .Add(c => c.Value, accepted)
            .Add(c => c.ValueChanged, (string? _) => { }));

        var setContentAfter = _module.Invocations.Count(i => i.Identifier == "rte.setContent");

        // Fix: _lastPushed == rejected != Value (accepted) → authoritative → setContent(accepted).
        Assert.True(setContentAfter > setContentBefore,
            "A rejected edit must trigger rte.setContent to roll TipTap back to the accepted value.");
        Assert.Contains(
            _module.Invocations,
            i => i.Identifier == "rte.setContent" && i.Arguments.Contains(accepted));
    }

    /// <summary>
    /// When ValueChanged IS bound and the parent ACCEPTS an edit (echoes back the same
    /// value we pushed), OnParametersSet must NOT call rte.setContent — that would disturb
    /// the cursor / undo history for no functional reason.
    /// </summary>
    [Fact]
    public async Task ControlledValue_acceptedByParent_doesNotCallSetContent()
    {
        string? parentValue = "<p>initial</p>";

        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(c => c.Value, parentValue)
            .Add(c => c.ValueChanged, (string? html) => parentValue = html));

        await cut.InvokeAsync(() => cut.Instance.OnContentUpdate("<p>typed</p>"));

        var setContentBefore = _module.Invocations.Count(i => i.Identifier == "rte.setContent");

        // Parent accepted the edit: re-renders with Value == the html we emitted.
        cut.Render(p => p
            .Add(c => c.Value, parentValue) // "<p>typed</p>"
            .Add(c => c.ValueChanged, (string? html) => parentValue = html));

        var setContentAfter = _module.Invocations.Count(i => i.Identifier == "rte.setContent");

        Assert.Equal(setContentBefore, setContentAfter);
    }
}
