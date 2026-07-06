using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Role = Lumeo.AgentMessage.AgentMessageRole;

namespace Lumeo.Tests.Components.AgentMessageList;

/// <summary>
/// Wave 5 — AgentMessage Actions toolbar (copy / regenerate / retry) and branch
/// navigation (shadcn Actions + MessageBranch parity).
/// </summary>
public class AgentMessageActionsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AgentMessageActionsTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── AgentMessage Actions slot ────────────────────────────────────────────

    [Fact]
    public void AgentMessage_Renders_Actions_Slot_For_Assistant()
    {
        var cut = _ctx.Render<Lumeo.AgentMessage>(p => p
            .Add(m => m.Role, Role.Assistant)
            .AddChildContent("Answer")
            .Add(m => m.Actions, b => b.AddMarkupContent(0, "<span data-testid='act'>x</span>")));

        Assert.NotNull(cut.Find("[data-testid='act']"));
    }

    // ── AgentMessageActions built-in Copy ────────────────────────────────────

    [Fact]
    public void Copy_Button_Renders_When_CopyText_Set_And_Copies_To_Clipboard()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageActions>(p => p
            .Add(x => x.CopyText, "hello world"));

        cut.Find("[aria-label='Copy']").Click();

        Assert.Contains(_ctx.JSInterop.Invocations,
            i => i.Identifier == "copyToClipboard" && (i.Arguments[0] as string) == "hello world");
        // Confirmation swaps the label to "Copied".
        Assert.Contains("Copied", cut.Markup);
    }

    [Fact]
    public void No_Copy_Button_When_CopyText_Null()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageActions>();
        Assert.Empty(cut.FindAll("[aria-label='Copy']"));
    }

    [Fact]
    public void Regenerate_And_Retry_Render_Only_When_Wired()
    {
        var regen = false;
        var retry = false;

        var cut = _ctx.Render<Lumeo.AgentMessageActions>(p => p
            .Add(x => x.OnRegenerate, () => regen = true)
            .Add(x => x.OnRetry, () => retry = true));

        cut.Find("[aria-label='Regenerate']").Click();
        cut.Find("[aria-label='Retry']").Click();

        Assert.True(regen);
        Assert.True(retry);
    }

    [Fact]
    public void Regenerate_Absent_When_No_Callback()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageActions>(p => p
            .Add(x => x.CopyText, "x"));

        Assert.Empty(cut.FindAll("[aria-label='Regenerate']"));
        Assert.Empty(cut.FindAll("[aria-label='Retry']"));
    }

    // ── AgentMessageAction single button ─────────────────────────────────────

    [Fact]
    public void Action_Button_Exposes_Label_And_Fires_OnClick()
    {
        var clicked = false;
        var cut = _ctx.Render<Lumeo.AgentMessageAction>(p => p
            .Add(x => x.Label, "Bookmark")
            .Add(x => x.OnClick, () => clicked = true));

        var btn = cut.Find("button");
        Assert.Equal("Bookmark", btn.GetAttribute("aria-label"));
        btn.Click();
        Assert.True(clicked);
    }

    // ── AgentMessageBranchNav ────────────────────────────────────────────────

    [Fact]
    public void BranchNav_Shows_Page_Indicator()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageBranchNav>(p => p
            .Add(x => x.Index, 0)
            .Add(x => x.Count, 3));

        Assert.Contains("1 of 3", cut.Markup);
    }

    [Fact]
    public void BranchNav_Previous_Disabled_At_First()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageBranchNav>(p => p
            .Add(x => x.Index, 0)
            .Add(x => x.Count, 3));

        Assert.True(cut.Find("[aria-label='Previous response']").HasAttribute("disabled"));
        Assert.False(cut.Find("[aria-label='Next response']").HasAttribute("disabled"));
    }

    [Fact]
    public void BranchNav_Next_Advances_Index()
    {
        var captured = -1;
        var cut = _ctx.Render<Lumeo.AgentMessageBranchNav>(p => p
            .Add(x => x.Index, 0)
            .Add(x => x.Count, 3)
            .Add(x => x.IndexChanged, (int i) => captured = i));

        cut.Find("[aria-label='Next response']").Click();

        Assert.Equal(1, captured);
    }

    [Fact]
    public void BranchNav_Loop_Wraps_Previous_From_First_To_Last()
    {
        var captured = -1;
        var cut = _ctx.Render<Lumeo.AgentMessageBranchNav>(p => p
            .Add(x => x.Index, 0)
            .Add(x => x.Count, 3)
            .Add(x => x.Loop, true)
            .Add(x => x.IndexChanged, (int i) => captured = i));

        cut.Find("[aria-label='Previous response']").Click();

        Assert.Equal(2, captured);
    }
}
