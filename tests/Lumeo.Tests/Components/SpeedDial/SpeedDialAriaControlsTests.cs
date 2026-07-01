using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.SpeedDial;

/// <summary>
/// Regression tests for triage #192 — the FAB trigger had aria-expanded and
/// aria-haspopup="menu" but no aria-controls, so assistive tech could not
/// associate the expanded menu with the trigger. The trigger now exposes
/// aria-controls pointing at the menu container's id while the menu is open.
/// </summary>
public class SpeedDialAriaControlsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SpeedDialAriaControlsTests()
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
    public void Trigger_Has_No_AriaControls_When_Closed()
    {
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));

        // Closed: the menu container does not exist, so aria-controls must be
        // omitted (a null attribute is dropped by Blazor) rather than dangle.
        var trigger = cut.Find("button[id^='speeddial-trigger-']");
        Assert.Null(trigger.GetAttribute("aria-controls"));
    }

    [Fact]
    public void Trigger_AriaControls_Points_At_Menu_When_Open()
    {
        _interop.MenuItemCount = 2;
        var cut = _ctx.Render<L.SpeedDial>(p => p.Add(c => c.Items, TwoItems()));

        cut.Find("button[id^='speeddial-trigger-']").Click();

        var menuId = cut.Find("[role='menu']").GetAttribute("id");
        Assert.NotNull(menuId);

        var trigger = cut.Find("button[id^='speeddial-trigger-']");
        Assert.Equal(menuId, trigger.GetAttribute("aria-controls"));
    }
}
