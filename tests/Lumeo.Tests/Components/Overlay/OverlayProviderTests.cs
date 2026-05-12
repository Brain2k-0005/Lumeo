using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.Overlay;

public class OverlayProviderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OverlayProviderTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Without_Overlays_On_Startup()
    {
        // OverlayProvider should render an empty container when no overlay has been shown
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        // No dialogs, sheets, or drawers expected
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void Two_AlertDialog_Overlays_Both_Render_With_Distinct_Titles()
    {
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        // Show two alert dialogs via the service
        _ = service.ShowAlertDialogAsync(new AlertDialogOptions
        {
            Title = "Confirm Delete",
            Description = "This action cannot be undone."
        });
        _ = service.ShowAlertDialogAsync(new AlertDialogOptions
        {
            Title = "Confirm Archive",
            Description = "Items will be archived."
        });

        cut.WaitForState(() => cut.Markup.Contains("Confirm Delete") && cut.Markup.Contains("Confirm Archive"));

        Assert.Contains("Confirm Delete", cut.Markup);
        Assert.Contains("Confirm Archive", cut.Markup);
    }
}
