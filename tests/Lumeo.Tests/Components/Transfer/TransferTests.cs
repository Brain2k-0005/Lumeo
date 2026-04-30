using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Transfer;

public class TransferTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TransferTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.Transfer>();
        // Two panels should be rendered
        var panels = cut.FindAll("div.flex-1");
        Assert.True(panels.Count >= 2);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.Class, "transfer-cls"));
        Assert.Contains("transfer-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.Transfer>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "transfer" }));
        Assert.Contains("data-testid=\"transfer\"", cut.Markup);
    }

    [Fact]
    public void Renders_source_items()
    {
        var sourceItems = new List<L.Transfer.TransferItem>
        {
            new("Apple", "apple"),
            new("Banana", "banana")
        };
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceItems, sourceItems));
        Assert.Contains("Apple", cut.Markup);
        Assert.Contains("Banana", cut.Markup);
    }

    [Fact]
    public void Shows_custom_source_title()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceTitle, "Available items"));
        Assert.Contains("Available items", cut.Markup);
    }
}
