using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

/// <summary>
/// Calendar's <c>DateTooltip</c> must render the styled Lumeo <see cref="L.Tooltip"/>
/// per day — NOT the native browser <c>title</c> attribute — and only for days that
/// actually return a hint (so non-tooltip days stay bare and the grid stays cheap).
/// </summary>
public class CalendarDateTooltipTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public CalendarDateTooltipTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Calendar> RenderWithTooltipOn15() =>
        _ctx.Render<L.Calendar>(p => p
            .Add(c => c.Value, new DateOnly(2024, 6, 1))
            .Add(c => c.DateTooltip, (Func<DateTime, string?>)(dt => dt.Day == 15 ? "Booked" : null)));

    [Fact]
    public void DateTooltip_Day_Is_Wrapped_In_Lumeo_Tooltip_With_No_Native_Title()
    {
        var cut = RenderWithTooltipOn15();

        // No day button carries a native title — the hint is the Lumeo Tooltip now.
        Assert.DoesNotContain(cut.FindAll("button"), b => b.HasAttribute("title"));

        // Exactly one day (the 15th) is the AsChild trigger of a Lumeo Tooltip: its
        // parent is the tooltip root, forced to `block w-full` so the grid cell keeps
        // its full width. Non-tooltip days are bare (no such wrapper).
        var wrapped = cut.FindAll("button")
            .Where(b => (b.ParentElement?.GetAttribute("class") ?? "").Contains("w-full")
                        && (b.ParentElement?.GetAttribute("class") ?? "").Contains("relative"))
            .ToList();
        Assert.Single(wrapped);
        Assert.Equal("15", wrapped[0].TextContent.Trim());
    }

    [Fact]
    public async Task DateTooltip_Day_Hover_Reveals_The_Lumeo_Tooltip_Text()
    {
        var cut = RenderWithTooltipOn15();

        var day15 = cut.FindAll("button")
            .First(b => (b.ParentElement?.GetAttribute("class") ?? "").Contains("w-full"));
        day15.ParentElement!.TriggerEvent("onmouseenter", new MouseEventArgs());

        await Task.Run(() => cut.WaitForAssertion(
            () => Assert.Contains("Booked", cut.Find("[role='tooltip']").TextContent),
            TimeSpan.FromSeconds(2)));
    }
}
