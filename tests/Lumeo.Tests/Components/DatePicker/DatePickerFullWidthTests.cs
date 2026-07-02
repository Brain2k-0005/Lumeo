using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Tests for B2 fix: DatePicker FullWidth parameter and inner input min-w-0.
/// </summary>
public class DatePickerFullWidthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerFullWidthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── back-compat: default renders WITHOUT w-full on wrappers ──────────────

    [Fact]
    public void Default_root_div_does_not_have_w_full()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(c => c.AllowKeyboardInput, false));
        Assert.DoesNotContain("flex flex-col items-start w-full", cut.Markup);
    }

    [Fact]
    public void Default_Popover_wrapper_does_not_have_w_full()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(c => c.AllowKeyboardInput, false));
        Assert.DoesNotContain("relative inline-block outline-none w-full", cut.Markup);
    }

    [Fact]
    public void Default_PopoverTrigger_does_not_have_w_full()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(c => c.AllowKeyboardInput, false));
        Assert.DoesNotContain("inline-flex w-full", cut.Markup);
    }

    // ── FullWidth=true puts w-full on root, Popover wrapper, PopoverTrigger ──

    [Fact]
    public void FullWidth_adds_w_full_to_root_div()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, false)
            .Add(c => c.FullWidth, true));
        Assert.Contains("flex flex-col items-start w-full", cut.Markup);
    }

    [Fact]
    public void FullWidth_adds_w_full_to_Popover_wrapper()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, false)
            .Add(c => c.FullWidth, true));
        Assert.Contains("relative inline-block outline-none w-full", cut.Markup);
    }

    [Fact]
    public void FullWidth_adds_w_full_to_PopoverTrigger()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.AllowKeyboardInput, false)
            .Add(c => c.FullWidth, true));
        Assert.Contains("inline-flex w-full", cut.Markup);
    }

    // ── inner input always has min-w-0 regardless of FullWidth ───────────────

    [Fact]
    public void Inner_input_has_min_w_0_by_default()
    {
        // AllowKeyboardInput=true is the default; renders the typeable input trigger.
        var cut = _ctx.Render<L.DatePicker>();
        var input = cut.Find("input");
        Assert.Contains("min-w-0", input.ClassName);
    }

    [Fact]
    public void Inner_input_has_min_w_0_when_FullWidth()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(c => c.FullWidth, true));
        var input = cut.Find("input");
        Assert.Contains("min-w-0", input.ClassName);
    }
}
