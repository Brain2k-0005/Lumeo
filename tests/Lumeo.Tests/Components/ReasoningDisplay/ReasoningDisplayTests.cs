using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.ReasoningDisplay;

public class ReasoningDisplayTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ReasoningDisplayTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Details_Element()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>();

        Assert.NotNull(cut.Find("details"));
    }

    [Fact]
    public void Default_Summary_When_Not_Streaming_Is_Reasoning()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>();

        Assert.Contains("Reasoning", cut.Markup);
    }

    [Fact]
    public void Default_Summary_When_Streaming_Is_Thinking()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.IsStreaming, true));

        Assert.Contains("Thinking", cut.Markup);
    }

    [Fact]
    public void DurationMs_Renders_Reasoned_For_Summary()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.DurationMs, 2500L));

        Assert.Contains("Reasoned for", cut.Markup);
        Assert.Contains("2.5", cut.Markup);
    }

    [Fact]
    public void Custom_Summary_Overrides_Default()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.Summary, "Custom label"));

        Assert.Contains("Custom label", cut.Markup);
    }

    [Fact]
    public void Text_Renders_Inside_Body()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.Text, "Step 1: do this."));

        Assert.Contains("Step 1: do this.", cut.Markup);
    }

    [Fact]
    public void DefaultOpen_True_Sets_Open_Attribute()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.DefaultOpen, true));

        Assert.True(cut.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void DefaultOpen_False_Does_Not_Set_Open()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.DefaultOpen, false));

        Assert.False(cut.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void IsStreaming_Adds_Pulse_Indicator()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.IsStreaming, true));

        Assert.Contains("animate-pulse", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.Class, "rd-x"));

        Assert.Contains("rd-x", cut.Find("details").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "rd"
            }));

        Assert.Equal("rd", cut.Find("details").GetAttribute("data-testid"));
    }
}
