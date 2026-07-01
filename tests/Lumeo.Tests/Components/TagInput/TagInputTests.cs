using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using TagInputCmp = global::Lumeo.TagInput<string>;

namespace Lumeo.Tests.Components.TagInput;

public class TagInputTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TagInputTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Input_Element()
    {
        var cut = _ctx.Render<TagInputCmp>();

        Assert.NotNull(cut.Find("input[type='text']"));
    }

    [Fact]
    public void Container_Has_Border_And_RoundedMd_Classes()
    {
        var cut = _ctx.Render<TagInputCmp>();

        var cls = cut.Find("div.rounded-md").GetAttribute("class");
        Assert.Contains("rounded-md", cls);
        Assert.Contains("border", cls);
    }

    [Fact]
    public void Tags_Are_Rendered_As_Spans()
    {
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.Tags, new List<string> { "alpha", "beta" }));

        var spans = cut.FindAll("span.inline-flex");
        Assert.Equal(2, spans.Count);
        Assert.Contains("alpha", cut.Markup);
        Assert.Contains("beta", cut.Markup);
    }

    [Fact]
    public void Placeholder_Is_Set_On_Input()
    {
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.Placeholder, "Type here..."));

        var input = cut.Find("input");
        Assert.Equal("Type here...", input.GetAttribute("placeholder"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.Class, "my-tag-input"));

        var cls = cut.Find("div.rounded-md").GetAttribute("class");
        Assert.Contains("my-tag-input", cls);
    }
}
