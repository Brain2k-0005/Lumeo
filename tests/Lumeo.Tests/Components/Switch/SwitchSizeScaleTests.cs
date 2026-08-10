using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

// Pins all 7 Lumeo.Size rungs (batch B, spec §15) across the four Size-driven
// switch expressions: TrackSizeClasses (button), ThumbSizeClasses +
// ThumbTranslateClass (thumb span), ThumbSpinnerClass (Loading spinner
// wrapper). Asserts the RENDERED class attribute as an exact token (not a
// bare substring — e.g. "h-1" is a substring of "h-11", "px-1" is a
// substring of "px-1.5" — so a loose Contains could silently pass a wrong
// value), per the task brief's note that Cx.Merge's argument order has
// bitten this repo twice.
public class SwitchSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchSizeScaleTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertHasClasses(string? cls, params string[] tokens)
    {
        var actual = (cls ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
            Assert.Contains(token, actual);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-2", "w-3")]
    [InlineData(Lumeo.Size.Xs, "h-3", "w-5")]
    [InlineData(Lumeo.Size.Sm, "h-4", "w-7")]
    [InlineData(Lumeo.Size.Md, "h-5", "w-9")]
    [InlineData(Lumeo.Size.Lg, "h-6", "w-11")]
    [InlineData(Lumeo.Size.Xl, "h-7", "w-[52px]")]
    [InlineData(Lumeo.Size.Xxl, "h-8", "w-[60px]")]
    public void Track_Renders_Correct_Size_Classes(Lumeo.Size size, string heightClass, string widthClass)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Size, size));

        AssertHasClasses(cut.Find("button").GetAttribute("class"), heightClass, widthClass);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-1", "w-1")]
    [InlineData(Lumeo.Size.Xs, "h-2", "w-2")]
    [InlineData(Lumeo.Size.Sm, "h-3", "w-3")]
    [InlineData(Lumeo.Size.Md, "h-4", "w-4")]
    [InlineData(Lumeo.Size.Lg, "h-5", "w-5")]
    [InlineData(Lumeo.Size.Xl, "h-6", "w-6")]
    [InlineData(Lumeo.Size.Xxl, "h-7", "w-7")]
    public void Thumb_Renders_Correct_Size_Classes(Lumeo.Size size, string heightClass, string widthClass)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Size, size));

        AssertHasClasses(cut.Find("span").GetAttribute("class"), heightClass, widthClass);
    }

    // Formula-derived (spec §15b/§0.8): translate = trackW − thumbW − 4px border.
    [Theory]
    [InlineData(Lumeo.Size.Xxs, "translate-x-1")]
    [InlineData(Lumeo.Size.Xs, "translate-x-2")]
    [InlineData(Lumeo.Size.Sm, "translate-x-3")]
    [InlineData(Lumeo.Size.Md, "translate-x-4")]
    [InlineData(Lumeo.Size.Lg, "translate-x-5")]
    [InlineData(Lumeo.Size.Xl, "translate-x-6")]
    [InlineData(Lumeo.Size.Xxl, "translate-x-7")]
    public void Checked_Thumb_Renders_Correct_Translate_Class(Lumeo.Size size, string translateClass)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Size, size)
            .Add(b => b.Checked, true));

        AssertHasClasses(cut.Find("span").GetAttribute("class"), translateClass);
    }

    // Xxs/Xs both floor to !h-1 !w-1 (spec §15b flags the tie — the formula
    // value floors to 0 at Xxs).
    [Theory]
    [InlineData(Lumeo.Size.Xxs, "!h-1", "!w-1")]
    [InlineData(Lumeo.Size.Xs, "!h-1", "!w-1")]
    [InlineData(Lumeo.Size.Sm, "!h-2", "!w-2")]
    [InlineData(Lumeo.Size.Md, "!h-3", "!w-3")]
    [InlineData(Lumeo.Size.Lg, "!h-3.5", "!w-3.5")]
    [InlineData(Lumeo.Size.Xl, "!h-5", "!w-5")]
    [InlineData(Lumeo.Size.Xxl, "!h-6", "!w-6")]
    public void Loading_Spinner_Renders_Correct_Size_Classes(Lumeo.Size size, string heightClass, string widthClass)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Size, size)
            .Add(b => b.Loading, true));

        var spinner = cut.Find("button [role=\"status\"]");
        AssertHasClasses(spinner.GetAttribute("class"), heightClass, widthClass);
    }
}
