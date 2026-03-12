using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.BackToTop;

public class BackToTopTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BackToTopTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Does_Not_Render_Button_By_Default()
    {
        // BackToTop only renders button when _visible is true,
        // which requires JS scroll event — it starts hidden
        var cut = _ctx.Render<Lumeo.BackToTop>();

        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Renders_Without_Throwing()
    {
        var exception = Record.Exception(() =>
            _ctx.Render<Lumeo.BackToTop>());

        Assert.Null(exception);
    }

    [Fact]
    public void Renders_Without_Throwing_With_Custom_Threshold()
    {
        var exception = Record.Exception(() =>
            _ctx.Render<Lumeo.BackToTop>(p => p
                .Add(b => b.VisibilityThreshold, 500)));

        Assert.Null(exception);
    }
}
