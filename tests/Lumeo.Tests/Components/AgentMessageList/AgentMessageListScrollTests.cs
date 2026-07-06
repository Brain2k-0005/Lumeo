using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AgentMessageList;

/// <summary>
/// Wave 5 — AgentMessageList ConversationScrollButton + ConversationEmptyState +
/// messages-to-Markdown export (shadcn Conversation parity).
/// </summary>
public class AgentMessageListScrollTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AgentMessageListScrollTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── Scroll-to-latest button ──────────────────────────────────────────────

    [Fact]
    public void ScrollButton_Hidden_Until_Scrolled_Away()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .AddChildContent("<span>m</span>"));

        Assert.Empty(cut.FindAll("[data-testid='agent-scroll-button']"));
    }

    [Fact]
    public void ScrollButton_Appears_When_Observer_Reports_Scrolled_Away()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .AddChildContent("<span>m</span>"));

        // Simulate the JS observer firing (list scrolled up).
        cut.InvokeAsync(() => cut.Instance.OnScrollAwayChanged(true));

        Assert.NotNull(cut.Find("[data-testid='agent-scroll-button']"));
    }

    [Fact]
    public void ScrollButton_Click_Scrolls_To_Bottom_And_Hides()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .AddChildContent("<span>m</span>"));
        cut.InvokeAsync(() => cut.Instance.OnScrollAwayChanged(true));

        cut.Find("[data-testid='agent-scroll-button']").Click();

        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "ai.scrollToBottom");
        // Optimistically hidden after the jump.
        Assert.Empty(cut.FindAll("[data-testid='agent-scroll-button']"));
    }

    [Fact]
    public void ScrollButton_Disabled_Does_Not_Register_Observer()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.ShowScrollButton, false)
            .AddChildContent("<span>m</span>"));

        // Even if a stray callback arrives, nothing renders.
        cut.InvokeAsync(() => cut.Instance.OnScrollAwayChanged(true));
        Assert.Empty(cut.FindAll("[data-testid='agent-scroll-button']"));
        Assert.DoesNotContain(_ctx.JSInterop.Invocations, i => i.Identifier == "ai.observeScrollButton");
    }

    // ── Empty state ──────────────────────────────────────────────────────────

    [Fact]
    public void Empty_Default_Shows_Localized_Title_And_Description()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.IsEmpty, true));

        Assert.Contains("No messages yet", cut.Markup);
        Assert.Contains("Start a conversation", cut.Markup);
    }

    [Fact]
    public void Empty_Slot_Overrides_Default()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.IsEmpty, true)
            .Add(x => x.EmptyState, b => b.AddMarkupContent(0, "<span data-testid='es'>Ask me anything</span>")));

        Assert.NotNull(cut.Find("[data-testid='es']"));
        Assert.DoesNotContain("No messages yet", cut.Markup);
    }

    [Fact]
    public void Empty_Suppresses_Message_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.IsEmpty, true)
            .AddChildContent("<span data-testid='msg'>hidden</span>"));

        Assert.Empty(cut.FindAll("[data-testid='msg']"));
    }

    // ── Markdown export ──────────────────────────────────────────────────────

    [Fact]
    public void ToMarkdown_Renders_Roles_Title_And_Bodies()
    {
        var md = Lumeo.AgentMessageList.ToMarkdown(new[]
        {
            new Lumeo.AgentMessageList.AgentMessageMarkdown(
                Lumeo.AgentMessage.AgentMessageRole.User, "Hello", Name: "You"),
            new Lumeo.AgentMessageList.AgentMessageMarkdown(
                Lumeo.AgentMessage.AgentMessageRole.Assistant, "Hi there"),
        }, title: "Chat");

        Assert.Contains("## Chat", md);
        Assert.Contains("**You**", md);
        Assert.Contains("Hello", md);
        Assert.Contains("**Assistant**", md);
        Assert.Contains("Hi there", md);
    }

    [Fact]
    public void ToMarkdown_Includes_Timestamp_When_Provided()
    {
        var ts = new DateTimeOffset(2026, 4, 20, 14, 30, 0, TimeSpan.Zero);
        var md = Lumeo.AgentMessageList.ToMarkdown(new[]
        {
            new Lumeo.AgentMessageList.AgentMessageMarkdown(
                Lumeo.AgentMessage.AgentMessageRole.Assistant, "Body", Timestamp: ts),
        });

        Assert.Contains("2026-04-20 14:30", md);
    }
}
