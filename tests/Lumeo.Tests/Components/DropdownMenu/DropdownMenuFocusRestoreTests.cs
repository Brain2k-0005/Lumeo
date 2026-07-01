using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// B2 — focus restore (WCAG 2.4.3). Opening a DropdownMenu saves the trigger's
/// focus and moves focus into the menu; closing it must hand focus back to the
/// trigger rather than dropping it to &lt;body&gt;. saveFocus/restoreFocus are a
/// no-trap pair (Tab still closes the menu per the WAI-ARIA pattern).
/// </summary>
public class DropdownMenuFocusRestoreTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly RenderFragment _child;

    public DropdownMenuFocusRestoreTests()
    {
        _ctx.AddLumeoServices();
        _child = b =>
        {
            b.OpenComponent<L.DropdownMenuTrigger>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Menu")));
            b.CloseComponent();
            b.OpenComponent<L.DropdownMenuContent>(2);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "items")));
            b.CloseComponent();
        };
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DropdownMenu> RenderOpen()
        => _ctx.Render<L.DropdownMenu>(p => p.Add(m => m.Open, true).Add(m => m.ChildContent, _child));

    [Fact]
    public void Open_Saves_The_Trigger_Focus()
    {
        RenderOpen();
        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "saveFocus");
    }

    [Fact]
    public void Close_Restores_Focus_To_The_Trigger()
    {
        var cut = RenderOpen();

        // Close the menu (controlled) — Cleanup must hand focus back.
        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, _child));

        var restore = Assert.Single(_ctx.JSInterop.Invocations, i => i.Identifier == "restoreFocus");
        // Keyed by the menu content id (the same key saveFocus used).
        Assert.False(string.IsNullOrEmpty(restore.Arguments[0] as string));
    }
}
