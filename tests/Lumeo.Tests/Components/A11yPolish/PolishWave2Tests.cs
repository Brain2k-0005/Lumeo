using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.A11yPolish;

/// <summary>
/// Polish wave 2:
///   #284 Result — role is "alert" for error-ish statuses, "status" otherwise.
///   #270 ButtonGroup — exposes role="group" + optional AriaLabel.
///   #245 Stepper — nav button labels come from the localizer when not overridden.
/// (#247 BackToTop scroll-throttle is JS-only — covered by e2e/manual, not bUnit.)
/// </summary>
public class PolishWave2Tests
{
    private static BunitContext NewCtx()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        return ctx;
    }

    [Theory]
    [InlineData(Lumeo.Result.ResultStatus.Error, "alert")]
    [InlineData(Lumeo.Result.ResultStatus.Forbidden, "alert")]
    [InlineData(Lumeo.Result.ResultStatus.ServerError, "alert")]
    [InlineData(Lumeo.Result.ResultStatus.Success, "status")]
    [InlineData(Lumeo.Result.ResultStatus.Info, "status")]
    [InlineData(Lumeo.Result.ResultStatus.NotFound, "status")]
    public void Result_role_tracks_status(Lumeo.Result.ResultStatus status, string expectedRole)
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Result>(p => p
            .Add(x => x.Status, status)
            .Add(x => x.Title, "Outcome"));

        Assert.NotNull(cut.Find($"[role='{expectedRole}']"));
    }

    [Fact]
    public void ButtonGroup_exposes_group_role_and_label()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.ButtonGroup>(p => p
            .Add(x => x.AriaLabel, "Text alignment"));

        var group = cut.Find("[role='group']");
        Assert.Equal("Text alignment", group.GetAttribute("aria-label"));
    }

    [Fact]
    public void Stepper_nav_labels_default_to_localized_strings()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Stepper>();

        var buttons = cut.FindAll("button");
        // No steps registered -> the only buttons are the footer Back + Next.
        Assert.Equal(2, buttons.Count);
        Assert.Equal("Back", buttons[0].TextContent.Trim());
        Assert.Equal("Next", buttons[1].TextContent.Trim());
    }

    [Fact]
    public void Stepper_nav_labels_respect_explicit_overrides()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Stepper>(p => p
            .Add(x => x.BackLabel, "Zurück")
            .Add(x => x.NextLabel, "Weiter"));

        var buttons = cut.FindAll("button");
        Assert.Equal("Zurück", buttons[0].TextContent.Trim());
        Assert.Equal("Weiter", buttons[1].TextContent.Trim());
    }
}
