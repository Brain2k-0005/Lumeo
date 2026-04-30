using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InplaceEditor;

public class InplaceEditorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InplaceEditorTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default_display_mode()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p.Add(c => c.Value, "Hello world"));
        Assert.Contains("Hello world", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p.Add(c => c.Class, "inplace-cls"));
        Assert.Contains("inplace-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "inplace" }));
        Assert.Contains("data-testid=\"inplace\"", cut.Markup);
    }

    [Fact]
    public void Shows_placeholder_when_no_value()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p.Add(c => c.Placeholder, "Click to edit"));
        Assert.Contains("Click to edit", cut.Markup);
    }

    [Fact]
    public void Clicking_display_enters_edit_mode()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p.Add(c => c.Value, "Original text"));
        // In display mode, clicking the display div should enter edit mode
        var displayDiv = cut.FindAll("div").FirstOrDefault(d =>
            (d.GetAttribute("class") ?? "").Contains("cursor-pointer"));
        Assert.NotNull(displayDiv);
        displayDiv!.Click();
        // After click, an input or textarea should appear
        Assert.True(cut.FindAll("input[type='text']").Count > 0 ||
                    cut.FindAll("textarea").Count > 0);
    }
}
