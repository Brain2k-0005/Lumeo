using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Checkbox;

/// <summary>
/// Pattern B regression — FormField splat-id override.
///
/// A consumer-splatted <c>id="..."</c> inside a FormField must not override the
/// generated FormFieldControlId, because the FormField's <c>&lt;label for&gt;</c>
/// points at that ControlId. Before the fix the splat rendered last (via
/// <c>@attributes</c>) and silently won, leaving the FormField's label targeting an
/// id that no longer existed on the button. Also guards that standalone Checkboxes
/// still receive the consumer's splatted id unchanged (no regression on Triage #23).
/// </summary>
public class CheckboxFormFieldTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CheckboxFormFieldTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Consumer_Splatted_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        // Regression (Pattern B): a consumer-splatted id must be stripped when the
        // Checkbox is inside a FormField, so the FormField label's `for` always
        // matches the button's generated id. Before the fix the splat rendered last
        // and overrode the generated id, orphaning the FormField label.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Accept terms");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.Checkbox>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = "consumer-id" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var buttonId = cut.Find("button[role='checkbox']").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");
        Assert.False(string.IsNullOrEmpty(buttonId));
        // The FormField label for and the button id must agree.
        Assert.Equal(labelFor, buttonId);
        // The consumer-splatted "consumer-id" must have been stripped in favour of the
        // generated FormFieldControlId so the label still targets the button.
        Assert.NotEqual("consumer-id", buttonId);
    }

    [Fact]
    public void Consumer_Splatted_Id_Outside_FormField_Still_Reaches_Button()
    {
        // Guard: id-stripping only applies inside a FormField. A standalone Checkbox
        // with a consumer-supplied id must still receive that id on the button (Triage #23).
        var cut = _ctx.Render<L.Checkbox>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "standalone-id" }));

        Assert.Equal("standalone-id", cut.Find("button[role='checkbox']").GetAttribute("id"));
    }
}
