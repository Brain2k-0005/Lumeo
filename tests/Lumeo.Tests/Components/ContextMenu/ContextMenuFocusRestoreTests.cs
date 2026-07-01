using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// B2 — focus restore (WCAG 2.4.3). ContextMenu saves the focus that was active
/// when it opened and hands it back on close, rather than dropping focus to
/// &lt;body&gt;. Same no-trap saveFocus/restoreFocus pair as DropdownMenu.
/// </summary>
public class ContextMenuFocusRestoreTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly RenderFragment _child;

    public ContextMenuFocusRestoreTests()
    {
        _ctx.AddLumeoServices();
        _child = b =>
        {
            b.OpenComponent<L.ContextMenuTrigger>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Right-click me")));
            b.CloseComponent();
            b.OpenComponent<L.ContextMenuContent>(2);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "items")));
            b.CloseComponent();
        };
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Open_Saves_Focus_And_Close_Restores_It()
    {
        var cut = _ctx.Render<L.ContextMenu>(p => p.Add(m => m.Open, true).Add(m => m.ChildContent, _child));
        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "saveFocus");

        cut.Render(p => p.Add(m => m.Open, false).Add(m => m.ChildContent, _child));
        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "restoreFocus");
    }
}
