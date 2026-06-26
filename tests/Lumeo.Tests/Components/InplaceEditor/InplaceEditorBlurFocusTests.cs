using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InplaceEditor;

/// <summary>
/// Regression coverage for the blur-save focus-steal bug (battle-wave2 n=6, high):
/// an implicit blur-save (e.g. the user pressing Tab to move to the next control)
/// used to set <c>_focusDisplayOnRender = true</c> in <c>Save()</c>, which then forced
/// focus back onto the display trigger in <c>OnAfterRenderAsync</c> — defeating the
/// Tab-out navigation the user just initiated. An explicit commit (Enter key, or the
/// Save button) SHOULD still restore focus to the trigger; only the implicit blur path
/// must leave focus alone.
///
/// The directly observable state that drives the focus move is the private
/// <c>_focusDisplayOnRender</c> field (consumed by <c>OnAfterRenderAsync</c> to call
/// <c>_displayRef.FocusAsync()</c>). JSInterop is Loose so FocusAsync is a no-op mock and
/// would not throw either way; asserting on the field directly is what distinguishes
/// "requested focus-back" from "left focus alone".
/// </summary>
public class InplaceEditorBlurFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InplaceEditorBlurFocusTests()
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

    private static bool FocusDisplayRequested(L.InplaceEditor instance)
    {
        var field = typeof(L.InplaceEditor)
            .GetField("_focusDisplayOnRender", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (bool)field!.GetValue(instance)!;
    }

    [Fact]
    public void Blur_save_commits_value_but_does_not_steal_focus_back_to_trigger()
    {
        string? committed = null;
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello")
            .Add(e => e.ValueChanged, v => committed = v));

        // Enter edit mode (display trigger -> input).
        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var input = cut.Find("input");
        input.Input("world");

        // User presses Tab -> the input blurs. SaveOnBlur (default true), no buttons.
        input.Blur();

        // The value must still be committed on blur...
        Assert.Equal("world", committed);
        // ...and we are back in display mode.
        Assert.Empty(cut.FindAll("input"));

        // CRITICAL: the implicit blur-save must NOT request focus back to the trigger,
        // otherwise Tab navigation to the next control is broken. Without the fix this
        // field is left true and OnAfterRenderAsync yanks focus back.
        Assert.False(FocusDisplayRequested(cut.Instance));
    }
}
