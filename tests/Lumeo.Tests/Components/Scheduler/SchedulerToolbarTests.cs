using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// bUnit coverage for <see cref="L.SchedulerToolbar"/> — extracted from the FullCalendar-era
/// <c>Scheduler.razor</c> (spec §1.1/§1.2) so the first-party views can share one toolbar
/// instance. Purely presentational: Prev/Next/Today buttons and the view switcher.
/// </summary>
public class SchedulerToolbarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_The_Given_Title()
    {
        var cut = _ctx.Render<L.SchedulerToolbar>(p => p.Add(c => c.Title, "March 2026"));

        Assert.Contains("March 2026", cut.Markup);
    }

    [Fact]
    public void Clicking_Today_Fires_OnToday()
    {
        var fired = false;
        var cut = _ctx.Render<L.SchedulerToolbar>(p => p.Add(c => c.OnToday, () => fired = true));

        var buttons = cut.FindAll("button");
        var todayButton = buttons.First(b => b.TextContent.Trim() == "Today");
        todayButton.Click();

        Assert.True(fired);
    }

    [Fact]
    public void Selecting_A_Different_View_Fires_CurrentViewChanged()
    {
        L.SchedulerView? changed = null;
        var cut = _ctx.Render<L.SchedulerToolbar>(p => p
            .Add(c => c.CurrentView, L.SchedulerView.Month)
            .Add(c => c.CurrentViewChanged, (L.SchedulerView v) => changed = v));

        var weekItem = cut.FindAll("button").First(b => b.TextContent.Trim() == "Week");
        weekItem.Click();

        Assert.Equal(L.SchedulerView.Week, changed);
    }

    [Fact]
    public void Selecting_The_Already_Current_View_Does_Not_Fire_CurrentViewChanged()
    {
        var fired = false;
        var cut = _ctx.Render<L.SchedulerToolbar>(p => p
            .Add(c => c.CurrentView, L.SchedulerView.Month)
            .Add(c => c.CurrentViewChanged, (L.SchedulerView _) => fired = true));

        var monthItem = cut.FindAll("button").First(b => b.TextContent.Trim() == "Month");
        monthItem.Click();

        Assert.False(fired);
    }
}
