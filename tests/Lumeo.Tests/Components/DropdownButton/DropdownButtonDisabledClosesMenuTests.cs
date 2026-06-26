using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownButton;

/// <summary>
/// Regression coverage for the battle-test state-on-data-change finding #115:
/// when <see cref="L.DropdownButton"/> becomes <c>Disabled</c> while its menu is
/// open, the open state must not be stranded. The trigger turns
/// <c>pointer-events-none</c> and its <c>Toggle</c> early-returns on Disabled, so
/// the user can no longer click to dismiss — the menu would sit open over a
/// disabled control forever. DropdownButton must force-close on the disable edge.
///
/// These mirror <see cref="DropdownButtonBehaviorTests"/>: same loose-but-tracked
/// interop, same two-item MenuContent, and the same role-based assertions against
/// the public surface (open <c>role="menu"</c> popup + trigger <c>aria-expanded</c>)
/// rather than internal markup.
/// </summary>
public class DropdownButtonDisabledClosesMenuTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DropdownButtonDisabledClosesMenuTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DropdownButton> RenderButton(bool disabled = false)
        => _ctx.Render<L.DropdownButton>(p => p
            .Add(b => b.Text, "Actions")
            .Add(b => b.Disabled, disabled)
            .Add(b => b.MenuContent, (RenderFragment)(menu =>
            {
                menu.OpenComponent<L.DropdownMenuItem>(0);
                menu.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Edit")));
                menu.CloseComponent();

                menu.OpenComponent<L.DropdownMenuItem>(2);
                menu.AddAttribute(3, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Delete")));
                menu.CloseComponent();
            })));

    [Fact]
    public void Becoming_Disabled_While_Open_Force_Closes_The_Menu()
    {
        var cut = RenderButton();

        // Open the menu via the live trigger click.
        cut.Find("[role='button']").Click();
        Assert.NotEmpty(cut.FindAll("[role='menu']"));
        Assert.Equal("true", cut.Find("[role='button']").GetAttribute("aria-expanded"));

        // Disable the button while the menu is open. The trigger is now
        // pointer-events-none and Toggle is gated, so without the disable-edge
        // close the menu would be stranded open.
        cut.Render(p => p.Add(b => b.Disabled, true));

        Assert.Empty(cut.FindAll("[role='menu']"));
        Assert.Equal("false", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Reenabling_After_Disable_Edge_Leaves_The_Menu_Closed()
    {
        var cut = RenderButton();
        cut.Find("[role='button']").Click();
        Assert.NotEmpty(cut.FindAll("[role='menu']"));

        cut.Render(p => p.Add(b => b.Disabled, true));
        Assert.Empty(cut.FindAll("[role='menu']"));

        // Re-enabling must NOT resurrect the previously-open menu — the close was
        // a real state change, not a transient suppression.
        cut.Render(p => p.Add(b => b.Disabled, false));

        Assert.Empty(cut.FindAll("[role='menu']"));
        Assert.Equal("false", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Unrelated_Rerender_While_Open_Does_Not_Close_The_Menu()
    {
        var cut = RenderButton();
        cut.Find("[role='button']").Click();
        Assert.NotEmpty(cut.FindAll("[role='menu']"));

        // A re-render that does not flip Disabled (here: a Text change) must not
        // touch open state — the disable-edge guard keys on the false->true edge,
        // never on every OnParametersSet.
        cut.Render(p => p.Add(b => b.Text, "Updated"));

        Assert.NotEmpty(cut.FindAll("[role='menu']"));
        Assert.Equal("true", cut.Find("[role='button']").GetAttribute("aria-expanded"));
    }
}
