using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Mention;

public class MentionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MentionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.Mention>();
        Assert.NotEmpty(cut.FindAll("textarea"));
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.Mention>(p => p.Add(c => c.Class, "mention-cls"));
        var textarea = cut.Find("textarea");
        Assert.Contains("mention-cls", textarea.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.Mention>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "mention-area" }));
        Assert.Contains("data-testid=\"mention-area\"", cut.Markup);
    }

    [Fact]
    public void Shows_placeholder_when_set()
    {
        var cut = _ctx.Render<L.Mention>(p => p.Add(c => c.Placeholder, "Type @someone"));
        var textarea = cut.Find("textarea");
        Assert.Equal("Type @someone", textarea.GetAttribute("placeholder"));
    }

    [Fact]
    public void Renders_as_disabled_when_disabled()
    {
        var cut = _ctx.Render<L.Mention>(p => p.Add(c => c.Disabled, true));
        var textarea = cut.Find("textarea");
        Assert.NotNull(textarea.GetAttribute("disabled"));
    }
}
