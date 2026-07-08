using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// GDPR / EDPB: the first-layer Reject and Accept actions must have EQUAL visual
/// weight so the banner never nudges toward accepting. Asserts both buttons render
/// with the identical class attribute and that neither uses the primary accent —
/// which fails against the old markup (Accept was <c>bg-primary</c>, Reject a
/// bordered/outline button).
/// </summary>
public class ConsentBannerEqualButtonsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerEqualButtonsTests()
    {
        _ctx.AddLumeoServices();
        // ConsentBanner @injects ConsentService by concrete type.
        _ctx.Services.AddScoped<ConsentService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement ButtonWithText(IRenderedComponent<L.ConsentBanner> cut, string text)
        => cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    [Fact]
    public void Layer1_Reject_And_Accept_Have_Equal_Visual_Weight()
    {
        var cut = _ctx.Render<L.ConsentBanner>();

        cut.WaitForAssertion(() =>
        {
            var rejectClass = ButtonWithText(cut, "Reject optional").GetAttribute("class");
            var acceptClass = ButtonWithText(cut, "Accept all").GetAttribute("class");

            Assert.False(string.IsNullOrWhiteSpace(acceptClass));
            // Same size + variant → same class string.
            Assert.Equal(rejectClass, acceptClass);
            // Neither action may carry the primary accent that would privilege one over the other.
            Assert.DoesNotContain("bg-primary", acceptClass);
            Assert.DoesNotContain("bg-primary", rejectClass);
        });
    }
}
