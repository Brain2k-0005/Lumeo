using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Descriptions;

/// <summary>
/// Regression: DescriptionsItem emits &lt;dt&gt;/&lt;dd&gt; pairs, but the
/// Descriptions grid container was a plain &lt;div&gt; — invalid HTML (dt/dd
/// require a &lt;dl&gt; ancestor; div group wrappers inside a dl are valid).
/// The grid container now renders as &lt;dl&gt; with the grid classes intact.
/// </summary>
public class DescriptionsDlTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DescriptionsDlTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment OneItem() => b =>
    {
        b.OpenComponent<Lumeo.DescriptionsItem>(0);
        b.AddAttribute(1, "Label", "Status");
        b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Active")));
        b.CloseComponent();
    };

    [Fact]
    public void Grid_Container_Is_Dl_With_Grid_Classes()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.ChildContent, OneItem()));

        var dl = cut.Find("dl");
        var cls = dl.GetAttribute("class") ?? "";
        Assert.Contains("grid", cls);
        Assert.Contains("grid-cols-3", cls);
    }

    [Fact]
    public void Bordered_Grid_Container_Is_Dl()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.Bordered, true)
            .Add(d => d.ChildContent, OneItem()));

        var dl = cut.Find("dl");
        Assert.Contains("divide-x", dl.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Dt_And_Dd_Have_Dl_Ancestor()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.ChildContent, OneItem()));

        Assert.NotNull(cut.Find("dl dt"));
        Assert.NotNull(cut.Find("dl dd"));
        Assert.Equal("Status", cut.Find("dt").TextContent);
        Assert.Equal("Active", cut.Find("dd").TextContent.Trim());
    }
}
