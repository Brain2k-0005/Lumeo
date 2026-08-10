using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ToggleGroup;

// Pins all 7 Lumeo.Size rungs (batch B, spec §17) for ToggleGroupItem.SizeClasses
// (cascaded from ToggleGroup.Size via ToggleGroupContext). Asserts the RENDERED
// class attribute as an exact token (not a bare substring — e.g. "h-1" is a
// substring of "h-11"/"h-12" — so a loose Contains could silently pass a wrong
// value), per the task brief's note that Cx.Merge's argument order has bitten
// this repo twice.
public class ToggleGroupItemSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToggleGroupItemSizeScaleTests()
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

    private IRenderedComponent<IComponent> RenderToggleGroup(L.Size size)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ToggleGroup>(0);
            builder.AddAttribute(1, "Type", L.ToggleGroup.ToggleGroupType.Single);
            builder.AddAttribute(2, "Size", size);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ToggleGroupItem>(0);
                b.AddAttribute(1, "Value", "a");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Theory]
    [InlineData(L.Size.Xxs, "h-6", "px-0")]
    [InlineData(L.Size.Xs, "h-7", "px-1")]
    [InlineData(L.Size.Sm, "h-8", "px-2")]
    [InlineData(L.Size.Md, "h-9", "px-3")]
    [InlineData(L.Size.Lg, "h-10", "px-4")]
    [InlineData(L.Size.Xl, "h-11", "px-5")]
    [InlineData(L.Size.Xxl, "h-12", "px-6")]
    public void Item_Renders_Correct_Size_Classes(L.Size size, string heightClass, string paddingClass)
    {
        var cut = RenderToggleGroup(size);

        AssertHasClasses(cut.Find("button").GetAttribute("class"), heightClass, paddingClass);
    }
}
