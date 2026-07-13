using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// B14 — the DateTimePicker time columns must be arrow-key navigable like the
/// standalone TimePicker (they previously had no keyboard nav at all): ArrowDown/Up
/// move roving focus to the next/previous ENABLED option, Home/End jump to the
/// first/last enabled, and the arrow keys' scroll is suppressed via preventDefault.
/// </summary>
public class DateTimePickerKeyboardNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DateTimePickerKeyboardNavTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DateTimePicker> RenderOpen(Action<ComponentParameterCollectionBuilder<L.DateTimePicker>> extra)
    {
        var cut = _ctx.Render<L.DateTimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, true);
            extra(p);
        });
        cut.Find("button").Click(); // open the popover
        return cut;
    }

    private static AngleSharp.Dom.IElement HourOption(IRenderedComponent<L.DateTimePicker> cut, int hour)
        => cut.FindAll("[role='listbox']")[0].QuerySelectorAll("button[role='option']")
            .First(b => b.TextContent.Trim() == hour.ToString("D2"));

    private bool FocusedId(string id) => _ctx.JSInterop.Invocations
        .Any(i => i.Identifier == "focusElementById" && (i.Arguments.Count > 0 ? i.Arguments[0] as string : null) == id);

    [Fact]
    public void ArrowDown_Moves_Roving_Focus_To_The_Next_Hour()
    {
        var cut = RenderOpen(p => p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0)));
        var nextId = HourOption(cut, 13).GetAttribute("id");

        HourOption(cut, 12).KeyDown("ArrowDown");

        Assert.True(FocusedId(nextId!));
    }

    [Fact]
    public void ArrowUp_Moves_Roving_Focus_To_The_Previous_Hour()
    {
        var cut = RenderOpen(p => p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0)));
        var prevId = HourOption(cut, 11).GetAttribute("id");

        HourOption(cut, 12).KeyDown("ArrowUp");

        Assert.True(FocusedId(prevId!));
    }

    [Fact]
    public void Home_Focuses_The_First_Enabled_Hour_Skipping_Disabled()
    {
        // MinDate 09:00 on the value's day disables hours 0-8, so Home lands on 09.
        var cut = RenderOpen(p =>
        {
            p.Add(c => c.MinDate, new DateTime(2026, 6, 10, 9, 0, 0));
            p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0));
        });
        var firstEnabledId = HourOption(cut, 9).GetAttribute("id");

        HourOption(cut, 12).KeyDown("Home");

        Assert.True(FocusedId(firstEnabledId!));      // 09, not the disabled 00
    }

    [Fact]
    public void End_Focuses_The_Last_Hour()
    {
        var cut = RenderOpen(p => p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0)));
        var lastId = HourOption(cut, 23).GetAttribute("id");

        HourOption(cut, 12).KeyDown("End");

        Assert.True(FocusedId(lastId!));
    }

    [Fact]
    public void Enter_Selects_The_Focused_Hour()
    {
        DateTime? captured = null;
        var fires = 0;
        var cb = EventCallback.Factory.Create<DateTime?>(this, (DateTime? d) => { captured = d; fires++; });
        var cut = RenderOpen(p =>
        {
            p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0));
            p.Add(c => c.ValueChanged, cb);
        });

        // The option is a native <button>, so Enter/Space activate it via a synthesized native click
        // (modelled here with .Click()). Selection fires exactly once.
        HourOption(cut, 15).Click();
        Assert.Equal(15, captured?.Hour);
        Assert.Equal(1, fires);

        // The keydown handler intentionally does NOT also select on Enter/Space — otherwise Enter in a
        // real browser would fire ValueChanged twice (keydown handler + the synthesized native click).
        // A bare Enter keydown must be a no-op for selection (Codex P3).
        HourOption(cut, 15).KeyDown("Enter");
        Assert.Equal(1, fires);
    }

    [Fact]
    public void Opening_Registers_PreventDefault_For_The_Column_Nav_Keys()
    {
        var cut = RenderOpen(p => p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0)));
        var hoursColId = cut.FindAll("[role='listbox']")[0].GetAttribute("id");

        var reg = Assert.Single(_ctx.JSInterop.Invocations,
            i => i.Identifier == "registerPreventDefaultKeys" && (i.Arguments[0] as string) == hoursColId);
        var rules = Lumeo.Tests.Helpers.PreventDefaultRuleCapture.Parse(reg.Arguments[1]);
        var keys = rules.Select(r => r.Key).ToList();
        Assert.Contains("ArrowDown", keys);
        Assert.Contains("ArrowUp", keys);
        Assert.Contains("Home", keys);
        Assert.Contains("End", keys);
    }

    [Fact]
    public void Selected_Hour_Is_The_Roving_Tab_Stop()
    {
        var cut = RenderOpen(p => p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0)));

        Assert.Equal("0", HourOption(cut, 12).GetAttribute("tabindex"));   // selected = tabbable
        Assert.Equal("-1", HourOption(cut, 13).GetAttribute("tabindex"));  // others roved out
    }
}
