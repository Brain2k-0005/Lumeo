using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toast;

public class ToastTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public ToastTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    // ─── Toast component rendering ───────────────────────────────────────────

    [Fact]
    public void Toast_Renders_Div_With_Role_Alert()
    {
        var cut = _ctx.Render<L.Toast>();

        var alert = cut.Find("[role='alert']");
        Assert.NotNull(alert);
    }

    [Fact]
    public void Toast_Default_Variant_Has_Border_And_BgCard()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Default));

        var alert = cut.Find("[role='alert']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("bg-card", cls);
        Assert.Contains("text-foreground", cls);
    }

    [Fact]
    public void Toast_Destructive_Variant_Has_Destructive_Classes()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Destructive));

        var alert = cut.Find("[role='alert']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("border-destructive", cls);
        Assert.Contains("text-destructive-text", cls);
    }

    [Fact]
    public void Toast_Success_Variant_Has_Success_Classes()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Success));

        var alert = cut.Find("[role='alert']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("border-success", cls);
        Assert.Contains("text-success-text", cls);
    }

    [Fact]
    public void Toast_Warning_Variant_Has_Warning_Classes()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Warning));

        var alert = cut.Find("[role='alert']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("border-warning", cls);
        Assert.Contains("text-warning-text", cls);
    }

    [Fact]
    public void Toast_Info_Variant_Has_Info_Classes()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Variant, ToastVariant.Info));

        var alert = cut.Find("[role='alert']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("border-info", cls);
        Assert.Contains("text-info-text", cls);
    }

    [Fact]
    public void Toast_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.ChildContent, (RenderFragment)(builder =>
                builder.AddContent(0, "Toast body"))));

        Assert.Contains("Toast body", cut.Markup);
    }

    [Fact]
    public void Toast_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.Toast>(p => p
            .Add(b => b.Class, "my-toast"));

        var alert = cut.Find("[role='alert']");
        Assert.Contains("my-toast", alert.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Toast_Alert_Has_Base_Classes()
    {
        var cut = _ctx.Render<L.Toast>();

        var alert = cut.Find("[role='alert']");
        var cls = alert.GetAttribute("class") ?? "";
        Assert.Contains("rounded-md", cls);
        Assert.Contains("shadow-lg", cls);
        Assert.Contains("border", cls);
    }

    // ─── ToastTitle ─────────────────────────────────────────────────────────

    [Fact]
    public void ToastTitle_Renders_Paragraph_With_Content()
    {
        var cut = _ctx.Render<L.ToastTitle>(p => p
            .Add(b => b.ChildContent, (RenderFragment)(b =>
                b.AddContent(0, "My Title"))));

        var p = cut.Find("p");
        Assert.Contains("My Title", p.TextContent);
    }

    [Fact]
    public void ToastTitle_Has_Font_Semibold_Class()
    {
        var cut = _ctx.Render<L.ToastTitle>();

        var p = cut.Find("p");
        Assert.Contains("font-semibold", p.GetAttribute("class") ?? "");
    }

    // ─── ToastDescription ───────────────────────────────────────────────────

    [Fact]
    public void ToastDescription_Renders_Div_With_Content()
    {
        var cut = _ctx.Render<L.ToastDescription>(p => p
            .Add(b => b.ChildContent, (RenderFragment)(b =>
                b.AddContent(0, "Some description"))));

        Assert.Contains("Some description", cut.Markup);
    }

    [Fact]
    public void ToastDescription_Has_Text_Xs_Class()
    {
        var cut = _ctx.Render<L.ToastDescription>();

        var div = cut.Find("div");
        Assert.Contains("text-xs", div.GetAttribute("class") ?? "");
    }

    // ─── ToastClose ─────────────────────────────────────────────────────────

    [Fact]
    public void ToastClose_Renders_Button()
    {
        var cut = _ctx.Render<L.ToastClose>();

        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void ToastClose_Click_Invokes_OnClose_Callback()
    {
        var called = false;
        var cut = _ctx.Render<L.ToastClose>(p => p
            .Add(b => b.OnClose, EventCallback.Factory.Create(_ctx, () => called = true)));

        cut.Find("button").Click();

        Assert.True(called);
    }

    [Fact]
    public void ToastClose_Has_Absolute_Positioning()
    {
        var cut = _ctx.Render<L.ToastClose>();

        var btn = cut.Find("button");
        Assert.Contains("absolute", btn.GetAttribute("class") ?? "");
    }

    // ─── ToastViewport ──────────────────────────────────────────────────────

    [Fact]
    public void ToastViewport_Renders_Div()
    {
        var cut = _ctx.Render<L.ToastViewport>();

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void ToastViewport_Default_Position_BottomRight()
    {
        var cut = _ctx.Render<L.ToastViewport>();

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("bottom-4", cls);
        Assert.Contains("right-4", cls);
    }

    [Fact]
    public void ToastViewport_TopLeft_Position_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.ToastViewport>(p => p
            .Add(b => b.Position, L.ToastViewport.ToastPosition.TopLeft));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("top-4", cls);
        Assert.Contains("left-4", cls);
    }

    [Fact]
    public void ToastViewport_TopRight_Position_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.ToastViewport>(p => p
            .Add(b => b.Position, L.ToastViewport.ToastPosition.TopRight));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("top-4", cls);
        Assert.Contains("right-4", cls);
    }

    [Fact]
    public void ToastViewport_Has_Fixed_And_ZIndex_Class()
    {
        var cut = _ctx.Render<L.ToastViewport>();

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("fixed", cls);
        Assert.Contains("z-[100]", cls);
    }

    // ─── ToastProvider ──────────────────────────────────────────────────────

    [Fact]
    public void ToastProvider_Renders_Without_Toasts_Initially()
    {
        var cut = _ctx.Render<L.ToastProvider>();

        // No toast alerts visible until a message is shown
        var alerts = cut.FindAll("[role='alert']");
        Assert.Empty(alerts);
    }

    [Fact]
    public void ToastProvider_Shows_Toast_When_ToastService_Show_Is_Called()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>();

        toastService!.Show("Hello World");
        cut.WaitForState(() => cut.FindAll("[role='alert']").Count > 0, TimeSpan.FromSeconds(2));

        Assert.NotEmpty(cut.FindAll("[role='alert']"));
        Assert.Contains("Hello World", cut.Markup);
    }

    [Fact]
    public void ToastProvider_Shows_Description_When_Provided()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>();

        toastService!.Show("Title", "My description");
        cut.WaitForState(() => cut.FindAll("[role='alert']").Count > 0, TimeSpan.FromSeconds(2));

        Assert.Contains("My description", cut.Markup);
    }

    [Fact]
    public void ToastProvider_Shows_Variant_Class_For_Destructive()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>();

        toastService!.Show("Error!", variant: ToastVariant.Destructive);
        cut.WaitForState(() => cut.FindAll("[role='alert']").Count > 0, TimeSpan.FromSeconds(2));

        var alert = cut.Find("[role='alert']");
        Assert.Contains("border-destructive", alert.GetAttribute("class") ?? "");
    }

    [Fact]
    public void ToastProvider_Dismiss_Removes_Toast()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        var cut = _ctx.Render<L.ToastProvider>();

        toastService!.Show("Dismiss me");
        cut.WaitForState(() => cut.FindAll("[role='alert']").Count > 0, TimeSpan.FromSeconds(2));

        // Click the close button
        cut.Find("button").Click();
        cut.WaitForState(() => cut.FindAll("[role='alert']").Count == 0, TimeSpan.FromSeconds(2));

        Assert.Empty(cut.FindAll("[role='alert']"));
    }

    [Fact]
    public void ToastProvider_Default_Position_Is_BottomRight()
    {
        var cut = _ctx.Render<L.ToastProvider>();

        // The viewport div should have bottom-right positioning classes
        var divs = cut.FindAll("div");
        Assert.True(divs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("bottom-4") && cls.Contains("right-4");
        }));
    }

    [Fact]
    public void ToastProvider_Custom_Position_TopLeft()
    {
        var cut = _ctx.Render<L.ToastProvider>(p => p
            .Add(b => b.Position, L.ToastViewport.ToastPosition.TopLeft));

        var divs = cut.FindAll("div");
        Assert.True(divs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("top-4") && cls.Contains("left-4");
        }));
    }
}
