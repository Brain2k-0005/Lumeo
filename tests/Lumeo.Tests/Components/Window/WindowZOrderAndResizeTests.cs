using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Window;

/// <summary>
/// Regression tests for #236 — Window hardcoded z-40 on every state (so a
/// background window couldn't be raised by clicking it) and resize had only a
/// lower bound (a window near an edge grew off-screen). Also adds Escape-to-
/// close and a labelled resize handle.
/// </summary>
public class WindowZOrderAndResizeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public WindowZOrderAndResizeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static int ZIndexOf(AngleSharp.Dom.IElement el)
    {
        var style = el.GetAttribute("style") ?? "";
        var marker = "z-index:";
        var i = style.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(i >= 0, $"no z-index in style: {style}");
        var rest = style[(i + marker.Length)..].TrimStart();
        var digits = new string(rest.TakeWhile(char.IsDigit).ToArray());
        return int.Parse(digits);
    }

    [Fact]
    public void Window_ZIndex_Is_Set_Inline_Not_Hardcoded_z40_Class()
    {
        var cut = _ctx.Render<L.Window>(p => p.Add(w => w.Open, true).Add(w => w.Title, "W"));
        var dialog = cut.Find("[role='dialog']");
        Assert.DoesNotContain("z-40", dialog.GetAttribute("class") ?? "");
        Assert.Contains("z-index:", dialog.GetAttribute("style") ?? "");
    }

    [Fact]
    public void Clicking_A_Window_Brings_It_To_Front()
    {
        // Two windows; the second opens above the first.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Window>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Title", "First");
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, "a")));
            builder.CloseComponent();

            builder.OpenComponent<L.Window>(4);
            builder.AddAttribute(5, "Open", true);
            builder.AddAttribute(6, "Title", "Second");
            builder.AddAttribute(7, "ChildContent", (RenderFragment)(b => b.AddContent(0, "b")));
            builder.CloseComponent();
        });

        var dialogs = cut.FindAll("[role='dialog']");
        Assert.Equal(2, dialogs.Count);
        var first = dialogs[0];
        var second = dialogs[1];
        Assert.True(ZIndexOf(second) > ZIndexOf(first), "second window should open on top");

        // Click (pointerdown) the first window — it must come to the front.
        first.PointerDown(new PointerEventArgs { PointerId = 1 });

        var firstAfter = cut.FindAll("[role='dialog']")[0];
        var secondAfter = cut.FindAll("[role='dialog']")[1];
        Assert.True(ZIndexOf(firstAfter) > ZIndexOf(secondAfter), "clicked window should now be on top");
    }

    [Fact]
    public void Resize_Is_Clamped_To_Viewport()
    {
        // Small viewport so growing past it is clamped. Window opens at 80,80.
        _interop.ViewportSize = new ViewportSize(400, 300);
        var cut = _ctx.Render<L.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.Title, "W")
            .AddChildContent("body"));

        var handle = cut.Find(".cursor-se-resize");
        handle.PointerDown(new PointerEventArgs { PointerId = 1, ClientX = 10, ClientY = 10 });
        // Drag far beyond the viewport.
        handle.PointerMove(new PointerEventArgs { PointerId = 1, ClientX = 5000, ClientY = 5000 });

        var style = cut.Find("[role='dialog']").GetAttribute("style") ?? "";
        // maxWidth = 400 - 80 - 8 = 312; maxHeight = 300 - 80 - 8 = 212.
        Assert.Contains("width:312px", style);
        Assert.Contains("height:212px", style);
    }

    [Fact]
    public void Escape_Closes_The_Window()
    {
        var closed = false;
        var cut = _ctx.Render<L.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.Title, "W")
            .Add(w => w.OpenChanged, EventCallback.Factory.Create<bool>(this, v => closed = !v))
            .AddChildContent("body"));

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.True(closed);
    }

    [Fact]
    public void Resize_Handle_Has_Role_And_AriaLabel()
    {
        var cut = _ctx.Render<L.Window>(p => p.Add(w => w.Open, true).Add(w => w.Title, "W"));
        var handle = cut.Find(".cursor-se-resize");
        Assert.Equal("button", handle.GetAttribute("role"));
        Assert.False(string.IsNullOrEmpty(handle.GetAttribute("aria-label")));
    }
}
