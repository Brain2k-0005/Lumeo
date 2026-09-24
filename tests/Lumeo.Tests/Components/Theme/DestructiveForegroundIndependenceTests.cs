using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Theme;

/// <summary>
/// DocFlow / SQL Analyst field report (LU-14): every first-party component that painted a
/// SOLID destructive surface (<c>bg-destructive</c>) used <c>text-destructive-foreground</c>
/// for its label. Theme generators (tweakcn and others) still targeting the older shadcn
/// convention define <c>--destructive-foreground</c> as a dark-red TEXT color meant to sit on
/// a light destructive TINT — paired with Lumeo's solid <c>bg-destructive</c> that rendered as
/// near-invisible red-on-red text in 11 places across one consumer's app.
///
/// shadcn new-york v4 dropped <c>--destructive-foreground</c> entirely and hardcodes
/// <c>text-white</c> on every solid-destructive surface (badge.tsx, button.tsx) — verified
/// against the current shadcn-ui/ui repository. Lumeo now matches: every component below uses
/// <c>text-white</c>, which resolves from Tailwind's own color scale, not a theme-overridable
/// semantic token, so it can no longer be pulled dark-on-dark by a consumer's theme.
///
/// Toast's destructive variant is NOT in this list: it uses a separate, correctly-paired
/// <c>--destructive-light</c>/<c>--destructive-text</c> tint pair (see Toast.razor), never
/// <c>--destructive-foreground</c>, so it was never affected.
/// </summary>
public class DestructiveForegroundIndependenceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DestructiveForegroundIndependenceTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertWhiteNotForeground(string? cssClass)
    {
        Assert.NotNull(cssClass);
        Assert.Contains("text-white", cssClass);
        Assert.DoesNotContain("text-destructive-foreground", cssClass);
    }

    [Fact]
    public void Badge_Destructive_Uses_White_Text()
    {
        var cut = _ctx.Render<L.Badge>(p => p
            .Add(b => b.Variant, L.Badge.BadgeVariant.Destructive)
            .AddChildContent("Error"));

        AssertWhiteNotForeground(cut.Find("[data-slot='badge']").GetAttribute("class"));
    }

    [Fact]
    public void Button_Destructive_Uses_White_Text()
    {
        var cut = _ctx.Render<L.Button>(p => p
            .Add(b => b.Variant, L.Button.ButtonVariant.Destructive)
            .AddChildContent("Delete"));

        AssertWhiteNotForeground(cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Chip_Solid_Destructive_Uses_White_Text()
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Variant, L.Chip.ChipVariant.Solid)
            .Add(c => c.Color, "destructive")
            .AddChildContent("Blocked"));

        AssertWhiteNotForeground(cut.Find("[data-slot='chip']").GetAttribute("class"));
    }

    [Fact]
    public void SpeedDial_Destructive_Trigger_Uses_White_Text()
    {
        var cut = _ctx.Render<L.SpeedDial>(p => p
            .Add(s => s.Variant, "destructive")
            .Add(s => s.Items, new List<L.SpeedDial.SpeedDialItem>()));

        AssertWhiteNotForeground(cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void UploadTrigger_Destructive_Uses_White_Text()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(u => u.Variant, L.Button.ButtonVariant.Destructive)
            .AddChildContent("Remove file"));

        AssertWhiteNotForeground(cut.Find("[data-slot='upload-trigger']").GetAttribute("class"));
    }

    [Fact]
    public void AlertDialogAction_Destructive_Uses_White_Text()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.AlertDialogAction>(0);
                    inner.AddAttribute(1, "Variant", L.Button.ButtonVariant.Destructive);
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Delete")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        AssertWhiteNotForeground(cut.Find("button").GetAttribute("class"));
    }
}
