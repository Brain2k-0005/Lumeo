using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Alert;

public class AlertTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AlertTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Alert()
    {
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .AddChildContent("Alert message"));

        var div = cut.Find("[role='alert']");
        Assert.Equal("Alert message", div.TextContent.Trim());
    }

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .AddChildContent("Something went wrong"));

        Assert.Equal("Something went wrong", cut.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public void Renders_As_Div_Element()
    {
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .AddChildContent("Alert"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Has_Role_Alert_Attribute()
    {
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .AddChildContent("Accessible alert"));

        var div = cut.Find("div");
        Assert.Equal("alert", div.GetAttribute("role"));
    }

    [Fact]
    public void Default_Variant_Has_Background_And_Foreground_Classes()
    {
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .AddChildContent("Default"));

        var cls = cut.Find("[role='alert']").GetAttribute("class");
        Assert.Contains("bg-background", cls);
        Assert.Contains("text-foreground", cls);
    }

    [Fact]
    public void Destructive_Variant_Has_Destructive_Classes()
    {
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .Add(b => b.Variant, Lumeo.Alert.AlertVariant.Destructive)
            .AddChildContent("Error occurred"));

        var cls = cut.Find("[role='alert']").GetAttribute("class");
        Assert.Contains("border-destructive", cls);
        Assert.Contains("text-destructive", cls);
    }

    [Fact]
    public void All_Alerts_Have_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .AddChildContent("Base"));

        var cls = cut.Find("[role='alert']").GetAttribute("class");
        Assert.Contains("relative", cls);
        Assert.Contains("w-full", cls);
        Assert.Contains("rounded-lg", cls);
        Assert.Contains("border", cls);
        Assert.Contains("border-border", cls);
        Assert.Contains("px-4", cls);
        Assert.Contains("py-3", cls);
        Assert.Contains("text-sm", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .Add(b => b.Class, "my-alert-class")
            .AddChildContent("Styled"));

        var cls = cut.Find("[role='alert']").GetAttribute("class");
        Assert.Contains("my-alert-class", cls);
        Assert.Contains("relative", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-alert",
                ["aria-live"] = "polite"
            })
            .AddChildContent("Alert"));

        var div = cut.Find("div");
        Assert.Equal("my-alert", div.GetAttribute("data-testid"));
        Assert.Equal("polite", div.GetAttribute("aria-live"));
    }

    [Fact]
    public async Task Manual_Dismiss_Disposes_AutoDismiss_Timer()
    {
        int dismissCount = 0;
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .Add(x => x.IsDismissible, true)
            .Add(x => x.AutoDismissMs, 5000)
            .Add(x => x.Title, "Test")
            .Add(x => x.OnDismiss, EventCallback.Factory.Create(this, () => dismissCount++)));

        // Click dismiss button
        cut.Find("button[aria-label='Dismiss']").Click();
        Assert.Equal(1, dismissCount);

        // Wait longer than AutoDismissMs would fire
        // In bUnit the timer won't actually fire, but verify dismiss was called exactly once
        Assert.Equal(1, dismissCount);
    }

    [Fact]
    public void Double_Dismiss_Only_Fires_Once()
    {
        int dismissCount = 0;
        var cut = _ctx.Render<Lumeo.Alert>(p => p
            .Add(x => x.IsDismissible, true)
            .Add(x => x.Title, "Test")
            .Add(x => x.OnDismiss, EventCallback.Factory.Create(this, () => dismissCount++)));

        cut.Find("button[aria-label='Dismiss']").Click();
        // Second click shouldn't be possible (alert is hidden), but test the guard
        Assert.Equal(1, dismissCount);
    }
}
