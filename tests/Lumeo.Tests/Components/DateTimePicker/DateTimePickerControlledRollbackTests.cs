using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// Regression tests for the controlled-component rollback fix on DateTimePicker.
/// When the picker is used in controlled mode (ValueChanged bound) and the parent
/// vetoes a pick by re-rendering with the original Value, the UI must roll back
/// to the bound value rather than keeping the optimistic local pick.
/// </summary>
public class DateTimePickerControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Hour buttons use the px-3 list style; the hours column renders first, so
    // the first matching button is the hour (not the minute) entry. Mirrors
    // DateTimePickerPendingTimeTests' helper.
    private static void SelectHour(IRenderedComponent<L.DateTimePicker> cut, string hour) =>
        cut.FindAll("button")
            .First(b => b.TextContent.Trim() == hour && (b.GetAttribute("class") ?? "").Contains("px-3"))
            .Click();

    private static bool HourSelected(IRenderedComponent<L.DateTimePicker> cut, string hour) =>
        cut.FindAll("button")
            .Any(b => b.TextContent.Trim() == hour && (b.GetAttribute("class") ?? "").Contains("bg-primary"));

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        // Parent starts with Value=2026-06-15 08:00 and vetoes every pick by
        // keeping its own state unchanged (always re-renders with the original).
        var original = new DateTime(2026, 6, 15, 8, 0, 0);
        DateTime? parentState = original;
        IRenderedComponent<L.DateTimePicker>? cut = null;

        var callback = EventCallback.Factory.Create<DateTime?>(_ctx, (DateTime? incoming) =>
        {
            // Veto: do NOT adopt the incoming value into parentState; re-render
            // the picker with the original (rejected) bound value.
            cut!.Render(p =>
            {
                p.Add(c => c.Value, parentState);
                p.Add(c => c.ValueChanged, EventCallback.Factory.Create<DateTime?>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.DateTimePicker>(p => p
            .Add(c => c.Value, original)
            .Add(c => c.ValueChanged, callback));

        cut.Find("button[type='button']").Click(); // open popover
        Assert.True(HourSelected(cut, "08"));

        // Pick a new hour — UpdateValue optimistically commits and fires
        // ValueChanged; the parent vetoes and re-renders with the original Value.
        SelectHour(cut, "10");

        // After the veto the picker must show the ORIGINAL hour again, not "10".
        Assert.True(HourSelected(cut, "08"));
        Assert.False(HourSelected(cut, "10"));
    }

    // --- Controlled: accepted pick keeps new value ---

    [Fact]
    public void Controlled_Accepted_Pick_Keeps_New_Value()
    {
        // Parent accepts every pick by updating its own state and re-rendering.
        var original = new DateTime(2026, 6, 15, 8, 0, 0);
        DateTime? parentState = original;
        IRenderedComponent<L.DateTimePicker>? cut = null;

        EventCallback<DateTime?> callback = default;
        callback = EventCallback.Factory.Create<DateTime?>(_ctx, (DateTime? incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(c => c.Value, parentState);
                p.Add(c => c.ValueChanged, callback);
            });
        });

        cut = _ctx.Render<L.DateTimePicker>(p => p
            .Add(c => c.Value, original)
            .Add(c => c.ValueChanged, callback));

        cut.Find("button[type='button']").Click();
        SelectHour(cut, "10");

        // Parent accepted — the new hour must stay selected.
        Assert.True(HourSelected(cut, "10"));
        Assert.False(HourSelected(cut, "08"));
    }

    // --- Controlled: programmatic parent reset ---

    [Fact]
    public void Controlled_Programmatic_Reset_Is_Adopted()
    {
        // Start at 08:00; parent programmatically resets to a different value
        // WITHOUT the user picking anything first (e.g. external data reload).
        var original = new DateTime(2026, 6, 15, 8, 0, 0);
        var cut = _ctx.Render<L.DateTimePicker>(p => p
            .Add(c => c.Value, original)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateTime?>(_ctx, (_) => { })));

        cut.Find("button[type='button']").Click();
        Assert.True(HourSelected(cut, "08"));

        // Parent resets the bound value without a user pick first.
        cut.Render(p => p
            .Add(c => c.Value, new DateTime(2026, 6, 15, 14, 0, 0))
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateTime?>(_ctx, (_) => { })));

        Assert.True(HourSelected(cut, "14"));
        Assert.False(HourSelected(cut, "08"));
    }
}
