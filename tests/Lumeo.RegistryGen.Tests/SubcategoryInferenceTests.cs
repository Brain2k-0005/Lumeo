using Xunit;
using Lumeo.RegistryGen;

namespace Lumeo.RegistryGen.Tests;

public class SubcategoryInferenceTests
{
    [Theory]
    [InlineData("Input", "Forms", "Inputs")]
    [InlineData("Textarea", "Forms", "Inputs")]
    [InlineData("Select", "Forms", "Selection")]
    [InlineData("Combobox", "Forms", "Selection")]
    [InlineData("Button", "Forms", "Buttons & Actions")]
    [InlineData("Form", "Forms", "Form Composition")]
    [InlineData("FormField", "Forms", "Form Composition")]
    [InlineData("DatePicker", "Forms", "Specialized")]
    [InlineData("Slider", "Forms", "Specialized")]
    [InlineData("Table", "Data Display", "Tables")]
    [InlineData("Card", "Data Display", "Cards & Layout")]
    [InlineData("Timeline", "Data Display", "Lists & Trees")]
    [InlineData("Chart", "Data Display", "Charts")]
    [InlineData("Progress", "Data Display", "Status & Indicators")]
    public void InferSubcategory_returns_expected_subgroup(string component, string category, string expected)
    {
        var result = SubcategoryInferrer.Infer(component, category);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void InferSubcategory_returns_null_for_categories_without_subgroups()
    {
        Assert.Null(SubcategoryInferrer.Infer("Stack", "Layout"));
        Assert.Null(SubcategoryInferrer.Infer("Heading", "Typography"));
        Assert.Null(SubcategoryInferrer.Infer("Toast", "Feedback"));
    }

    [Fact]
    public void InferSubcategory_returns_null_for_unknown_form_component()
    {
        Assert.Null(SubcategoryInferrer.Infer("MysteryControl", "Forms"));
    }
}
