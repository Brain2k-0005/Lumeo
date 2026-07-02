using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>
/// Tests for B2 fix: TimePicker FullWidth parameter.
/// </summary>
public class TimePickerFullWidthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimePickerFullWidthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── back-compat: default renders WITHOUT w-full on wrappers ──────────────

    [Fact]
    public void Default_root_div_does_not_have_w_full()
    {
        var cut = _ctx.Render<L.TimePicker>();
        Assert.DoesNotContain("flex flex-col items-start w-full", cut.Markup);
    }

    [Fact]
    public void Default_Popover_wrapper_does_not_have_w_full()
    {
        var cut = _ctx.Render<L.TimePicker>();
        Assert.DoesNotContain("relative inline-block outline-none w-full", cut.Markup);
    }

    [Fact]
    public void Default_trigger_button_does_not_have_w_full()
    {
        var cut = _ctx.Render<L.TimePicker>();
        // The trigger button's CssClass does not include w-full unless FullWidth=true.
        var button = cut.Find("button");
        Assert.DoesNotContain("w-full", button.ClassName);
    }

    // ── FullWidth=true puts w-full on root, Popover wrapper, PopoverTrigger, trigger ──

    [Fact]
    public void FullWidth_adds_w_full_to_root_div()
    {
        var cut = _ctx.Render<L.TimePicker>(p => p.Add(c => c.FullWidth, true));
        Assert.Contains("flex flex-col items-start w-full", cut.Markup);
    }

    [Fact]
    public void FullWidth_adds_w_full_to_Popover_wrapper()
    {
        var cut = _ctx.Render<L.TimePicker>(p => p.Add(c => c.FullWidth, true));
        Assert.Contains("relative inline-block outline-none w-full", cut.Markup);
    }

    [Fact]
    public void FullWidth_adds_w_full_to_PopoverTrigger()
    {
        var cut = _ctx.Render<L.TimePicker>(p => p.Add(c => c.FullWidth, true));
        Assert.Contains("inline-flex w-full", cut.Markup);
    }

    [Fact]
    public void FullWidth_adds_w_full_to_trigger_button()
    {
        var cut = _ctx.Render<L.TimePicker>(p => p.Add(c => c.FullWidth, true));
        var button = cut.Find("button");
        Assert.Contains("w-full", button.ClassName);
    }
}
