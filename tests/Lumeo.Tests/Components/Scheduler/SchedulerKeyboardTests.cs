using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// The toolbar's prev/today/next controls.
///
/// <para>
/// These used to assert that clicking each button reached the matching FullCalendar interop
/// call. With the bridge gone the buttons move an internal anchor date instead, so what is
/// checked now is the outcome a user can see: the grid moves, and moves the right way.
/// </para>
/// </summary>
public class SchedulerKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Anchor = new(2026, 6, 15);

    private static AngleSharp.Dom.IElement NavButton(IRenderedComponent<L.Scheduler> cut, string label) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == label || b.GetAttribute("aria-label") == label);

    private IRenderedComponent<L.Scheduler> Render() =>
        _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Day)
            .Add(c => c.InitialDate, Anchor));

    private static string VisibleDay(IRenderedComponent<L.Scheduler> cut) =>
        cut.Find("[data-daycol]").GetAttribute("data-daycol")!;

    [Fact]
    public void Activating_Previous_Moves_The_Grid_Back()
    {
        var cut = Render();
        Assert.Equal("2026-06-15", VisibleDay(cut));

        NavButton(cut, "Previous").Click();

        Assert.Equal("2026-06-14", VisibleDay(cut));
    }

    [Fact]
    public void Activating_Next_Moves_The_Grid_Forward()
    {
        var cut = Render();

        NavButton(cut, "Next").Click();

        Assert.Equal("2026-06-16", VisibleDay(cut));
    }

    [Fact]
    public void Activating_Today_Returns_To_The_Current_Day()
    {
        var cut = Render();
        NavButton(cut, "Next").Click();
        Assert.NotEqual("2026-06-15", VisibleDay(cut));

        NavButton(cut, "Today").Click();

        // No interop is available in bUnit, so this exercises the documented fallback —
        // the host's own today, which is what the browser-date lookup degrades to.
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), VisibleDay(cut));
    }
}
