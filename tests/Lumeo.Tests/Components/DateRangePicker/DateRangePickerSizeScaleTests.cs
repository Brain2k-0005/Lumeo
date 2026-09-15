using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateRangePicker;

/// <summary>
/// Field report #464 (carried): DateRangePicker had no Size lever. DateRangePicker is a
/// thin forwarding wrapper around <see cref="L.DatePicker"/> (Mode=Range) — this pins
/// that the new <c>Size</c> parameter actually reaches the inner DatePicker's trigger
/// rather than being dropped on the way through. Full per-rung height/text/padding
/// coverage and Select-trigger parity live in DatePickerSizeScaleTests.cs.
/// </summary>
public class DateRangePickerSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DateRangePickerSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertHasClass(string? cls, string token)
    {
        var tokens = (cls ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(token, tokens);
    }

    [Fact]
    public void Default_Size_Is_Md()
    {
        var cut = _ctx.Render<L.DateRangePicker>();
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, "h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]");
        AssertHasClass(cls, "px-2.5");
    }

    [Theory]
    [InlineData(L.Size.Xs, "h-7", "px-2", "text-[10px]")]
    [InlineData(L.Size.Lg, "h-11", "px-4", "text-base")]
    [InlineData(L.Size.Xxl, "h-[60px]", "px-6", "text-xl")]
    public void Size_Forwards_To_Inner_DatePicker_Trigger(L.Size size, string h, string px, string textClass)
    {
        var cut = _ctx.Render<L.DateRangePicker>(p => p.Add(d => d.Size, size));
        var cls = cut.Find("button").GetAttribute("class");
        AssertHasClass(cls, h);
        AssertHasClass(cls, px);
        AssertHasClass(cls, textClass);
    }
}
