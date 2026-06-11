using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// Regression tests: a time picked BEFORE any date lives only in the internal
/// _timeValue (UpdateValue cannot commit without a date). OnParametersSet used
/// to resync the internal state from Value unconditionally, so any parent
/// re-render while Value was still null silently wiped the pending time.
/// </summary>
public class DateTimePickerPendingTimeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerPendingTimeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void SelectHour(IRenderedComponent<L.DateTimePicker> cut, string hour)
    {
        // Hour buttons use the px-3 list style; the hours column renders first,
        // so the first matching button is the hour (not the minute) entry.
        cut.FindAll("button")
            .First(b => b.TextContent.Trim() == hour && (b.GetAttribute("class") ?? "").Contains("px-3"))
            .Click();
    }

    [Fact]
    public void Pending_Time_Survives_Parent_ReRender_Before_Date_Is_Picked()
    {
        var cut = _ctx.Render<L.DateTimePicker>();

        // Open the popover and pick an hour — no date yet, so nothing commits.
        cut.Find("button[type='button']").Click();
        SelectHour(cut, "10");

        // Simulate a parent re-render while the selection is still pending.
        cut.Render();

        // The pending hour must still be highlighted (not wiped back to null Value).
        var selected = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Trim() == "10" && (b.GetAttribute("class") ?? "").Contains("bg-primary"));
        Assert.NotNull(selected);
    }

    [Fact]
    public void Time_Picked_Before_Date_Is_Committed_When_Date_Arrives_After_ReRender()
    {
        DateTime? committed = null;
        var cut = _ctx.Render<L.DateTimePicker>(p => p
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateTime?>(_ctx, (DateTime? v) => committed = v)));

        cut.Find("button[type='button']").Click();
        SelectHour(cut, "10");
        Assert.Null(committed); // no date yet — nothing committed

        // Parent re-render between time pick and date pick.
        cut.Render();

        // Now pick a date — calendar day buttons carry the h-8 w-8 cell classes.
        cut.FindAll("button")
            .First(b => b.TextContent.Trim() == "15" && (b.GetAttribute("class") ?? "").Contains("h-8 w-8"))
            .Click();

        Assert.NotNull(committed);
        Assert.Equal(new TimeSpan(10, 0, 0), committed!.Value.TimeOfDay);
        Assert.Equal(15, committed.Value.Day);
    }

    [Fact]
    public void External_Value_Change_Still_Resyncs_Internal_Selection()
    {
        var cut = _ctx.Render<L.DateTimePicker>();
        cut.Find("button[type='button']").Click();
        SelectHour(cut, "10");

        // The parent pushing a real Value must win over the pending state.
        cut.Render(p => p.Add(c => c.Value, new DateTime(2026, 6, 15, 8, 30, 0)));

        var selected = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Trim() == "08" && (b.GetAttribute("class") ?? "").Contains("bg-primary"));
        Assert.NotNull(selected);
    }

    [Fact]
    public void Clearing_Value_Externally_Resets_Internal_Selection()
    {
        var cut = _ctx.Render<L.DateTimePicker>(p => p
            .Add(c => c.Value, new DateTime(2026, 6, 15, 8, 30, 0)));
        cut.Find("button[type='button']").Click();

        cut.Render(p => p.Add(c => c.Value, (DateTime?)null));

        // The previously-selected hour must no longer be highlighted.
        var selected = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Trim() == "08" && (b.GetAttribute("class") ?? "").Contains("bg-primary"));
        Assert.Null(selected);
    }
}
