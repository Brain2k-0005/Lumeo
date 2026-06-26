using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Stepper;

/// <summary>
/// Regression coverage for the "reorder-class" Stepper bugs fixed in this batch:
/// <list type="bullet">
///   <item>#197 — roving tabindex follows the FOCUSED tab (arrow keys), while
///   <c>aria-selected</c>/<c>aria-current</c> stay on <c>ActiveStep</c>.</item>
///   <item>#196 — Home/End jump focus to the first/last tab.</item>
///   <item>#104 — <c>ActiveStep</c> is clamped into range when the step count
///   shrinks, and the clamped value is pushed back via <c>ActiveStepChanged</c>.</item>
///   <item>#105 — the KeepMounted panel is keyed on the step's STABLE identity,
///   so a kept-mounted fragment keeps tracking its own <c>ChildContent</c> across
///   a remove of an earlier step.</item>
/// </list>
/// Per the WAI-ARIA tablist contract these assert MARKUP only (tabindex / aria /
/// recorded child content) — never real DOM focus. Method names are distinct from
/// the sibling <see cref="StepperTests"/> and <see cref="StepperBehaviorTests"/>.
/// </summary>
public class StepperReorderClassTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StepperReorderClassTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Three plain steps; ActiveStep + the change handler are caller-controlled.
    private IRenderedComponent<Lumeo.Stepper> RenderThreeSteps(
        int activeStep = 0,
        EventCallback<int>? activeStepChanged = null)
    {
        return _ctx.Render<Lumeo.Stepper>(p =>
        {
            p.Add(s => s.ActiveStep, activeStep);
            if (activeStepChanged is { } cb)
                p.Add(s => s.ActiveStepChanged, cb);
            p.AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "One")
                .AddChildContent("body-one"));
            p.AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "Two")
                .AddChildContent("body-two"));
            p.AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "Three")
                .AddChildContent("body-three"));
        });
    }

    // ---- #197 / #196: roving tabindex follows focus, selection does not ----

    [Fact]
    public void ArrowKey_Moves_Roving_TabIndex_To_Focused_Tab_But_Not_Selection()
    {
        // Active = 0, so tab[0] starts as the single roving stop (tabindex=0).
        var cut = RenderThreeSteps(activeStep: 0);

        // ArrowRight on the active tab roves FOCUS to tab[1] (focus, not activation).
        cut.InvokeAsync(() =>
            cut.FindAll("[role='tab']")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }))
            .GetAwaiter().GetResult();

        var tabs = cut.FindAll("[role='tab']");

        // The roving tab stop moved to the focused tab: exactly one tabindex=0,
        // and it is the NEWLY focused tab[1].
        Assert.Single(cut.FindAll("[role='tab'][tabindex='0']"));
        Assert.Equal("0", tabs[1].GetAttribute("tabindex"));
        Assert.Equal("-1", tabs[0].GetAttribute("tabindex"));
        Assert.Equal("-1", tabs[2].GetAttribute("tabindex"));

        // Focus != activation: selection / current stay on the ORIGINAL active step.
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("step", tabs[0].GetAttribute("aria-current"));
        Assert.Equal("false", tabs[1].GetAttribute("aria-selected"));
        Assert.Null(tabs[1].GetAttribute("aria-current"));
    }

    [Fact]
    public void End_Key_Roves_TabIndex_To_Last_Tab_Without_Changing_Selection()
    {
        var fired = false;
        var cb = EventCallback.Factory.Create<int>(this, _ => fired = true);
        var cut = RenderThreeSteps(activeStep: 0, activeStepChanged: cb);

        // End jumps focus to the LAST tab (#196).
        cut.InvokeAsync(() =>
            cut.FindAll("[role='tab']")[0].KeyDown(new KeyboardEventArgs { Key = "End" }))
            .GetAwaiter().GetResult();

        var tabs = cut.FindAll("[role='tab']");
        Assert.Equal("0", tabs[2].GetAttribute("tabindex"));
        Assert.Single(cut.FindAll("[role='tab'][tabindex='0']"));

        // Still focus-only: no activation, selection stays on step 0.
        Assert.False(fired, "Home/End move focus only; they must not fire ActiveStepChanged.");
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("step", tabs[0].GetAttribute("aria-current"));
    }

    [Fact]
    public void Home_Key_Roves_TabIndex_To_First_Tab_From_A_Later_Tab()
    {
        // Active = 2: tab[2] owns the roving stop initially.
        var cut = RenderThreeSteps(activeStep: 2);
        Assert.Equal("0", cut.FindAll("[role='tab']")[2].GetAttribute("tabindex"));

        // Home jumps focus to the FIRST tab (#196).
        cut.InvokeAsync(() =>
            cut.FindAll("[role='tab']")[2].KeyDown(new KeyboardEventArgs { Key = "Home" }))
            .GetAwaiter().GetResult();

        var tabs = cut.FindAll("[role='tab']");
        Assert.Equal("0", tabs[0].GetAttribute("tabindex"));
        Assert.Single(cut.FindAll("[role='tab'][tabindex='0']"));

        // Selection unmoved: step 2 is still selected/current.
        Assert.Equal("true", tabs[2].GetAttribute("aria-selected"));
        Assert.Equal("step", tabs[2].GetAttribute("aria-current"));
        Assert.Equal("false", tabs[0].GetAttribute("aria-selected"));
    }

    // ---- #104: ActiveStep clamped on shrink + pushed back via the binding ----

    [Fact]
    public void Shrinking_Below_ActiveStep_Clamps_And_Reports_The_Clamped_Index()
    {
        int? reported = null;
        var cb = EventCallback.Factory.Create<int>(this, v => reported = v);

        // Start at the last of three steps.
        var cut = RenderThreeSteps(activeStep: 2, activeStepChanged: cb);
        Assert.Equal(3, cut.FindAll("[role='tab']").Count);

        // Re-render with ONLY ONE step (still requesting ActiveStep=2). The two
        // removed StepperSteps dispose; the count shrinks from 3 to 1.
        cut.Render(p => p
            .Add(s => s.ActiveStep, 2)
            .AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "One")
                .AddChildContent("body-one")));

        // Only one step left, and the active panel shows it (not a blank panel).
        Assert.Single(cut.FindAll("[role='tab']"));
        Assert.Contains("body-one", cut.Markup);

        // ActiveStep was clamped to the new last index (0) and PUSHED BACK so a
        // @bind-ActiveStep parent stays in sync.
        Assert.Equal(0, reported);

        // The single step is both first and last, so Back is disabled and the
        // Finish button is enabled (footer is no longer wedged on a stale index).
        var back = cut.FindAll("button").First(b => b.TextContent.Contains("Back"));
        Assert.NotNull(back.GetAttribute("disabled"));
        var finish = cut.FindAll("button").First(b => b.TextContent.Contains("Finish"));
        Assert.Null(finish.GetAttribute("disabled"));

        // The clamped tab is the single roving stop and is selected/current.
        var tab = cut.FindAll("[role='tab']")[0];
        Assert.Equal("0", tab.GetAttribute("tabindex"));
        Assert.Equal("true", tab.GetAttribute("aria-selected"));
        Assert.Equal("step", tab.GetAttribute("aria-current"));
    }

    // ---- #105: KeepMounted panel keyed on stable identity, not loop index ----

    [Fact]
    public void Removing_First_Step_Keeps_The_KeptMounted_Body_With_Its_Own_Content()
    {
        // Two KeepMounted steps so BOTH bodies live in the DOM regardless of which
        // is active. Active = 1, so step[1]'s body is the visible one and carries
        // its own marker "kept-two".
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.ActiveStep, 1)
            .AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "One")
                .Add(x => x.KeepMounted, true)
                .AddChildContent("kept-one"))
            .AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "Two")
                .Add(x => x.KeepMounted, true)
                .AddChildContent("kept-two")));

        Assert.Equal(2, cut.FindAll("[role='tab']").Count);
        Assert.Contains("kept-one", cut.Markup);
        Assert.Contains("kept-two", cut.Markup);

        // Remove step[0]. The former step[1] is reused (its instance survives) and,
        // because the panel is keyed on the step's STABLE Guid identity (#105) and
        // not the loop index, its kept-mounted fragment keeps rendering ITS OWN
        // ChildContent ("kept-two") rather than picking up the removed step's slot.
        cut.Render(p => p
            .Add(s => s.ActiveStep, 0)
            .AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "Two")
                .Add(x => x.KeepMounted, true)
                .AddChildContent("kept-two")));

        Assert.Single(cut.FindAll("[role='tab']"));
        // The surviving step still carries its OWN content marker; the removed
        // step's marker is gone (no stale fragment dragged onto the wrong owner).
        Assert.Contains("kept-two", cut.Markup);
        Assert.DoesNotContain("kept-one", cut.Markup);
    }
}
