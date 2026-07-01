using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Regression tests for two Multiple-mode trigger bugs (Codex P2 / user-reported):
///
/// 1. Label resolution: with composition-mode &lt;SelectItem&gt; children (not the
///    data-bound Items prop) and Values pre-selected, the closed trigger's tags echoed
///    the RAW value (e.g. a Guid) instead of the matching SelectItem's rendered label —
///    even though the open dropdown showed the correct labels for the same options.
///    Fixed via a persistent label cache (Select._labelCache), populated whenever a
///    SelectItem registers itself — necessary because SelectContent only renders its
///    SelectItem children while the popover is OPEN (@if (Context.IsOpen) in
///    SelectContent.razor), so a value pre-selected before the popover has EVER been
///    opened in this session still has no label to resolve; ChildContent / a future
///    explicit TagLabel parameter remain the reliable fallback for that specific case.
/// 2. ChildContent override: an explicit &lt;SelectTrigger ChildContent="..."&gt; was
///    silently ignored whenever Multiple=true and a value was selected — the tags branch's
///    condition was identical to HasValue's Multiple case, so ChildContent could never be
///    reached (dead code, not a runtime override).
///
/// Both bugs live in the same if/else-if chain in SelectTrigger.razor.
/// </summary>
public class SelectMultipleTagLabelTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectMultipleTagLabelTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class RenderState
    {
        public List<string> Values = new();
        public bool Open;
        public RenderFragment? TriggerChildContent;
    }

    private (IRenderedComponent<IComponent> Cut, RenderState State) RenderCompositionMultiple(
        List<string> values, RenderFragment? triggerChildContent = null, bool open = false)
    {
        var state = new RenderState { Values = values, Open = open, TriggerChildContent = triggerChildContent };
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Multiple", true);
            builder.AddAttribute(2, "Values", state.Values);
            builder.AddAttribute(9, "Open", state.Open);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                if (state.TriggerChildContent is not null)
                    b.AddAttribute(1, "ChildContent", state.TriggerChildContent);
                else
                    b.AddAttribute(1, "Placeholder", "Pick some…");
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    void Item(int seq, string value, string label)
                    {
                        c.OpenComponent<L.SelectItem>(seq);
                        c.AddAttribute(seq + 1, "Value", value);
                        c.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(t => t.AddContent(0, label)));
                        c.CloseComponent();
                    }
                    Item(0, "11111111-1111-1111-1111-111111111111", "Alice");
                    Item(10, "22222222-2222-2222-2222-222222222222", "Bob");
                    Item(20, "33333333-3333-3333-3333-333333333333", "Carol");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
        return (cut, state);
    }

    [Fact]
    public void ClosedTrigger_Tags_Show_The_SelectItem_Label_Not_The_Raw_Value_After_Being_Opened_Once()
    {
        // Open first so the composition-mode SelectItems mount and register their labels
        // into the persistent cache, THEN close — matching a real user who opens the
        // dropdown at least once during the session before it's ever collapsed again.
        var (cut, state) = RenderCompositionMultiple(new List<string>
        {
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
        }, open: true);

        state.Open = false;
        cut.Render();

        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
        // The raw value is expected in each tag's "Remove {value}" aria-label (unrelated to
        // this fix) — check the VISIBLE tag text specifically, not the whole markup.
        var tagTexts = cut.FindAll("span").Select(e => e.TextContent.Trim());
        Assert.DoesNotContain(tagTexts, t => t == "11111111-1111-1111-1111-111111111111");
        Assert.DoesNotContain(tagTexts, t => t == "22222222-2222-2222-2222-222222222222");
    }

    [Fact]
    public void ClosedTrigger_With_Only_One_Value_Still_Resolves_The_Label_After_Being_Opened_Once()
    {
        var (cut, state) = RenderCompositionMultiple(
            new List<string> { "33333333-3333-3333-3333-333333333333" }, open: true);

        state.Open = false;
        cut.Render();

        Assert.Contains("Carol", cut.Markup);
        var tagTexts = cut.FindAll("span").Select(e => e.TextContent.Trim());
        Assert.DoesNotContain(tagTexts, t => t == "33333333-3333-3333-3333-333333333333");
    }

    [Fact]
    public void An_Explicit_SelectTrigger_ChildContent_Overrides_The_Default_Tags_In_Multiple_Mode()
    {
        var (cut, _) = RenderCompositionMultiple(
            new List<string> { "11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222" },
            triggerChildContent: b => b.AddContent(0, "2 people selected"));

        Assert.Contains("2 people selected", cut.Markup);
        // The default tag rendering (with its own remove buttons) must NOT also appear.
        Assert.DoesNotContain("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
        Assert.DoesNotContain("aria-label=\"Remove ", cut.Markup);
    }

    [Fact]
    public void WithoutValues_The_Placeholder_Still_Wins_Even_When_ChildContent_Is_Set()
    {
        // Guard: the ChildContent-wins fix must stay gated on HasValue — otherwise a
        // consumer who always supplies ChildContent would never see the Placeholder.
        var (cut, _) = RenderCompositionMultiple(
            new List<string>(),
            triggerChildContent: b => b.AddContent(0, "should not show when empty"));

        Assert.DoesNotContain("should not show when empty", cut.Markup);
    }

    [Fact]
    public void TagWrap_Icon_Wrapper_Aligns_To_The_Start_Not_Centered_Across_Wrapped_Rows()
    {
        var (cut, _) = RenderCompositionMultiple(new List<string>
        {
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
            "33333333-3333-3333-3333-333333333333",
        });

        // The chevron/clear icon wrapper must use self-start (not the row's default
        // items-center), so it aligns with the first tag row instead of being vertically
        // centered across the full wrapped height once tags wrap to 2+ lines.
        var iconWrapper = cut.FindAll("div").Single(e =>
            e.ClassList.Contains("ms-auto") && e.ClassList.Contains("shrink-0"));
        Assert.Contains("self-start", iconWrapper.ClassList);
    }
}
