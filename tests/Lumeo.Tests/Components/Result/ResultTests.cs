using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Result;

public class ResultTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ResultTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Title_And_SubTitle()
    {
        var cut = _ctx.Render<Lumeo.Result>(p => p
            .Add(r => r.Title, "Success!")
            .Add(r => r.SubTitle, "Your request was processed."));

        Assert.Contains("Success!", cut.Markup);
        Assert.Contains("Your request was processed.", cut.Markup);
    }

    [Fact]
    public void Success_Status_Has_Emerald_Background()
    {
        var cut = _ctx.Render<Lumeo.Result>(p => p
            .Add(r => r.Status, Lumeo.Result.ResultStatus.Success)
            .Add(r => r.Title, "Done"));

        Assert.Contains("bg-success-light", cut.Markup);
        Assert.Contains("text-success", cut.Markup);
    }

    [Fact]
    public void Error_Status_Has_Destructive_Background()
    {
        var cut = _ctx.Render<Lumeo.Result>(p => p
            .Add(r => r.Status, Lumeo.Result.ResultStatus.Error)
            .Add(r => r.Title, "Error"));

        Assert.Contains("bg-destructive/10", cut.Markup);
    }

    [Fact]
    public void Container_Has_Py12_Px6_Classes()
    {
        var cut = _ctx.Render<Lumeo.Result>(p => p
            .Add(r => r.Title, "Result"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("py-12", cls);
        Assert.Contains("px-6", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Result>(p => p
            .Add(r => r.Class, "my-result")
            .Add(r => r.Title, "Result"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-result", cls);
    }
}
