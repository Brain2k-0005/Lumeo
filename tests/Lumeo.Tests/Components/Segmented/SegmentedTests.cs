using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Segmented;

public class SegmentedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SegmentedTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<Lumeo.Segmented.SegmentedOption> CreateOptions() =>
    [
        new() { Label = "Day", Value = "day" },
        new() { Label = "Week", Value = "week" },
        new() { Label = "Month", Value = "month" }
    ];

    [Fact]
    public void Renders_Options_As_Buttons()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions()));

        var buttons = cut.FindAll("button[role='radio']");
        Assert.Equal(3, buttons.Count);
    }

    [Fact]
    public void Container_Has_RadioGroup_Role()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions()));

        Assert.Equal("radiogroup", cut.Find("div").GetAttribute("role"));
    }

    [Fact]
    public void Active_Option_Has_AriaChecked_True()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions())
            .Add(s => s.Value, "week"));

        var weekButton = cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Week"));
        Assert.Equal("true", weekButton.GetAttribute("aria-checked"));
    }

    [Fact]
    public void Block_True_Adds_FullWidth_Class()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Options, CreateOptions())
            .Add(s => s.Block, true));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Segmented>(p => p
            .Add(s => s.Class, "my-segmented"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-segmented", cls);
    }
}
