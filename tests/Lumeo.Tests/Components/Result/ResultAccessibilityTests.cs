using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Result;

// Regression: wave3-53 — the default status glyph sits inside the root
// role="status"/role="alert" live region. It is decorative, so it must be
// aria-hidden, otherwise AT announces a redundant icon label alongside the
// Title/SubTitle. Without the fix the icon emits no aria-hidden attribute.
public class ResultAccessibilityTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ResultAccessibilityTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Theory]
    [InlineData(Lumeo.Result.ResultStatus.Success)]
    [InlineData(Lumeo.Result.ResultStatus.Error)]
    [InlineData(Lumeo.Result.ResultStatus.Warning)]
    [InlineData(Lumeo.Result.ResultStatus.Info)]
    [InlineData(Lumeo.Result.ResultStatus.NotFound)]
    [InlineData(Lumeo.Result.ResultStatus.Forbidden)]
    [InlineData(Lumeo.Result.ResultStatus.ServerError)]
    public void DefaultStatusIcon_IsAriaHidden(Lumeo.Result.ResultStatus status)
    {
        var cut = _ctx.Render<Lumeo.Result>(p => p
            .Add(r => r.Status, status)
            .Add(r => r.Title, "Outcome"));

        // The only <svg> rendered is the decorative default status glyph.
        var icon = cut.Find("svg");
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void DefaultStatusIcon_InAlertLiveRegion_IsAriaHidden()
    {
        // Error is an assertive role="alert" region — a non-hidden decorative
        // icon would be announced redundantly when the alert fires.
        var cut = _ctx.Render<Lumeo.Result>(p => p
            .Add(r => r.Status, Lumeo.Result.ResultStatus.Error)
            .Add(r => r.Title, "Something went wrong"));

        Assert.Equal("alert", cut.Find("div").GetAttribute("role"));
        Assert.Equal("true", cut.Find("svg").GetAttribute("aria-hidden"));
    }
}
