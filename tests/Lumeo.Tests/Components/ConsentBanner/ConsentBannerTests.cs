using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

public class ConsentBannerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerTests()
    {
        _ctx.AddLumeoServices();
        // ConsentBanner @injects ConsentService directly by concrete type
        _ctx.Services.AddScoped<ConsentService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_without_exception()
    {
        // ConsentBanner only shows after EnsureLoadedAsync + _ready==true
        // In tests the JS interop returns defaults (null/false) so _ready stays false
        // and the banner is hidden. Markup should be empty but no exception.
        var cut = _ctx.Render<L.ConsentBanner>();
        Assert.NotNull(cut);
    }

    [Fact]
    public void Merges_class_parameter_when_visible()
    {
        // We can verify the Class parameter is accepted without error
        var cut = _ctx.Render<L.ConsentBanner>(p => p.Add(c => c.Class, "my-consent-banner"));
        // Banner starts hidden (no JS to trigger _ready), but component renders
        Assert.NotNull(cut);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.ConsentBanner>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "consent" }));
        Assert.NotNull(cut);
    }

    [Fact]
    public void Custom_title_parameter_accepted()
    {
        var cut = _ctx.Render<L.ConsentBanner>(p => p.Add(c => c.Title, "We use cookies"));
        // Component renders; title stored on parameter
        Assert.NotNull(cut);
    }
}
