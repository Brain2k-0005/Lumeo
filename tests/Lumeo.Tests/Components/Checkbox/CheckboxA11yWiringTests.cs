using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.Checkbox;

/// <summary>
/// Triage #23 and #24 (medium, keyboard-a11y).
///
/// #23 — A consumer-splatted <c>id="..."</c> (via AdditionalAttributes) renders AFTER the
/// component's explicit <c>id="@EffectiveId"</c>, so the splat is what actually lands on the
/// <c>&lt;button&gt;</c>. Before the fix the <c>&lt;label for&gt;</c> still pointed at the
/// generated/explicit id, desyncing the pair so clicking the label no longer toggled the
/// checkbox. The fix routes <c>EffectiveId</c> through <c>LumeoIds.Effective(AdditionalAttributes, …)</c>
/// so the label's <c>for</c> follows the splatted id and they stay in sync.
///
/// #24 — A standalone <c>Description</c> was rendered visually but never referenced by
/// <c>aria-describedby</c> (which outside a FormField was null), so the text was
/// programmatically orphaned. The fix gives the description <c>&lt;p&gt;</c> a stable id and
/// joins it into <c>aria-describedby</c>.
///
/// These assert the directly observable rendered markup (id / for / aria-describedby) — no
/// reliance on real DOM focus.
/// </summary>
public class CheckboxA11yWiringTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CheckboxA11yWiringTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- #23: splatted id keeps button id and label for in sync ---

    [Fact]
    public void Splatted_Id_Lands_On_Button_And_Label_For_Follows_It()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Label, "Accept terms")
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["id"] = "splatted-id"
            }));

        var buttonId = cut.Find("button").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        // The splat wins on the button id (it renders after the explicit id=).
        Assert.Equal("splatted-id", buttonId);
        // Without the fix the label's `for` stays on the generated id and desyncs;
        // with the fix it follows the splatted id so the label still targets the button.
        Assert.Equal("splatted-id", labelFor);
        Assert.Equal(buttonId, labelFor);
    }

    // --- #24: Description is wired via aria-describedby ---

    [Fact]
    public void Description_Is_Referenced_By_Aria_DescribedBy()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Label, "Terms")
            .Add(c => c.Description, "You agree to our terms"));

        var describedBy = cut.Find("button").GetAttribute("aria-describedby");
        var descriptionId = cut.Find("p").GetAttribute("id");

        // The description <p> now carries a stable id...
        Assert.False(string.IsNullOrEmpty(descriptionId));
        // ...and the control points at it (before the fix aria-describedby was null/empty
        // outside a FormField, leaving the description programmatically orphaned).
        Assert.False(string.IsNullOrEmpty(describedBy));
        Assert.Contains(descriptionId!, describedBy!);
    }

    [Fact]
    public void No_Description_Leaves_No_Aria_DescribedBy()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Label, "Terms"));

        // With no Description and no FormField there are no tokens to describe by, so the
        // attribute is omitted entirely rather than rendered as aria-describedby="".
        Assert.Null(cut.Find("button").GetAttribute("aria-describedby"));
    }
}
