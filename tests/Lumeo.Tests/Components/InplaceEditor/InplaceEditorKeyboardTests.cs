using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InplaceEditor;

/// <summary>
/// InplaceEditor has two modes: an idle "display" trigger (a role=button div) and an
/// active "edit" field. The display div enters edit mode on click or on Enter/Space via
/// <c>HandleDisplayKeyDown</c>; the edit input commits on Enter (when SaveOnEnter and the
/// Text edit mode) through <c>OnKeyDown -&gt; Save</c> — firing ValueChanged with the typed
/// text — and cancels on Escape through <c>OnKeyDown -&gt; Cancel</c>, which restores the
/// original value and does NOT fire ValueChanged. The component uses JS interop only to
/// move focus (FocusAsync) on render, so Loose JSInterop is enough; the mode transitions
/// themselves are pure C# driven from keyboard events.
/// </summary>
public class InplaceEditorKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InplaceEditorKeyboardTests()
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

    [Fact]
    public void Enter_on_display_enters_edit_mode()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello"));

        // No input while idle.
        Assert.Empty(cut.FindAll("input"));

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // An editable text input is now present, seeded with the current value.
        var input = cut.Find("input");
        Assert.Equal("hello", input.GetAttribute("value"));
    }

    [Fact]
    public void Space_on_display_enters_edit_mode()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello"));

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.Single(cut.FindAll("input"));
    }

    [Fact]
    public void Enter_in_input_commits_new_value_and_returns_to_display()
    {
        string? committed = null;
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello")
            .Add(e => e.ValueChanged, v => committed = v));

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var input = cut.Find("input");
        input.Input("world");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("world", committed);
        // Back to display mode — the edit input is gone.
        Assert.Empty(cut.FindAll("input"));
    }

    [Fact]
    public void Escape_in_input_cancels_without_committing_and_returns_to_display()
    {
        string? committed = null;
        var cancelled = false;
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello")
            .Add(e => e.ValueChanged, v => committed = v)
            .Add(e => e.OnCancel, () => cancelled = true));

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var input = cut.Find("input");
        input.Input("world");
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // Escape must not commit; ValueChanged never fires, OnCancel does.
        Assert.Null(committed);
        Assert.True(cancelled);
        Assert.Empty(cut.FindAll("input"));
    }

    [Fact]
    public void Disabled_display_ignores_Enter_keydown()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello")
            .Add(e => e.Disabled, true));

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // Disabled editor stays in display mode.
        Assert.Empty(cut.FindAll("input"));
    }
}
