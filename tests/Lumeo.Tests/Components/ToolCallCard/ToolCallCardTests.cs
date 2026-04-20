using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.ToolCallCard;

public class ToolCallCardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToolCallCardTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ToolName_Renders_In_Header()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "search_web"));

        Assert.Contains("search_web", cut.Markup);
    }

    [Fact]
    public void Renders_Details_Element_With_Summary()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x"));

        Assert.NotNull(cut.Find("details"));
        Assert.NotNull(cut.Find("summary"));
    }

    [Fact]
    public void DefaultOpen_True_Sets_Open_Attribute()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.DefaultOpen, true));

        Assert.True(cut.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void DefaultOpen_False_Does_Not_Set_Open()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.DefaultOpen, false));

        Assert.False(cut.Find("details").HasAttribute("open"));
    }

    [Theory]
    [InlineData(Lumeo.ToolCallCard.ToolCallStatus.Pending, "pending")]
    [InlineData(Lumeo.ToolCallCard.ToolCallStatus.Running, "running")]
    [InlineData(Lumeo.ToolCallCard.ToolCallStatus.Success, "success")]
    [InlineData(Lumeo.ToolCallCard.ToolCallStatus.Error, "error")]
    public void Status_Label_Appears(Lumeo.ToolCallCard.ToolCallStatus status, string expectedLabel)
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.Status, status));

        Assert.Contains(expectedLabel, cut.Markup);
    }

    [Fact]
    public void Success_Status_Has_Emerald_Color()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.Status, Lumeo.ToolCallCard.ToolCallStatus.Success));

        Assert.Contains("emerald", cut.Markup);
    }

    [Fact]
    public void Error_Status_Has_Destructive_Color()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.Status, Lumeo.ToolCallCard.ToolCallStatus.Error));

        Assert.Contains("destructive", cut.Markup);
    }

    [Fact]
    public void Input_Renders_In_Pre_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.Input, "{\"q\":\"test\"}"));

        var pres = cut.FindAll("pre");
        Assert.NotEmpty(pres);
        Assert.Contains("\"q\":\"test\"", cut.Markup);
    }

    [Fact]
    public void Output_Renders_In_Pre_When_Provided_And_Not_Error()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.Status, Lumeo.ToolCallCard.ToolCallStatus.Success)
            .Add(c => c.Output, "result-data"));

        Assert.Contains("result-data", cut.Markup);
        Assert.NotEmpty(cut.FindAll("pre"));
    }

    [Fact]
    public void Error_Status_Shows_Error_Message_Instead_Of_Output()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.Status, Lumeo.ToolCallCard.ToolCallStatus.Error)
            .Add(c => c.ErrorMessage, "boom")
            .Add(c => c.Output, "not shown"));

        Assert.Contains("boom", cut.Markup);
        Assert.DoesNotContain("not shown", cut.Markup);
    }

    [Fact]
    public void DurationMs_Under_Thousand_Renders_Ms_Suffix()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.DurationMs, 250L));

        Assert.Contains("250ms", cut.Markup);
    }

    [Fact]
    public void DurationMs_Above_Thousand_Renders_Seconds()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.DurationMs, 2500L));

        Assert.Contains("2.5s", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.Class, "tcc-x"));

        Assert.Contains("tcc-x", cut.Find("details").GetAttribute("class"));
    }
}
