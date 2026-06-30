using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Form;

/// <summary>
/// Codex P2 — a FormField's <c>&lt;label for&gt;</c> must resolve to a real rendered element even for
/// picker variants that have no popover trigger button (TimePicker <c>Wheel</c> → TimeWheelPicker,
/// DatePicker <c>Inline</c> → Calendar / DateWheelPicker). Those variants now splat the FormField's
/// generated <c>EffectiveTriggerId</c> (+ <c>aria-describedby</c> + <c>role="group"</c>) onto their own
/// root element via <c>AdditionalAttributes</c>, the same way the popover-trigger button does in the
/// conforming branches — so the label/help/error wiring works on every variant, not just the default one.
/// </summary>
public class FormFieldNonTriggerVariantIdWiringTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FormFieldNonTriggerVariantIdWiringTests() => _ctx.AddLumeoServices();
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

    private static string AssertLabelForResolvesToARenderedElement(IRenderedComponent<IComponent> cut)
    {
        var labelFor = cut.Find("label").GetAttribute("for");
        Assert.False(string.IsNullOrEmpty(labelFor));
        // Must actually exist in the DOM — a non-empty `for` pointing at nothing is the bug being fixed.
        Assert.NotNull(cut.Find($"#{labelFor}"));
        return labelFor!;
    }

    [Fact]
    public void TimePicker_Wheel_Variant_Gets_A_Real_Label_For()
    {
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.TimePicker>(0);
            b.AddAttribute(1, "Variant", L.TimePicker.TimePickerVariant.Wheel);
            b.CloseComponent();
        });

        var id = AssertLabelForResolvesToARenderedElement(cut);
        // The id lands on TimeWheelPicker's own root (a group), not a popover trigger that doesn't exist.
        Assert.Equal("group", cut.Find($"#{id}").GetAttribute("role"));
    }

    [Fact]
    public void DatePicker_Inline_Variant_Gets_A_Real_Label_For()
    {
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.DatePicker>(0);
            b.AddAttribute(1, "Inline", true);
            b.CloseComponent();
        });

        AssertLabelForResolvesToARenderedElement(cut);
    }

    [Fact]
    public void DatePicker_Inline_Wheel_Variant_Gets_A_Real_Label_For()
    {
        // The Inline+Wheel combination renders DateWheelPicker directly (a third, distinct gap from the
        // plain Inline Calendar branch) — must be wired too.
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.DatePicker>(0);
            b.AddAttribute(1, "Inline", true);
            b.AddAttribute(2, "Variant", L.DatePicker.DatePickerVariant.Wheel);
            b.CloseComponent();
        });

        AssertLabelForResolvesToARenderedElement(cut);
    }

    [Fact]
    public void Conforming_TimePicker_List_Variant_Keeps_Its_Label_For()
    {
        // Guard: the List variant's popover-trigger button already carried EffectiveTriggerId before this
        // fix — confirm it still does (not accidentally double-wired or broken by the splat change).
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.TimePicker>(0);
            b.AddAttribute(1, "Variant", L.TimePicker.TimePickerVariant.List);
            b.CloseComponent();
        });

        var id = AssertLabelForResolvesToARenderedElement(cut);
        Assert.Equal("BUTTON", cut.Find($"#{id}").TagName);
    }

    // --- round-15: aria-labelledby on non-labelable group pickers ---

    [Fact]
    public void TimePicker_Wheel_Variant_Also_Wires_Aria_Labelledby_To_The_Label()
    {
        // Codex P2: `<label for>` does not reliably associate with a non-labelable element
        // (`<div role="group">`), so the group additionally wires aria-labelledby straight at the
        // FormField's own <label> id.
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.TimePicker>(0);
            b.AddAttribute(1, "Variant", L.TimePicker.TimePickerVariant.Wheel);
            b.CloseComponent();
        });

        var labelId = cut.Find("label").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(labelId));

        var group = cut.Find("[role='group']");
        Assert.Equal(labelId, group.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void DatePicker_Inline_Variant_Also_Wires_Aria_Labelledby_To_The_Label()
    {
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.DatePicker>(0);
            b.AddAttribute(1, "Inline", true);
            b.CloseComponent();
        });

        var labelId = cut.Find("label").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(labelId));

        var group = cut.Find("[role='group']");
        Assert.Equal(labelId, group.GetAttribute("aria-labelledby"));
    }

    // --- round-15: compound pickers (date + nested time-of-day) must not duplicate the FormField id ---

    [Fact]
    public void DatePicker_Inline_With_ShowTimePicker_Does_Not_Duplicate_The_FormField_Id()
    {
        // Codex P2: ShowTimePicker nests a <TimePicker> for the time-of-day portion inside the SAME
        // FormField cascade as the outer DatePicker. Left cascaded, the inner TimePicker would ALSO
        // compute the same FormFieldControlId and render a duplicate DOM id — ambiguous label/focus
        // target. The nested TimePicker must be detached from the cascade.
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.DatePicker>(0);
            b.AddAttribute(1, "Inline", true);
            b.AddAttribute(2, "ShowTimePicker", true);
            b.CloseComponent();
        });

        var labelFor = cut.Find("label").GetAttribute("for");
        var matches = cut.FindAll($"#{labelFor}");
        Assert.Single(matches);
    }

    [Fact]
    public void DatePicker_Inline_Wheel_With_ShowTimePicker_Does_Not_Duplicate_The_FormField_Id()
    {
        var cut = RenderField(b =>
        {
            b.OpenComponent<L.DatePicker>(0);
            b.AddAttribute(1, "Inline", true);
            b.AddAttribute(2, "Variant", L.DatePicker.DatePickerVariant.Wheel);
            b.AddAttribute(3, "ShowTimePicker", true);
            b.CloseComponent();
        });

        var labelFor = cut.Find("label").GetAttribute("for");
        var matches = cut.FindAll($"#{labelFor}");
        Assert.Single(matches);
    }
}
