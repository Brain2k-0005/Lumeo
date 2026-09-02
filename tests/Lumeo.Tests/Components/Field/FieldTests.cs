using Microsoft.AspNetCore.Components;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Field;

/// <summary>
/// The Field family is shadcn's form-layout primitive (field.tsx): every one of their
/// login and signup blocks is built from it. Geometry pinned here was measured live on
/// ui.shadcn.com on 2026-09-02: group gap 20px, field gap 8px, set gap 16px, label
/// 14px/500, description 14px muted, separator 20px tall pulled in by -my-2.
/// </summary>
public class FieldTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FieldTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void FieldGroup_Is_A_Column_With_Gap_5_And_A_Container_Root()
    {
        var cut = _ctx.Render<L.FieldGroup>(p => p.AddChildContent("x"));
        var cls = cut.Find("[data-slot=field-group]").ClassList;

        Assert.Contains("flex-col", cls);
        Assert.Contains("gap-5", cls);
        Assert.Contains("@container/field-group", cls);
    }

    [Fact]
    public void Field_Defaults_To_Vertical_With_Gap_2_And_A_Group_Role()
    {
        var cut = _ctx.Render<L.Field>(p => p.AddChildContent("x"));
        var el = cut.Find("[data-slot=field]");

        Assert.Equal("group", el.GetAttribute("role"));
        Assert.Equal("vertical", el.GetAttribute("data-orientation"));
        Assert.Contains("flex-col", el.ClassList);
        Assert.Contains("gap-2", el.ClassList);
    }

    [Theory]
    [InlineData(L.Field.FieldOrientation.Horizontal, "horizontal", "flex-row")]
    [InlineData(L.Field.FieldOrientation.Responsive, "responsive", "@md/field-group:flex-row")]
    public void Field_Orientation_Sets_The_Attribute_And_The_Layout(L.Field.FieldOrientation o, string attr, string marker)
    {
        var cut = _ctx.Render<L.Field>(p => p.Add(f => f.Orientation, o).AddChildContent("x"));
        var el = cut.Find("[data-slot=field]");

        Assert.Equal(attr, el.GetAttribute("data-orientation"));
        Assert.Contains(marker, el.GetAttribute("class"));
    }

    [Fact]
    public void FieldLabel_Is_A_Label_That_Fits_Its_Content()
    {
        var cut = _ctx.Render<L.FieldLabel>(p => p.Add(f => f.For, "email").AddChildContent("Email"));
        var label = cut.Find("label");

        Assert.Equal("email", label.GetAttribute("for"));
        Assert.Equal("field-label", label.GetAttribute("data-slot"));
        Assert.Contains("w-fit", label.ClassList);
        Assert.Contains("text-sm", label.ClassList);
        Assert.Contains("font-medium", label.ClassList);
    }

    [Fact]
    public void FieldDescription_Is_Muted_Body_Text_With_Underlined_Links()
    {
        var cut = _ctx.Render<L.FieldDescription>(p => p.AddChildContent("Enter your email"));
        var cls = cut.Find("p").GetAttribute("class")!;

        Assert.Contains("text-muted-foreground", cls);
        Assert.Contains("text-sm", cls);
        Assert.Contains("[&>a]:underline", cls);
    }

    [Fact]
    public void FieldSet_And_Legend_Carry_Their_Own_Rhythm()
    {
        var cut = _ctx.Render<L.FieldSet>(p => p.AddChildContent(b =>
        {
            b.OpenComponent<L.FieldLegend>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Address")));
            b.CloseComponent();
        }));

        Assert.Contains("gap-4", cut.Find("fieldset").ClassList);
        var legend = cut.Find("legend");
        Assert.Equal("legend", legend.GetAttribute("data-variant"));
        Assert.Contains("text-base", legend.ClassList);
    }

    [Fact]
    public void FieldLegend_Label_Variant_Sizes_Like_A_Field_Label()
    {
        var cut = _ctx.Render<L.FieldLegend>(p => p.Add(f => f.Variant, L.FieldLegend.LegendVariant.Label).AddChildContent("Plan"));
        var legend = cut.Find("legend");

        Assert.Equal("label", legend.GetAttribute("data-variant"));
        Assert.Contains("text-sm", legend.ClassList);
    }

    [Fact]
    public void FieldSeparator_Without_Content_Is_A_Bare_Rule()
    {
        var cut = _ctx.Render<L.FieldSeparator>();
        var root = cut.Find("[data-slot=field-separator]");

        Assert.Equal("false", root.GetAttribute("data-content"));
        Assert.Contains("h-5", root.ClassList);
        Assert.Contains("-my-2", root.ClassList);
        Assert.Empty(cut.FindAll("[data-slot=field-separator-content]"));
    }

    [Fact]
    public void FieldSeparator_With_Content_Sits_The_Text_On_The_Rule()
    {
        var cut = _ctx.Render<L.FieldSeparator>(p => p.AddChildContent("Or continue with"));
        var content = cut.Find("[data-slot=field-separator-content]");

        Assert.Equal("true", cut.Find("[data-slot=field-separator]").GetAttribute("data-content"));
        Assert.Contains("bg-background", content.ClassList);
        Assert.Equal("Or continue with", content.TextContent.Trim());
    }

    [Fact]
    public void FieldError_Renders_Nothing_When_There_Is_Nothing_To_Say()
    {
        var cut = _ctx.Render<L.FieldError>(p => p.Add(f => f.Errors, new string?[] { null, "", "  " }));

        Assert.Empty(cut.FindAll("[data-slot=field-error]"));
    }

    [Fact]
    public void FieldError_Collapses_Duplicates_And_Lists_Distinct_Messages()
    {
        var cut = _ctx.Render<L.FieldError>(p => p.Add(f => f.Errors, new[] { "Required", "Required", "Too short" }));
        var root = cut.Find("[data-slot=field-error]");

        Assert.Equal("alert", root.GetAttribute("role"));
        Assert.Contains("text-destructive", root.ClassList);
        Assert.Equal(2, cut.FindAll("li").Count);
    }

    [Fact]
    public void FieldError_With_One_Message_Renders_It_Inline()
    {
        var cut = _ctx.Render<L.FieldError>(p => p.Add(f => f.Errors, new[] { "Required" }));

        Assert.Empty(cut.FindAll("li"));
        Assert.Equal("Required", cut.Find("[data-slot=field-error]").TextContent.Trim());
    }

    [Fact]
    public void FieldError_Child_Content_Wins_Over_Errors()
    {
        var cut = _ctx.Render<L.FieldError>(p => p.Add(f => f.Errors, new[] { "Required" }).AddChildContent("Custom"));

        Assert.Equal("Custom", cut.Find("[data-slot=field-error]").TextContent.Trim());
    }

    [Fact]
    public void Every_Member_Merges_A_Custom_Class()
    {
        Assert.Contains("x-1", _ctx.Render<L.Field>(p => p.Add(f => f.Class, "x-1")).Find("div").ClassList);
        Assert.Contains("x-2", _ctx.Render<L.FieldGroup>(p => p.Add(f => f.Class, "x-2")).Find("div").ClassList);
        Assert.Contains("x-3", _ctx.Render<L.FieldContent>(p => p.Add(f => f.Class, "x-3")).Find("div").ClassList);
        Assert.Contains("x-4", _ctx.Render<L.FieldTitle>(p => p.Add(f => f.Class, "x-4")).Find("div").ClassList);
        Assert.Contains("x-5", _ctx.Render<L.FieldDescription>(p => p.Add(f => f.Class, "x-5")).Find("p").ClassList);
    }
}
