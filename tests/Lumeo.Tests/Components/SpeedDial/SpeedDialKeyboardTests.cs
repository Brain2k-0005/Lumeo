using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.SpeedDial;

/// <summary>
/// Regression tests for #234 — SpeedDial had no keyboard support, no focus
/// management and no menu ARIA. It now exposes role=menu / menuitem, focuses
/// the first action on open, restores focus to the trigger on close, and
/// supports arrow navigation + Escape.
/// </summary>
public class SpeedDialKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SpeedDialKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.SpeedDial.SpeedDialItem> TwoItems() => new()
    {
        new() { Label = "Share" },
        new() { Label = "Print" },
    };

    [Fact]
    public void Trigger_AriaExpanded_Uses_Lowercase_Token_When_Closed()
    {
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Equal("menu", cut.Find("button").GetAttribute("aria-haspopup"));
    }

    [Fact]
    public void Open_Marks_Menu_Roles_And_Action_Labels()
    {
        _interop.MenuItemCount = 2;
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));
        cut.Find("button").Click();

        Assert.NotNull(cut.Find("[role='menu']"));
        var actions = cut.FindAll("[role='menuitem']");
        Assert.Equal(2, actions.Count);
        Assert.Equal("Share", actions[0].GetAttribute("aria-label"));
    }

    [Fact]
    public void Opening_Focuses_First_Action()
    {
        _interop.MenuItemCount = 2;
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));

        cut.Find("button").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.ContainerId.StartsWith("speeddial-content-") && c.Index == 0));
    }

    [Fact]
    public void Escape_Closes_And_Restores_Focus_To_Trigger()
    {
        _interop.MenuItemCount = 2;
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));
        cut.Find("button").Click();
        Assert.Contains("Share", cut.Markup);

        // Escape on the wrapper closes the menu and returns focus to the trigger.
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // The close is async — the handler awaits focus-restore interop before the
        // re-render — so wait for the menu to actually leave the markup rather than
        // asserting on the transient state the moment the keydown returns (that race
        // flaked under parallel test load).
        cut.WaitForAssertion(() => Assert.DoesNotContain("role=\"menu\"", cut.Markup));
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusElementCalls, id => id.StartsWith("speeddial-trigger-")));
    }

    [Fact]
    public void ArrowDown_Moves_Focus_To_Next_Action()
    {
        _interop.MenuItemCount = 2;
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));
        cut.Find("button").Click();

        // Opening focuses index 0 asynchronously, and ArrowDown computes the next
        // index from that state. If ArrowDown fires before the open-focus lands, the
        // handler increments from a stale index and re-lands on 0 instead of 1. Wait
        // for the open-focus first (this race flaked under parallel CI load, same as
        // the Escape test above).
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.Index == 0));

        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        // Index 0 on open, then ArrowDown -> index 1.
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.Index == 1));
    }
}
