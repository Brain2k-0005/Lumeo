using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// #222 — DropdownMenu gains Radix type-to-focus. Printable keystrokes on the
/// open menu container accumulate a query and jump focus to the first matching
/// item (via the shared MenuTypeahead buffer + the JS focusMenuItemByTypeahead
/// DOM match). These tests drive the C# wiring through TrackingInteropService.
/// </summary>
public class DropdownMenuTypeaheadTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DropdownMenuTypeaheadTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderOpenMenu()
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DropdownMenu>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DropdownMenuContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DropdownMenuItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                    inner.CloseComponent();
                    inner.OpenComponent<L.DropdownMenuItem>(2);
                    inner.AddAttribute(3, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Printable_Key_Triggers_Typeahead_With_Buffered_Query()
    {
        var cut = RenderOpenMenu();
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "b" });

        var call = Assert.Single(_interop.TypeaheadCalls);
        Assert.Equal("b", call.Query);
        Assert.StartsWith("dropdown-content-", call.ContainerId);
    }

    [Fact]
    public void Consecutive_Keys_Accumulate_The_Query()
    {
        var cut = RenderOpenMenu();
        var content = cut.Find("[role='menu']");
        content.KeyDown(new KeyboardEventArgs { Key = "b" });
        content.KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Equal(2, _interop.TypeaheadCalls.Count);
        Assert.Equal("b", _interop.TypeaheadCalls[0].Query);
        // Within the 1s reset window the second key appends → "ba".
        Assert.Equal("ba", _interop.TypeaheadCalls[1].Query);
    }

    [Fact]
    public void Matched_Index_Is_Carried_Into_The_Next_Call_As_CurrentIndex()
    {
        _interop.TypeaheadMatchIndex = 1; // pretend "Banana" (index 1) matched
        var cut = RenderOpenMenu();
        var content = cut.Find("[role='menu']");

        content.KeyDown(new KeyboardEventArgs { Key = "b" });
        content.KeyDown(new KeyboardEventArgs { Key = "x" });

        // The first call starts from -1; the second must pass the matched index 1
        // so repeated keystrokes cycle relative to the focused item.
        Assert.Equal(-1, _interop.TypeaheadCalls[0].CurrentIndex);
        Assert.Equal(1, _interop.TypeaheadCalls[1].CurrentIndex);
    }

    [Fact]
    public void Modifier_Combos_Are_Not_Typeahead()
    {
        var cut = RenderOpenMenu();
        // Ctrl+a (select-all) must NOT drive typeahead.
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "a", CtrlKey = true });
        Assert.Empty(_interop.TypeaheadCalls);
    }

    [Fact]
    public void Navigation_And_Space_Keys_Do_Not_Trigger_Typeahead()
    {
        var cut = RenderOpenMenu();
        // ArrowDown (nav) + Space (item activation) must not feed typeahead.
        // Escape is covered separately — it unmounts the menu.
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = " " });
        Assert.Empty(_interop.TypeaheadCalls);
    }
}
