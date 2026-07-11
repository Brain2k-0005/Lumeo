using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ButtonGroup;

/// <summary>
/// ButtonGroup's own doc comment is explicit: "Roving tabindex is intentionally NOT
/// forced here" (#270) — unlike a Toolbar, each child keeps its own independent Tab
/// stop and native Enter/Space semantics owned by the child component itself. This is
/// the one incremental, ButtonGroup-owned keyboard surface: the wrapper must not add a
/// tabindex/roving-focus layer of its own. Pinned by asserting each child Button keeps
/// its individual (non -1) native tabindex and the group root itself is not a tab stop.
/// </summary>
public class ButtonGroupKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ButtonGroupKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Each_Child_Button_Keeps_Its_Own_Independent_Tab_Stop()
    {
        var cut = _ctx.Render<L.ButtonGroup>(p => p.AddChildContent(
            "<button type=\"button\">One</button><button type=\"button\">Two</button>"));

        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);
        foreach (var button in buttons)
        {
            // Native <button>: no explicit tabindex means it participates in the
            // default (source-order) tab sequence — a roving-tabindex group would
            // instead force tabindex="-1" on all-but-one child.
            Assert.False(button.HasAttribute("tabindex"));
        }
    }

    [Fact]
    public void Group_Root_Itself_Is_Not_A_Tab_Stop()
    {
        var cut = _ctx.Render<L.ButtonGroup>(p => p.AddChildContent("<button>X</button>"));

        var root = cut.Find("[role='group']");
        Assert.False(root.HasAttribute("tabindex"));
    }
}
