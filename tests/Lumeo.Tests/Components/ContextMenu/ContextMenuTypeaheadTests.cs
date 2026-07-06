using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Wave 4 — ContextMenu gains Radix type-to-focus, parity with DropdownMenu.
/// Printable keystrokes on the open menu container accumulate a query (shared
/// MenuTypeahead buffer) and jump focus to the first matching item via the JS
/// focusMenuItemByTypeahead DOM match. Driven through TrackingInteropService.
/// </summary>
public class ContextMenuTypeaheadTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ContextMenuTypeaheadTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.ContextMenu> RenderOpenMenu()
        => _ctx.Render<L.ContextMenu>(p => p.Add(m => m.Open, true).Add(m => m.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<L.ContextMenuContent>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.ContextMenuItem>(0);
                inner.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                inner.CloseComponent();
                inner.OpenComponent<L.ContextMenuItem>(2);
                inner.AddAttribute(3, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                inner.CloseComponent();
            }));
            b.CloseComponent();
        })));

    [Fact]
    public void Printable_Key_Triggers_Typeahead_With_Buffered_Query()
    {
        var cut = RenderOpenMenu();
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "b" });

        var call = Assert.Single(_interop.TypeaheadCalls);
        Assert.Equal("b", call.Query);
        Assert.StartsWith("context-menu-content-", call.ContainerId);
    }

    [Fact]
    public void Consecutive_Keys_Accumulate_The_Query()
    {
        var cut = RenderOpenMenu();
        var content = cut.Find("[role='menu']");
        content.KeyDown(new KeyboardEventArgs { Key = "b" });
        content.KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Equal(2, _interop.TypeaheadCalls.Count);
        Assert.Equal("ba", _interop.TypeaheadCalls[1].Query);
    }

    [Fact]
    public void Modifier_Combos_And_Nav_Keys_Are_Not_Typeahead()
    {
        var cut = RenderOpenMenu();
        var content = cut.Find("[role='menu']");
        content.KeyDown(new KeyboardEventArgs { Key = "a", CtrlKey = true });
        content.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        content.KeyDown(new KeyboardEventArgs { Key = " " });
        Assert.Empty(_interop.TypeaheadCalls);
    }
}
