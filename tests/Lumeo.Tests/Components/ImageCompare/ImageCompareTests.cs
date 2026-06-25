using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ImageCompare;

public class ImageCompareTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ImageCompareTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/before.jpg")
            .Add(c => c.AfterSrc, "/after.jpg"));
        // The slider input should be present
        Assert.NotEmpty(cut.FindAll("input[type='range']"));
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.Class, "compare-cls"));
        Assert.Contains("compare-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "ic" }));
        Assert.Contains("data-testid=\"ic\"", cut.Markup);
    }

    [Fact]
    public void Renders_before_and_after_labels()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.BeforeLabel, "Before")
            .Add(c => c.AfterLabel, "After"));
        Assert.Contains("Before", cut.Markup);
        Assert.Contains("After", cut.Markup);
    }

    [Fact]
    public void Renders_vertical_orientation_slider()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.Orientation, L.Orientation.Vertical));
        Assert.Contains("ns-resize", cut.Markup);
    }

    // --- Full 0–100 travel + ARIA valuetext (#288) ---

    [Fact]
    public void Slider_Has_Slider_Role_And_Value_Bounds()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg"));
        var slider = cut.Find("input[type='range']");
        Assert.Equal("slider", slider.GetAttribute("role"));
        Assert.Equal("0", slider.GetAttribute("aria-valuemin"));
        Assert.Equal("100", slider.GetAttribute("aria-valuemax"));
    }

    [Fact]
    public void Initial_Position_Zero_Is_Allowed()
    {
        // The old [1,99] clamp could never reach 0; now it can.
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.InitialPosition, 0));
        var slider = cut.Find("input[type='range']");
        Assert.Equal("0", slider.GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Initial_Position_Hundred_Is_Allowed()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.InitialPosition, 100));
        var slider = cut.Find("input[type='range']");
        Assert.Equal("100", slider.GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Slider_Has_ValueText()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.BeforeLabel, "Before")
            .Add(c => c.InitialPosition, 50));
        var slider = cut.Find("input[type='range']");
        var vt = slider.GetAttribute("aria-valuetext") ?? "";
        Assert.Contains("Before", vt);
        Assert.Contains("50", vt);
    }

    [Fact]
    public void Input_Beyond_Range_Clamps_To_Hundred()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg"));
        var slider = cut.Find("input[type='range']");
        slider.Input("1000"); // beyond max — must clamp to 100
        Assert.Equal("100", cut.Find("input[type='range']").GetAttribute("aria-valuenow"));
    }

    // --- Regression: battle-wave2 #134 (keyboard-a11y) ---
    // The badge label and the image's accessible name (<img alt>) are separate
    // concerns. Hiding a badge (BeforeLabel/AfterLabel = "") must NOT blank the
    // alt — the alt falls back to the localized "Before"/"After" so the image is
    // always named. Before the fix, alt was bound straight to the label, so an
    // empty label produced alt="" (no accessible name).

    [Fact]
    public void Hiding_Before_Label_Keeps_Before_Image_Alt()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.BeforeLabel, "")
            .Add(c => c.AfterLabel, "Edited"));

        // The before image is the second <img> (the clip-path overlay).
        var beforeImg = cut.FindAll("img")[1];
        var alt = beforeImg.GetAttribute("alt") ?? "";
        Assert.False(string.IsNullOrEmpty(alt));
        Assert.Equal("Before", alt);
    }

    [Fact]
    public void Hiding_After_Label_Keeps_After_Image_Alt()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.BeforeLabel, "Original")
            .Add(c => c.AfterLabel, ""));

        // The after image is the first <img> (the full-size background).
        var afterImg = cut.FindAll("img")[0];
        var alt = afterImg.GetAttribute("alt") ?? "";
        Assert.False(string.IsNullOrEmpty(alt));
        Assert.Equal("After", alt);
    }

    [Fact]
    public void Custom_Labels_Are_Used_As_Image_Alt()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.BeforeLabel, "Original")
            .Add(c => c.AfterLabel, "Edited"));

        Assert.Equal("Edited", cut.FindAll("img")[0].GetAttribute("alt"));
        Assert.Equal("Original", cut.FindAll("img")[1].GetAttribute("alt"));
    }
}
