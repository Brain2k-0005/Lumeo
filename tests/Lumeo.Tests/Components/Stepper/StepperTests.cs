using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Stepper;

public class StepperTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StepperTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_With_Navigation_Buttons()
    {
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.ActiveStep, 0)
            .Add(s => s.NextLabel, "Next")
            .Add(s => s.BackLabel, "Back")
            .Add(s => s.FinishLabel, "Finish")
            .AddChildContent(b =>
            {
                b.OpenComponent<Lumeo.StepperStep>(0);
                b.AddAttribute(1, "Title", "Step 1");
                b.CloseComponent();
            }));

        var buttons = cut.FindAll("button");
        Assert.True(buttons.Count >= 2, "Should have at least Back and Next/Finish buttons");
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.Class, "my-stepper")
            .Add(s => s.ActiveStep, 0)
            .AddChildContent(b =>
            {
                b.OpenComponent<Lumeo.StepperStep>(0);
                b.AddAttribute(1, "Title", "Step 1");
                b.CloseComponent();
            }));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-stepper", cls);
    }

    [Fact]
    public void Back_Button_Disabled_On_First_Step()
    {
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.ActiveStep, 0)
            .Add(s => s.BackLabel, "Back")
            .AddChildContent(b =>
            {
                b.OpenComponent<Lumeo.StepperStep>(0);
                b.AddAttribute(1, "Title", "Step 1");
                b.CloseComponent();
                b.OpenComponent<Lumeo.StepperStep>(2);
                b.AddAttribute(3, "Title", "Step 2");
                b.CloseComponent();
            }));

        // Find the Back button and verify it is disabled
        var backButton = cut.FindAll("button").First(b => b.TextContent.Contains("Back"));
        Assert.NotNull(backButton.GetAttribute("disabled"));
    }

    [Fact]
    public void Orientation_Horizontal_Uses_FlexCol()
    {
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.Orientation, Lumeo.Orientation.Horizontal)
            .Add(s => s.ActiveStep, 0)
            .AddChildContent(b =>
            {
                b.OpenComponent<Lumeo.StepperStep>(0);
                b.AddAttribute(1, "Title", "Step 1");
                b.CloseComponent();
            }));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex-col", cls);
    }

    [Fact]
    public void Finish_Label_Used_For_FinishLabel_Param()
    {
        // Verify the FinishLabel parameter is accepted and the Stepper renders without error
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.ActiveStep, 0)
            .Add(s => s.FinishLabel, "Done!")
            .Add(s => s.NextLabel, "Continue")
            .Add(s => s.BackLabel, "Previous")
            .AddChildContent(b =>
            {
                b.OpenComponent<Lumeo.StepperStep>(0);
                b.AddAttribute(1, "Title", "Only Step");
                b.CloseComponent();
            }));

        // Steps now register with a same-pass render nudge — no manual second
        // render needed (this previously worked around the first-render blank).
        var buttons = cut.FindAll("button");
        Assert.True(buttons.Count >= 1, "Should have at least one navigation button");
        Assert.Contains("Done!", cut.Markup);
    }

    [Fact]
    public void Renders_Indicators_Panel_And_Footer_On_First_Render()
    {
        // Regression: StepperStep registrations land AFTER the parent's first
        // render pass; without the registration nudge the header/panel/footer
        // stayed blank until an unrelated re-render.
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.ActiveStep, 0)
            .AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "One")
                .AddChildContent("step-one-body"))
            .AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "Two")
                .AddChildContent("step-two-body")));

        Assert.Equal(2, cut.FindAll("[role='tab']").Count);
        Assert.Contains("step-one-body", cut.Markup);
        Assert.DoesNotContain("step-two-body", cut.Markup);
    }

    [Fact]
    public void KeepMounted_Step_Stays_In_Dom_While_Inactive()
    {
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.ActiveStep, 1)
            .AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "One")
                .Add(x => x.KeepMounted, true)
                .AddChildContent("keep-me-mounted"))
            .AddChildContent<Lumeo.StepperStep>(s => s
                .Add(x => x.Title, "Two")
                .AddChildContent("active-body")));

        // Inactive KeepMounted content stays in the DOM, hidden; active body shows.
        Assert.Contains("keep-me-mounted", cut.Markup);
        Assert.Contains("active-body", cut.Markup);
        Assert.Contains("display:none", cut.Markup);
    }

    [Fact]
    public void Removed_Step_Does_Not_Leave_Ghost_Indicator()
    {
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.ActiveStep, 0)
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "One"))
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "Two")));
        Assert.Equal(2, cut.FindAll("[role='tab']").Count);

        cut.Render(p => p
            .Add(s => s.ActiveStep, 0)
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "One")));

        Assert.Single(cut.FindAll("[role='tab']"));
    }
}
