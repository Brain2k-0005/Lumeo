using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InplaceEditor;

/// <summary>
/// Regression coverage for two state-on-data-change bugs (battle-wave2, medium):
///
/// n=37 — SaveOnBlur lost update: while the field is open but UNTOUCHED, a concurrent
/// external Value update used to be silently overwritten on blur because OnBlur always
/// committed the value captured back at StartEditing. The fix is dirty-tracking: an
/// untouched blur leaves edit mode WITHOUT firing ValueChanged/OnSave, and OnParametersSet
/// re-seeds the edit baseline from a fresh Value while the user hasn't typed.
///
/// n=38 — Disabling mid-edit: the render gate is @if (_editing &amp;&amp; !Disabled), so flipping
/// Disabled=true hides the field but used to leave the private _editing flag latched. When
/// Disabled later flipped back to false the stale in-progress edit re-appeared. The fix
/// clears _editing in OnParametersSet when Disabled goes true mid-edit.
///
/// The directly observable internal state is the private _editing field (the render gate)
/// and the ValueChanged callback (the commit). JSInterop is Loose so the focus-on-render
/// FocusAsync calls are harmless no-ops.
/// </summary>
public class InplaceEditorStateOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InplaceEditorStateOnDataChangeTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        module.Mode = JSRuntimeMode.Loose;
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static bool IsEditing(L.InplaceEditor instance)
    {
        var field = typeof(L.InplaceEditor)
            .GetField("_editing", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (bool)field!.GetValue(instance)!;
    }

    // n=37
    [Fact]
    public void Blur_on_untouched_field_does_not_commit_over_a_concurrent_external_Value_update()
    {
        var committed = new List<string?>();
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "original")
            .Add(e => e.ValueChanged, v => committed.Add(v)));

        // Enter edit mode but DO NOT type anything (the field stays untouched).
        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Single(cut.FindAll("input"));

        // Meanwhile the parent pushes a fresh value (e.g. a server/sibling update).
        cut.Render(p => p
            .Add(e => e.Value, "external-update")
            .Add(e => e.ValueChanged, v => committed.Add(v)));

        // The user tabs out -> the input blurs (SaveOnBlur default true, no buttons).
        cut.Find("input").Blur();

        // We left edit mode...
        Assert.Empty(cut.FindAll("input"));
        // ...but an UNTOUCHED blur must NOT have committed anything, so the external
        // update is preserved. Without the fix, OnBlur committed the stale "original"
        // pre-edit value, clobbering "external-update".
        Assert.Empty(committed);
    }

    // n=37 — the control case: a real edit must still commit on blur.
    [Fact]
    public void Blur_on_a_touched_field_still_commits_the_typed_value()
    {
        string? committed = null;
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "original")
            .Add(e => e.ValueChanged, v => committed = v));

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        var input = cut.Find("input");
        input.Input("typed");
        input.Blur();

        Assert.Equal("typed", committed);
    }

    // n=38
    [Fact]
    public void Disabling_mid_edit_clears_edit_state_so_re_enabling_starts_in_display_mode()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello")
            .Add(e => e.Disabled, false));

        // Enter edit mode.
        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Single(cut.FindAll("input"));
        Assert.True(IsEditing(cut.Instance));

        // The editor is disabled while the field is open.
        cut.Render(p => p
            .Add(e => e.Value, "hello")
            .Add(e => e.Disabled, true));

        // The field is hidden by the render gate AND _editing must be cleared.
        // Without the fix _editing stays latched here.
        Assert.False(IsEditing(cut.Instance));

        // Re-enabling must NOT resurrect the stale edit — we are in display mode.
        cut.Render(p => p
            .Add(e => e.Value, "hello")
            .Add(e => e.Disabled, false));

        Assert.False(IsEditing(cut.Instance));
        Assert.Empty(cut.FindAll("input"));
        Assert.Single(cut.FindAll("[role='button']"));
    }
}
