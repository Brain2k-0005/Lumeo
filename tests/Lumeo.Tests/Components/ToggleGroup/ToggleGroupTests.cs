using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ToggleGroup;

public class ToggleGroupTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public ToggleGroupTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    private IRenderedComponent<IComponent> RenderToggleGroup(
        L.ToggleGroup.ToggleGroupType type = L.ToggleGroup.ToggleGroupType.Single,
        L.ToggleGroup.ToggleGroupVariant variant = L.ToggleGroup.ToggleGroupVariant.Default,
        L.ToggleGroup.ToggleGroupSize size = L.ToggleGroup.ToggleGroupSize.Default,
        string? customClass = null,
        RenderFragment? children = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ToggleGroup>(0);
            builder.AddAttribute(1, "Type", type);
            builder.AddAttribute(2, "Variant", variant);
            builder.AddAttribute(3, "Size", size);
            if (customClass != null)
                builder.AddAttribute(4, "Class", customClass);
            builder.AddAttribute(5, "ChildContent", children ?? (RenderFragment)(b =>
            {
                b.OpenComponent<L.ToggleGroupItem>(0);
                b.AddAttribute(1, "Value", "a");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                b.CloseComponent();

                b.OpenComponent<L.ToggleGroupItem>(1);
                b.AddAttribute(2, "Value", "b");
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "B")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Rendering ---

    [Fact]
    public void Renders_Group_Role()
    {
        var cut = RenderToggleGroup();
        Assert.NotNull(cut.Find("[role='group']"));
    }

    [Fact]
    public void Renders_Buttons_For_Each_Item()
    {
        var cut = RenderToggleGroup();
        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);
    }

    [Fact]
    public void Items_Initial_AriaPressed_False()
    {
        var cut = RenderToggleGroup();
        var buttons = cut.FindAll("button");
        Assert.All(buttons, b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Renders_Item_ChildContent()
    {
        var cut = RenderToggleGroup();
        var buttons = cut.FindAll("button");
        Assert.Equal("A", buttons[0].TextContent.Trim());
        Assert.Equal("B", buttons[1].TextContent.Trim());
    }

    // --- Single mode ---

    [Fact]
    public void Single_Mode_Clicking_Item_Sets_AriaPressed_True()
    {
        var cut = RenderToggleGroup(type: L.ToggleGroup.ToggleGroupType.Single);
        cut.FindAll("button")[0].Click();
        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Single_Mode_Clicking_Second_Item_Deselects_First()
    {
        var cut = RenderToggleGroup(type: L.ToggleGroup.ToggleGroupType.Single);
        cut.FindAll("button")[0].Click();
        cut.FindAll("button")[1].Click();

        Assert.Equal("false", cut.FindAll("button")[0].GetAttribute("aria-pressed"));
        Assert.Equal("true", cut.FindAll("button")[1].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Single_Mode_Clicking_Same_Item_Twice_Deselects_It()
    {
        var cut = RenderToggleGroup(type: L.ToggleGroup.ToggleGroupType.Single);
        var buttons = cut.FindAll("button");
        buttons[0].Click();
        cut.FindAll("button")[0].Click();
        Assert.Equal("false", cut.FindAll("button")[0].GetAttribute("aria-pressed"));
    }

    // --- Multiple mode ---

    [Fact]
    public void Multiple_Mode_Can_Select_Multiple_Items()
    {
        var cut = RenderToggleGroup(type: L.ToggleGroup.ToggleGroupType.Multiple);
        cut.FindAll("button")[0].Click();
        cut.FindAll("button")[1].Click();

        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));
        Assert.Equal("true", cut.FindAll("button")[1].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Multiple_Mode_Clicking_Selected_Item_Deselects_It()
    {
        var cut = RenderToggleGroup(type: L.ToggleGroup.ToggleGroupType.Multiple);
        cut.FindAll("button")[0].Click();
        cut.FindAll("button")[0].Click();

        Assert.Equal("false", cut.FindAll("button")[0].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Multiple_Mode_Selecting_One_Does_Not_Deselect_Others()
    {
        var cut = RenderToggleGroup(type: L.ToggleGroup.ToggleGroupType.Multiple);
        cut.FindAll("button")[0].Click();
        cut.FindAll("button")[1].Click();
        cut.FindAll("button")[1].Click(); // deselect second

        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.FindAll("button")[1].GetAttribute("aria-pressed"));
    }

    // --- Variants ---

    [Fact]
    public void Default_Variant_Item_Has_Transparent_Background_When_Not_Selected()
    {
        var cut = RenderToggleGroup(variant: L.ToggleGroup.ToggleGroupVariant.Default);
        var cls = cut.FindAll("button")[0].GetAttribute("class") ?? "";
        Assert.Contains("bg-transparent", cls);
    }

    [Fact]
    public void Default_Variant_Item_Has_Accent_Background_When_Selected()
    {
        var cut = RenderToggleGroup(variant: L.ToggleGroup.ToggleGroupVariant.Default);
        cut.FindAll("button")[0].Click();
        var cls = cut.FindAll("button")[0].GetAttribute("class") ?? "";
        Assert.Contains("bg-accent", cls);
    }

    [Fact]
    public void Outline_Variant_Item_Has_Border_When_Not_Selected()
    {
        var cut = RenderToggleGroup(variant: L.ToggleGroup.ToggleGroupVariant.Outline);
        var cls = cut.FindAll("button")[0].GetAttribute("class") ?? "";
        Assert.Contains("border", cls);
        Assert.Contains("border-input", cls);
    }

    [Fact]
    public void Outline_Variant_Item_Has_Accent_And_Border_When_Selected()
    {
        var cut = RenderToggleGroup(variant: L.ToggleGroup.ToggleGroupVariant.Outline);
        cut.FindAll("button")[0].Click();
        var cls = cut.FindAll("button")[0].GetAttribute("class") ?? "";
        Assert.Contains("bg-accent", cls);
        Assert.Contains("border-input", cls);
    }

    // --- Sizes ---

    [Theory]
    [InlineData(L.ToggleGroup.ToggleGroupSize.Default, "h-9", "px-3")]
    [InlineData(L.ToggleGroup.ToggleGroupSize.Sm, "h-8", "px-2")]
    [InlineData(L.ToggleGroup.ToggleGroupSize.Lg, "h-10", "px-4")]
    public void Item_Renders_Correct_Size_Classes(L.ToggleGroup.ToggleGroupSize size, string heightClass, string paddingClass)
    {
        var cut = RenderToggleGroup(size: size);
        var cls = cut.FindAll("button")[0].GetAttribute("class") ?? "";
        Assert.Contains(heightClass, cls);
        Assert.Contains(paddingClass, cls);
    }

    // --- Disabled ---

    [Fact]
    public void Disabled_Item_Has_Disabled_Attribute()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ToggleGroup>(0);
            builder.AddAttribute(1, "Type", L.ToggleGroup.ToggleGroupType.Single);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ToggleGroupItem>(0);
                b.AddAttribute(1, "Value", "a");
                b.AddAttribute(2, "Disabled", true);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotNull(cut.Find("button[disabled]"));
    }

    [Fact]
    public void Disabled_Item_Click_Does_Not_Select()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ToggleGroup>(0);
            builder.AddAttribute(1, "Type", L.ToggleGroup.ToggleGroupType.Single);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ToggleGroupItem>(0);
                b.AddAttribute(1, "Value", "a");
                b.AddAttribute(2, "Disabled", true);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("button").Click();
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
    }

    // --- Custom Class ---

    [Fact]
    public void Custom_Class_Is_Applied_To_Group()
    {
        var cut = RenderToggleGroup(customClass: "my-toggle-group");
        var cls = cut.Find("[role='group']").GetAttribute("class") ?? "";
        Assert.Contains("my-toggle-group", cls);
        Assert.Contains("flex", cls);
    }

    [Fact]
    public void Item_Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ToggleGroup>(0);
            builder.AddAttribute(1, "Type", L.ToggleGroup.ToggleGroupType.Single);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ToggleGroupItem>(0);
                b.AddAttribute(1, "Value", "a");
                b.AddAttribute(2, "Class", "custom-item");
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("custom-item", cls);
    }
}
