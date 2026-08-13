using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Toolbar behaviour of <see cref="L.Scheduler"/>.
///
/// <para>
/// This file used to be mostly about the FullCalendar bridge — that the module was imported
/// by exact path, that init ran against it, and what the init options carried. None of that
/// exists any more, and none of it described anything a user could observe. What is left is
/// the part that did.
/// </para>
/// </summary>
public class SchedulerBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerBehaviorTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement ViewButton(IRenderedComponent<L.Scheduler> cut, string label) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == label);

    [Fact]
    public void Switching_view_updates_the_active_toggle_aria_pressed_state()
    {
        var cut = _ctx.Render<L.Scheduler>();

        // Month is the default active view, so its toggle is pressed and Day's is not —
        // the single-select view toolbar contract.
        Assert.Equal("true", ViewButton(cut, "Month").GetAttribute("aria-pressed"));
        Assert.Equal("false", ViewButton(cut, "Day").GetAttribute("aria-pressed"));

        ViewButton(cut, "Day").Click();

        // After switching, active state moves to Day (the picked view flows back through
        // _currentView → the ToggleGroup Value binding) and Month is no longer pressed.
        Assert.Equal("true", ViewButton(cut, "Day").GetAttribute("aria-pressed"));
        Assert.Equal("false", ViewButton(cut, "Month").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Switching_view_swaps_the_rendered_grid()
    {
        // The companion the old suite never had: the toggle's pressed state is only worth
        // anything if the grid underneath actually changed.
        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.InitialDate, new DateTime(2026, 6, 15)));

        Assert.Equal(42, cut.FindAll("[data-cell-date]").Count);   // month grid

        ViewButton(cut, "Day").Click();

        Assert.Empty(cut.FindAll("[data-cell-date]"));             // no month cells left
        Assert.Single(cut.FindAll("[data-daycol]"));               // one day column
    }
}
