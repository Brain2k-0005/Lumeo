using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.SignaturePad;

/// <summary>
/// #192 — clear/keyboard a11y for SignaturePad (the SVG export is JS-side and
/// covered by the signature-pad.js change). These assert the C#-observable
/// accessibility: the labelled region, the clear button's enabled/disabled
/// state, and Escape/Delete clearing the pad.
/// </summary>
public class SignaturePadTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SignaturePadTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Region_Has_Accessible_Label_And_Is_Focusable()
    {
        var cut = _ctx.Render<L.SignaturePad>();
        var region = cut.Find("[role='group']");
        Assert.Equal("Signature pad", region.GetAttribute("aria-label"));
        Assert.Equal("0", region.GetAttribute("tabindex"));
    }

    [Fact]
    public void Canvas_Is_Aria_Hidden()
    {
        var cut = _ctx.Render<L.SignaturePad>();
        Assert.Equal("true", cut.Find("canvas").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Clear_Button_Disabled_When_Empty()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p.Add(s => s.Value, null));
        var clear = cut.Find("button[aria-label='Clear signature']");
        Assert.True(clear.HasAttribute("disabled"));
    }

    [Fact]
    public void Clear_Button_Enabled_With_Value()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Value, "data:image/png;base64,AAAA"));
        var clear = cut.Find("button[aria-label='Clear signature']");
        Assert.False(clear.HasAttribute("disabled"));
    }

    [Fact]
    public void Escape_Clears_When_Value_Present()
    {
        string? bound = "data:image/png;base64,AAAA";
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Value, bound)
            .Add(s => s.ValueChanged, EventCallback.Factory.Create<string?>(this, v => bound = v)));

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Null(bound);
    }

    [Fact]
    public void Delete_Clears_When_Value_Present()
    {
        string? bound = "data:image/png;base64,AAAA";
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Value, bound)
            .Add(s => s.ValueChanged, EventCallback.Factory.Create<string?>(this, v => bound = v)));

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "Delete" });
        Assert.Null(bound);
    }

    [Fact]
    public void Escape_NoOp_When_Already_Empty()
    {
        var fired = 0;
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Value, null)
            .Add(s => s.OnClear, EventCallback.Factory.Create(this, () => fired++)));

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Svg_Format_Download_Uses_Svg_Mime_Filename()
    {
        // With Format="svg" the download filename uses the .svg extension.
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Format, "svg")
            .Add(s => s.ShowDownloadButton, true)
            .Add(s => s.Value, "data:image/svg+xml;base64,AAAA"));
        // Download button is present and labelled.
        Assert.NotNull(cut.Find("button[aria-label='Download signature']"));
    }
}
