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

    // --- #464 finding 1: horizontal header overflow containment ---

    [Fact]
    public void Horizontal_Header_Carries_Overflow_Containment_Classes()
    {
        // Regression: a horizontal header with several long-labeled steps had no width
        // constraint of its own, so its content's natural width could force the whole
        // page wider. The role=tablist rail must be its own horizontal scroll container
        // instead.
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.Orientation, Lumeo.Orientation.Horizontal)
            .Add(s => s.ActiveStep, 0)
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "A very long step title that could force overflow"))
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "Another very long step title, also long"))
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "Yet another long step title here too"))
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "Fourth long step title in this row"))
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "Fifth long step title, still going"))
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "Sixth and final long step title")));

        var header = cut.Find("[role='tablist']");
        var cls = header.GetAttribute("class") ?? "";
        Assert.Contains("overflow-x-auto", cls);

        // Each label wrapper must be able to shrink below its title's intrinsic content
        // width (min-w-0) so it participates in the scroll container instead of forcing
        // the rail wider, and the title itself truncates rather than growing unbounded.
        var labelDiv = header.QuerySelectorAll("span.font-medium")[0].ParentElement!;
        Assert.Contains("min-w-0", labelDiv.GetAttribute("class") ?? "");

        var titleSpan = header.QuerySelector("span.font-medium")!;
        Assert.Contains("truncate", titleSpan.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Vertical_Header_Does_Not_Get_Overflow_Containment_Classes()
    {
        // Vertical orientation already stacks and never grows horizontally — it must
        // stay untouched by the horizontal-header overflow fix.
        var cut = _ctx.Render<Lumeo.Stepper>(p => p
            .Add(s => s.Orientation, Lumeo.Orientation.Vertical)
            .Add(s => s.ActiveStep, 0)
            .AddChildContent<Lumeo.StepperStep>(s => s.Add(x => x.Title, "Step 1")));

        var header = cut.Find("[role='tablist']");
        var cls = header.GetAttribute("class") ?? "";
        Assert.DoesNotContain("overflow-x-auto", cls);
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
