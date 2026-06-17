using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// #207 — DatePicker multiple-date selection (Mode=Multiple + Values/
/// ValuesChanged). The inline calendar toggles days into the bound set, the
/// trigger summarises the selection, and Clear empties it.
/// </summary>
public class DatePickerMultipleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerMultipleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement Day(IRenderedComponent<L.DatePicker> cut, int day)
        => cut.FindAll("button")
            .First(b => b.TextContent.Trim() == day.ToString()
                        && !(b.GetAttribute("class") ?? "").Contains("muted-foreground/50"));

    [Fact]
    public void Inline_Multiple_Toggles_Days_Into_Values()
    {
        var captured = new List<DateOnly>();
        var cb = EventCallback.Factory.Create<List<DateOnly>?>(this, v => captured = v ?? new());
        var seed = new List<DateOnly> { new(2024, 6, 15) };
        var cut = _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.Inline, true);
            p.Add(c => c.Mode, L.DatePicker.DatePickerMode.Multiple);
            p.Add(c => c.Values, seed); // anchors to June 2024
            p.Add(c => c.ValuesChanged, cb);
        });

        Day(cut, 10).Click();

        Assert.Contains(new DateOnly(2024, 6, 10), captured);
        Assert.Contains(new DateOnly(2024, 6, 15), captured);
    }

    [Fact]
    public void Trigger_Lists_Up_To_Three_Dates()
    {
        var cut = _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.Mode, L.DatePicker.DatePickerMode.Multiple);
            p.Add(c => c.Values, new List<DateOnly> { new(2024, 6, 1), new(2024, 6, 2) });
        });

        // The trigger shows both dates (count <= 3 lists them).
        var trigger = cut.Find("button");
        Assert.Contains("2024", trigger.TextContent);
    }

    [Fact]
    public void Trigger_Collapses_To_Count_When_Many_Selected()
    {
        var cut = _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.Mode, L.DatePicker.DatePickerMode.Multiple);
            p.Add(c => c.Values, new List<DateOnly>
            {
                new(2024, 6, 1), new(2024, 6, 2), new(2024, 6, 3), new(2024, 6, 4),
            });
        });

        // 4 selected → "4 selected" summary.
        Assert.Contains("4 selected", cut.Find("button").TextContent);
    }

    [Fact]
    public void Trigger_Shows_Placeholder_When_Empty()
    {
        var cut = _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.Mode, L.DatePicker.DatePickerMode.Multiple);
            p.Add(c => c.Placeholder, "Pick days");
        });

        Assert.Contains("Pick days", cut.Find("button").TextContent);
    }

    [Fact]
    public void Clear_Empties_The_Selection()
    {
        List<DateOnly>? captured = new() { new(2024, 6, 1) };
        var cb = EventCallback.Factory.Create<List<DateOnly>?>(this, v => captured = v);
        var cut = _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.Mode, L.DatePicker.DatePickerMode.Multiple);
            p.Add(c => c.Clearable, true);
            p.Add(c => c.Values, new List<DateOnly> { new(2024, 6, 1), new(2024, 6, 2) });
            p.Add(c => c.ValuesChanged, cb);
        });

        // The clear button carries the localized "Clear" aria-label.
        cut.Find("button[aria-label='Clear']").Click();

        Assert.Null(captured);
    }
}
