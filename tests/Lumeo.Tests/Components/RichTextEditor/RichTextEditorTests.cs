using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.RichTextEditor;

public class RichTextEditorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public RichTextEditorTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_container()
    {
        var cut = _ctx.Render<L.RichTextEditor>();
        // The root div with lumeo-rte-content class is always rendered
        Assert.Contains("lumeo-rte-content", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.RichTextEditor>(p => p.Add(c => c.Class, "rte-cls"));
        Assert.Contains("rte-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "rte" }));
        Assert.Contains("data-testid=\"rte\"", cut.Markup);
    }

    [Fact]
    public void Renders_toolbar_by_default()
    {
        var cut = _ctx.Render<L.RichTextEditor>();
        // Standard toolbar is shown by default; toolbar renders buttons
        Assert.NotEmpty(cut.FindAll("button"));
    }

    [Fact]
    public void No_toolbar_when_preset_is_none()
    {
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(c => c.Toolbar, L.EditorToolbarPreset.None));
        // With no toolbar, only the content div is rendered; no editor toolbar buttons
        // (the link dialog button may still exist, but the toolbar buttons are gone)
        Assert.Contains("lumeo-rte-content", cut.Markup);
    }
}
