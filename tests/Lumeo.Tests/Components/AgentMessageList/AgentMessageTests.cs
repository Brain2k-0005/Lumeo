using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AgentMessageList;

public class AgentMessageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AgentMessageTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void User_Role_Aligns_To_End()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.User)
            .AddChildContent("Hi"));

        Assert.Contains("justify-end", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void User_Role_Renders_Primary_Bubble()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.User)
            .AddChildContent("Hi"));

        // bubble div has bg-primary
        var bubble = cut.FindAll("div").FirstOrDefault(d => (d.GetAttribute("class") ?? "").Contains("bg-primary"));
        Assert.NotNull(bubble);
    }

    [Fact]
    public void Assistant_Role_Aligns_Items_Start()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.Assistant)
            .AddChildContent("Hello"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("items-start", cls);
        Assert.DoesNotContain("justify-end", cls);
    }

    [Fact]
    public void System_Role_Uses_Centered_Muted_Chip()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.System)
            .AddChildContent("system msg"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("mx-auto", cls);
        Assert.Contains("rounded-full", cls);
        Assert.Contains("italic", cls);
    }

    [Fact]
    public void Tool_Role_Renders_Full_Width_Wrapper()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.Tool)
            .AddChildContent("tool"));

        Assert.Contains("w-full", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Name_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.Assistant)
            .Add(m => m.Name, "Claude")
            .AddChildContent("hi"));

        Assert.Contains("Claude", cut.Markup);
    }

    [Fact]
    public void Name_Not_Rendered_When_Not_Provided()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.Assistant)
            .AddChildContent("hi"));

        Assert.DoesNotContain("Claude", cut.Markup);
    }

    [Fact]
    public void Timestamp_Renders_When_Provided()
    {
        var ts = new DateTimeOffset(2026, 4, 20, 14, 30, 0, TimeSpan.Zero);
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.Assistant)
            .Add(m => m.Timestamp, ts)
            .AddChildContent("hi"));

        // The local time may shift based on timezone; just check a digit colon digit pattern exists
        Assert.Matches(@"\d{2}:\d{2}", cut.Markup);
    }

    [Fact]
    public void IsStreaming_Renders_Caret_For_Assistant()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.Assistant)
            .Add(m => m.IsStreaming, true)
            .AddChildContent("hi"));

        Assert.Contains("animate-pulse", cut.Markup);
    }

    [Fact]
    public void IsStreaming_Renders_Caret_For_User()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.User)
            .Add(m => m.IsStreaming, true)
            .AddChildContent("hi"));

        Assert.Contains("animate-pulse", cut.Markup);
    }

    [Fact]
    public void ChildContent_Renders()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.Assistant)
            .AddChildContent("payload text"));

        Assert.Contains("payload text", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Lumeo.AgentMessage.AgentMessageRole.Assistant)
            .Add(m => m.Class, "am-x")
            .AddChildContent("x"));

        Assert.Contains("am-x", cut.Find("div").GetAttribute("class"));
    }
}
