using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.OverlayForm;

public class OverlayFormTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OverlayFormTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class EditModel
    {
        public string? Name { get; set; }
    }

    [Fact]
    public void Missing_Model_Renders_Dev_Visible_Diagnostic_Instead_Of_Empty_Shell()
    {
        // #229: a missing Model must fail non-silently — render a visible alert
        // rather than an invisible empty <div>. It must still NOT throw (the
        // contract tests render every component with default parameters).
        var cut = _ctx.Render<L.OverlayForm>();

        var alert = cut.Find("[role='alert']");
        Assert.Contains("OverlayForm", alert.TextContent);
        Assert.Contains("Model", alert.TextContent);
        // No EditForm renders without a Model.
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public void Missing_Model_Still_Forwards_AdditionalAttributes_To_Root()
    {
        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "of"
            }));

        Assert.Contains("data-testid=\"of\"", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void With_Model_Renders_EditForm_And_No_Diagnostic()
    {
        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, new EditModel())
            .Add(o => o.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<span id='body'>fields</span>"))));

        Assert.NotNull(cut.Find("form"));
        Assert.Empty(cut.FindAll("[role='alert']"));
        Assert.NotNull(cut.Find("#body"));
    }

    [Fact]
    public void Valid_Submit_Fires_Callback_When_Model_Present()
    {
        EditContext? captured = null;
        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, new EditModel())
            .Add(o => o.OnValidSubmit, (EditContext ec) => captured = ec)
            .Add(o => o.Footer, (RenderFragment)(b => b.AddMarkupContent(0, "<button type=\"submit\">Save</button>"))));

        cut.Find("form").Submit();

        Assert.NotNull(captured);
    }
}
