using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AgentMessageList;

/// <summary>
/// Wave 4 composition audit — AgentMessageList/AgentMessageAction/
/// AgentMessageBranchNav render only native &lt;button&gt;s (scroll-to-latest,
/// per-message actions, branch prev/next); Enter/Space activation is free via
/// the browser's default button semantics. AgentMessageListScrollTests already
/// covers the scroll-to-latest button's click->scroll wiring, and
/// AgentMessageActionsTests already covers OnClick firing + branch index
/// stepping via .Click(). This file's own incremental surface — not covered
/// elsewhere — is confirming none of these buttons carry a tabindex override
/// that would pull them out of the native Tab order (the one gap-scan concern:
/// "no roving tabindex / arrow-key group — Tab-only" IS the correct, intended
/// behaviour here since these are independent action buttons, not a composite
/// widget requiring roving focus).
/// </summary>
public class AgentMessageListKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AgentMessageListKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ScrollToLatest_Button_Carries_No_Tabindex_Override()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .AddChildContent("<span>m</span>"));
        cut.InvokeAsync(() => cut.Instance.OnScrollAwayChanged(true));

        var button = cut.Find("[data-testid='agent-scroll-button']");
        Assert.False(button.HasAttribute("tabindex"));
    }

    [Fact]
    public void AgentMessageAction_Button_Carries_No_Tabindex_Override()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageAction>(p => p
            .Add(x => x.Label, "Bookmark"));

        Assert.False(cut.Find("button").HasAttribute("tabindex"));
    }

    [Fact]
    public void BranchNav_Prev_And_Next_Buttons_Carry_No_Tabindex_Override()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageBranchNav>(p => p
            .Add(x => x.Index, 1)
            .Add(x => x.Count, 3));

        Assert.All(cut.FindAll("button"), b => Assert.False(b.HasAttribute("tabindex")));
    }
}
