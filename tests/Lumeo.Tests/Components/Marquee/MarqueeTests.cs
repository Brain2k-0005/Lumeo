using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Marquee;

public class MarqueeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MarqueeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Root_Div_With_Marquee_Class()
    {
        var cut = _ctx.Render<Lumeo.Marquee>();

        Assert.Contains("lumeo-marquee", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_Two_Track_Children()
    {
        var cut = _ctx.Render<Lumeo.Marquee>(p => p
            .AddChildContent("<span>item</span>"));

        var tracks = cut.FindAll(".lumeo-marquee-track");
        Assert.Equal(2, tracks.Count);
    }

    [Fact]
    public void Default_Direction_Is_Left()
    {
        var cut = _ctx.Render<Lumeo.Marquee>();

        Assert.Equal("left", cut.Find("div").GetAttribute("data-direction"));
    }

    [Fact]
    public void Direction_Right_Sets_Data_Attribute()
    {
        var cut = _ctx.Render<Lumeo.Marquee>(p => p
            .Add(m => m.Direction, Lumeo.Marquee.MarqueeDirection.Right));

        Assert.Equal("right", cut.Find("div").GetAttribute("data-direction"));
    }

    [Fact]
    public void Reverse_Inverts_Direction()
    {
        var cut = _ctx.Render<Lumeo.Marquee>(p => p
            .Add(m => m.Direction, Lumeo.Marquee.MarqueeDirection.Left)
            .Add(m => m.Reverse, true));

        Assert.Equal("right", cut.Find("div").GetAttribute("data-direction"));
    }

    [Fact]
    public void Vertical_True_Sets_Data_Attribute()
    {
        var cut = _ctx.Render<Lumeo.Marquee>(p => p
            .Add(m => m.Vertical, true));

        Assert.Equal("true", cut.Find("div").GetAttribute("data-vertical"));
    }

    [Fact]
    public void PauseOnHover_True_Sets_Data_Attribute()
    {
        var cut = _ctx.Render<Lumeo.Marquee>(p => p
            .Add(m => m.PauseOnHover, true));

        Assert.Equal("true", cut.Find("div").GetAttribute("data-pause-hover"));
    }

    [Theory]
    [InlineData(Lumeo.Marquee.MarqueeSpeed.Slow, "60s")]
    [InlineData(Lumeo.Marquee.MarqueeSpeed.Normal, "30s")]
    [InlineData(Lumeo.Marquee.MarqueeSpeed.Fast, "12s")]
    public void Speed_Maps_To_Duration_Variable(Lumeo.Marquee.MarqueeSpeed speed, string expectedDuration)
    {
        var cut = _ctx.Render<Lumeo.Marquee>(p => p
            .Add(m => m.Speed, speed));

        var style = cut.Find("div").GetAttribute("style");
        Assert.Contains($"--lumeo-marquee-duration: {expectedDuration}", style);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Marquee>(p => p
            .Add(m => m.Class, "m-x"));

        Assert.Contains("m-x", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.Marquee>(p => p
            .Add(m => m.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "marquee"
            }));

        Assert.Equal("marquee", cut.Find("div").GetAttribute("data-testid"));
    }

    // Regression (battle-wave3 #13, keyboard-a11y): the duplicated marquee track is
    // aria-hidden, but without `inert` its focusable children stay in the tab order,
    // causing a double-tab and an aria-hidden-focus violation. The clone must be inert.
    [Fact]
    public void Duplicated_Track_Is_Inert_To_Remove_Clones_From_Tab_Order()
    {
        var cut = _ctx.Render<Lumeo.Marquee>(p => p
            .AddChildContent("<a href=\"#\">link</a>"));

        var tracks = cut.FindAll(".lumeo-marquee-track");
        Assert.Equal(2, tracks.Count);

        // The aria-hidden clone is the second track.
        var clone = tracks[1];
        Assert.Equal("true", clone.GetAttribute("aria-hidden"));
        Assert.True(clone.HasAttribute("inert"));

        // The visible track must remain interactive (not inert).
        Assert.False(tracks[0].HasAttribute("inert"));
    }
}
