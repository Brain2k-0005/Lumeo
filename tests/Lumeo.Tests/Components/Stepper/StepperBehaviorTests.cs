using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Stepper;

/// <summary>
/// Tablist-style behaviour + a11y for the step navigator. The header renders a
/// <c>role="tablist"</c> of <c>role="tab"</c> buttons with roving tabindex;
/// the active step carries <c>aria-selected="true"</c> + <c>aria-current="step"</c>,
/// and the body is a <c>role="tabpanel"</c> wired to the active tab via
/// <c>aria-labelledby</c>. Step activation flows through <c>ActiveStepChanged</c>;
/// clicking is gated by <c>AllowStepClick</c> (+ <c>Linear</c>); arrow keys move
/// FOCUS only and must NOT change the active step. Method names are distinct from
/// the sibling <see cref="StepperTests"/> in this folder.
/// </summary>
public class StepperBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StepperBehaviorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Three plain steps; AllowStepClick / Linear / ActiveStep / the change handler
    // are caller-controlled so each test drives only what it asserts.
    private IRenderedComponent<Lumeo.Stepper> RenderStepper(
        int activeStep = 0,
        bool allowStepClick = false,
        bool linear = true,
        EventCallback<int>? activeStepChanged = null)
    {
        return _ctx.Render<Lumeo.Stepper>(p =>
        {
            p.Add(s => s.ActiveStep, activeStep);
            p.Add(s => s.AllowStepClick, allowStepClick);
            p.Add(s => s.Linear, linear);
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

    [Fact]
    public void Active_Tab_Has_Selected_Current_And_Is_The_Only_Roving_Tab_Stop()
    {
        var cut = RenderStepper(activeStep: 1);

        // The header is a real tablist of tabs.
        var tablist = cut.Find("[role='tablist']");
        Assert.NotNull(tablist);
        var tabs = cut.FindAll("[role='tab']");
        Assert.Equal(3, tabs.Count);

        // Active (index 1): selected + current + the single tab stop.
        Assert.Equal("true", tabs[1].GetAttribute("aria-selected"));
        Assert.Equal("step", tabs[1].GetAttribute("aria-current"));
        Assert.Equal("0", tabs[1].GetAttribute("tabindex"));

        // Inactive tabs: not selected, no aria-current, removed from tab order.
        foreach (var i in new[] { 0, 2 })
        {
            Assert.Equal("false", tabs[i].GetAttribute("aria-selected"));
            Assert.Null(tabs[i].GetAttribute("aria-current"));
            Assert.Equal("-1", tabs[i].GetAttribute("tabindex"));
        }
    }

    [Fact]
    public void TabPanel_Is_Labelled_By_The_Active_Tab()
    {
        var cut = RenderStepper(activeStep: 1);

        var activeTabId = cut.FindAll("[role='tab']")[1].GetAttribute("id");
        var panel = cut.Find("[role='tabpanel']");

        // The panel announces in the context of the active step.
        Assert.Equal(activeTabId, panel.GetAttribute("aria-labelledby"));
        // ...and only the active step's body is shown.
        Assert.Contains("body-two", cut.Markup);
        Assert.DoesNotContain("body-one", cut.Markup);
    }

    [Fact]
    public void Clicking_A_Tab_In_NonLinear_Mode_Fires_ActiveStepChanged()
    {
        int? changedTo = null;
        var cb = EventCallback.Factory.Create<int>(this, v => changedTo = v);

        // Non-linear + clickable: any step is reachable by click.
        var cut = RenderStepper(activeStep: 0, allowStepClick: true, linear: false, activeStepChanged: cb);

        cut.FindAll("[role='tab']")[2].Click();

        Assert.Equal(2, changedTo);
    }

    [Fact]
    public void Click_Updates_Selection_And_AriaCurrent_Immediately()
    {
        // SetActiveStep self-assigns ActiveStep AND raises ActiveStepChanged, so a
        // clickable step reflects the new selection immediately — no parent rebind
        // needed for the ARIA state to move.
        int active = 0;
        var cut = RenderStepper(
            activeStep: 0,
            allowStepClick: true,
            linear: false,
            activeStepChanged: EventCallback.Factory.Create<int>(this, v => active = v));

        cut.FindAll("[role='tab']")[2].Click();

        Assert.Equal(2, active);
        var tabs = cut.FindAll("[role='tab']");
        Assert.Equal("true", tabs[2].GetAttribute("aria-selected"));
        Assert.Equal("step", tabs[2].GetAttribute("aria-current"));
        Assert.Equal("false", tabs[0].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Linear_Mode_Marks_Unreachable_Step_Disabled_And_Click_Is_Ignored()
    {
        var fired = false;
        var cb = EventCallback.Factory.Create<int>(this, _ => fired = true);

        // Linear + clickable from step 0: the far step (index 2) is not yet reachable.
        var cut = RenderStepper(activeStep: 0, allowStepClick: true, linear: true, activeStepChanged: cb);

        var farTab = cut.FindAll("[role='tab']")[2];
        Assert.Equal("true", farTab.GetAttribute("aria-disabled"));

        farTab.Click();

        Assert.False(fired, "Clicking a gated (aria-disabled) step must not change the active step.");
    }

    [Fact]
    public void Click_Does_Nothing_When_AllowStepClick_Is_False()
    {
        var fired = false;
        var cb = EventCallback.Factory.Create<int>(this, _ => fired = true);

        // Steps are not interactive when AllowStepClick is off.
        var cut = RenderStepper(activeStep: 0, allowStepClick: false, linear: false, activeStepChanged: cb);

        var tabs = cut.FindAll("[role='tab']");
        Assert.Equal("true", tabs[1].GetAttribute("aria-disabled"));

        tabs[1].Click();

        Assert.False(fired);
    }

    [Fact]
    public void Arrow_Key_Moves_Focus_Only_And_Does_Not_Change_The_Active_Step()
    {
        var fired = false;
        var cb = EventCallback.Factory.Create<int>(this, _ => fired = true);

        var cut = RenderStepper(activeStep: 0, allowStepClick: true, linear: false, activeStepChanged: cb);

        // ArrowRight on the active tab is a focus-roving key (WAI-ARIA tablist):
        // it must NOT activate / change the selected step.
        cut.FindAll("[role='tab']")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        var tabs = cut.FindAll("[role='tab']");
        Assert.False(fired, "Arrow keys move focus only; they must not fire ActiveStepChanged.");
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("step", tabs[0].GetAttribute("aria-current"));
    }
}
