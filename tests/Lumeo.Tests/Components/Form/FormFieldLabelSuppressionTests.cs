using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Form;

/// <summary>
/// Codex P2 — a FormField must not dangle its label's <c>for</c> at an id no element renders. Picker
/// variants that render no element bearing the cascaded ControlId (TimePicker <c>Wheel</c> →
/// TimeWheelPicker, DatePicker <c>Inline</c> → Calendar) signal the FormField to DROP the <c>for</c>;
/// conforming variants (which DO render the ControlId) keep it.
/// </summary>
public class FormFieldLabelSuppressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FormFieldLabelSuppressionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderField(RenderFragment child)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Time");
            builder.AddAttribute(2, "ChildContent", child);
            builder.CloseComponent();
        });

    [Fact]
    public void TimePicker_Wheel_Variant_Suppresses_The_Dangling_Label_For()
    {
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.TimePicker>(0);
            b.AddAttribute(1, "Variant", L.TimePicker.TimePickerVariant.Wheel);
            b.CloseComponent();
        });

        // The Wheel renders no element with the ControlId, so the label must NOT point a `for` at it.
        Assert.True(string.IsNullOrEmpty(cut.Find("label").GetAttribute("for")));
    }

    [Fact]
    public void DatePicker_Inline_Variant_Suppresses_The_Dangling_Label_For()
    {
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.DatePicker>(0);
            b.AddAttribute(1, "Inline", true);
            b.CloseComponent();
        });

        Assert.True(string.IsNullOrEmpty(cut.Find("label").GetAttribute("for")));
    }

    [Fact]
    public void Conforming_TimePicker_List_Variant_Keeps_The_Label_For()
    {
        // Guard against over-suppression: the List variant DOES render the trigger button with the
        // ControlId, so the label's `for` must stay and resolve to a real rendered element.
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.TimePicker>(0);
            b.AddAttribute(1, "Variant", L.TimePicker.TimePickerVariant.List);
            b.CloseComponent();
        });

        var labelFor = cut.Find("label").GetAttribute("for");
        Assert.False(string.IsNullOrEmpty(labelFor));
        // The id the label points at must actually exist in the DOM (the List trigger carries it).
        Assert.NotNull(cut.Find($"#{labelFor}"));
    }
}
