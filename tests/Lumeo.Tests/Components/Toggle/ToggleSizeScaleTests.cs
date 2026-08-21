using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Toggle;

// Pins all 7 Lumeo.Size rungs x 3 Density arms (batch B, spec §16) for
// Toggle.SizeClasses. Asserts the RENDERED class attribute as an exact
// token (not a bare substring — e.g. "px-1" is a substring of "px-1.5",
// "h-1" is a substring of "h-11"/"h-14" — so a loose Contains could
// silently pass a wrong value), per the task brief's note that Cx.Merge's
// argument order has bitten this repo twice.
/// The Sm/Md/Lg rungs moved down one step in the 5.0 scale alignment - shadcn's toggle is
/// h-8 px-2.5 at the default rung, h-7 small, h-9 large, with one padding value across all
/// three. The rungs above and below are untouched.
public class ToggleSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToggleSizeScaleTests()
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

    // Comfortable (default, Density left unset)
    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-6", "px-0")]
    [InlineData(Lumeo.Size.Xs, "h-7", "px-1")]
    [InlineData(Lumeo.Size.Sm, "h-7", "px-2.5")]
    [InlineData(Lumeo.Size.Md, "h-8", "px-2.5")]
    [InlineData(Lumeo.Size.Lg, "h-9", "px-2.5")]
    [InlineData(Lumeo.Size.Xl, "h-11", "px-5")]
    [InlineData(Lumeo.Size.Xxl, "h-12", "px-6")]
    public void Comfortable_Renders_Correct_Size_Classes(Lumeo.Size size, string heightClass, string paddingClass)
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Size, size)
            .AddChildContent("B"));

        AssertHasClasses(cut.Find("button").GetAttribute("class"), heightClass, paddingClass);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-5", "px-0.5")]
    [InlineData(Lumeo.Size.Xs, "h-6", "px-1")]
    [InlineData(Lumeo.Size.Sm, "h-6", "px-2")]
    [InlineData(Lumeo.Size.Md, "h-7", "px-2")]
    [InlineData(Lumeo.Size.Lg, "h-8", "px-2")]
    [InlineData(Lumeo.Size.Xl, "h-10", "px-4")]
    [InlineData(Lumeo.Size.Xxl, "h-11", "px-5")]
    public void Compact_Renders_Correct_Size_Classes(Lumeo.Size size, string heightClass, string paddingClass)
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Size, size)
            .Add(b => b.Density, Lumeo.Density.Compact)
            .AddChildContent("B"));

        AssertHasClasses(cut.Find("button").GetAttribute("class"), heightClass, paddingClass);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-5", "px-1")]
    [InlineData(Lumeo.Size.Xs, "h-7", "px-2")]
    [InlineData(Lumeo.Size.Sm, "h-8", "px-3")]
    [InlineData(Lumeo.Size.Md, "h-9", "px-3")]
    [InlineData(Lumeo.Size.Lg, "h-10", "px-3")]
    [InlineData(Lumeo.Size.Xl, "h-[52px]", "px-6")]
    [InlineData(Lumeo.Size.Xxl, "h-14", "px-7")]
    public void Spacious_Renders_Correct_Size_Classes(Lumeo.Size size, string heightClass, string paddingClass)
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Size, size)
            .Add(b => b.Density, Lumeo.Density.Spacious)
            .AddChildContent("B"));

        AssertHasClasses(cut.Find("button").GetAttribute("class"), heightClass, paddingClass);
    }
}
