using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.SplitButton;

/// <summary>
/// Wave 4 composition audit — SplitButton wires two native Lumeo &lt;Button&gt;s
/// (primary action + chevron, AsChild-folded onto the DropdownMenuTrigger).
/// Enter/Space activation on either half and the dropdown's own Escape/arrow
/// handling are free via the browser's native button semantics + the
/// already-tested DropdownMenu primitive (see SplitButtonBehaviorTests /
/// SplitButtonChevronAriaTests, which click both halves and assert
/// aria-expanded/aria-controls). This file's own incremental surface — not
/// covered elsewhere — is the plain DOM Tab order between the two halves.
/// </summary>
public class SplitButtonKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SplitButtonKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment MenuWithItem(string label) => builder =>
    {
        builder.OpenComponent<DropdownMenuItem>(0);
        builder.AddAttribute(1, "ChildContent", (RenderFragment)(b => b.AddContent(0, label)));
        builder.CloseComponent();
    };

    private IRenderedComponent<Lumeo.SplitButton> RenderSplit()
        => _ctx.Render<Lumeo.SplitButton>(p =>
        {
            p.Add(b => b.Text, "Save");
            p.Add(b => b.MenuContent, MenuWithItem("Save and exit"));
        });

    [Fact]
    public void Primary_Button_Precedes_Chevron_Button_In_Tab_Order()
    {
        // Neither half carries an explicit tabindex, so native DOM order IS the
        // Tab order — the primary action must render before the chevron.
        var cut = RenderSplit();

        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);
        Assert.Null(buttons[0].GetAttribute("tabindex"));
        Assert.Null(buttons[1].GetAttribute("tabindex"));
        Assert.Equal("menu", buttons[1].GetAttribute("aria-haspopup"));
    }

    [Fact]
    public void Neither_Half_Is_Disabled_By_Default()
    {
        // Both halves must be independently reachable/activatable — a disabled
        // wrapper on either would silently remove it from the Tab order.
        var cut = RenderSplit();

        Assert.All(cut.FindAll("button"), b => Assert.False(b.HasAttribute("disabled")));
    }
}
