using System.ComponentModel.DataAnnotations;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.OverlayForm;

/// <summary>
/// Behavior/a11y tier for <see cref="L.OverlayForm"/>: the EditForm-in-an-overlay
/// contract. A valid submit fires OnValidSubmit carrying the edited model; an
/// INVALID submit is blocked (OnValidSubmit stays silent, OnInvalidSubmit fires,
/// DataAnnotations validation messages surface); and a Footer cancel button's
/// OnClick fires independently of submit. Uses the default DataAnnotationsValidator
/// baked into OverlayForm.
/// </summary>
public class OverlayFormBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OverlayFormBehaviorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class PersonModel
    {
        [Required(ErrorMessage = "Name is required")]
        public string? Name { get; set; }

        [Range(1, 120, ErrorMessage = "Age out of range")]
        public int Age { get; set; }
    }

    // A Body fragment that surfaces validation messages the way real usage does,
    // via the built-in ValidationSummary fed by OverlayForm's DataAnnotationsValidator.
    private static RenderFragment BodyWithValidationSummary() => builder =>
    {
        builder.OpenComponent<ValidationSummary>(0);
        builder.CloseComponent();
    };

    private static RenderFragment SubmitButtonFooter() =>
        b => b.AddMarkupContent(0, "<button type=\"submit\">Save</button>");

    [Fact]
    public void ValidSubmit_Fires_OnValidSubmit_With_Edited_Model()
    {
        var model = new PersonModel { Name = "Ada", Age = 36 };
        object? submittedModel = null;
        var invalidFired = false;

        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, model)
            .Add(o => o.OnValidSubmit, (EditContext ec) => submittedModel = ec.Model)
            .Add(o => o.OnInvalidSubmit, (EditContext _) => invalidFired = true)
            .Add(o => o.Body, BodyWithValidationSummary())
            .Add(o => o.Footer, SubmitButtonFooter()));

        cut.Find("form").Submit();

        // Valid path: OnValidSubmit fired and carried the exact model instance.
        Assert.Same(model, submittedModel);
        Assert.False(invalidFired);
    }

    [Fact]
    public void InvalidSubmit_Blocks_OnValidSubmit_And_Fires_OnInvalidSubmit()
    {
        // Name missing + Age below the Range floor => two DataAnnotations failures.
        var model = new PersonModel { Name = null, Age = 0 };
        var validFired = false;
        EditContext? invalidContext = null;

        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, model)
            .Add(o => o.OnValidSubmit, (EditContext _) => validFired = true)
            .Add(o => o.OnInvalidSubmit, (EditContext ec) => invalidContext = ec)
            .Add(o => o.Body, BodyWithValidationSummary())
            .Add(o => o.Footer, SubmitButtonFooter()));

        cut.Find("form").Submit();

        // The valid callback must NOT fire; the invalid callback must.
        Assert.False(validFired);
        Assert.NotNull(invalidContext);
        Assert.Same(model, invalidContext!.Model);
    }

    [Fact]
    public void InvalidSubmit_Surfaces_Validation_Messages_In_Markup()
    {
        var model = new PersonModel { Name = null, Age = 0 };

        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, model)
            .Add(o => o.Body, BodyWithValidationSummary())
            .Add(o => o.Footer, SubmitButtonFooter()));

        cut.Find("form").Submit();

        // The default DataAnnotationsValidator populated the EditContext, and the
        // ValidationSummary in the Body rendered the human-readable messages.
        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find("ul.validation-errors");
            Assert.Contains("Name is required", summary.TextContent);
            Assert.Contains("Age out of range", summary.TextContent);
        });
    }

    [Fact]
    public void Resubmit_After_Fixing_Fields_Transitions_Invalid_To_Valid()
    {
        var model = new PersonModel { Name = null, Age = 0 };
        var validCount = 0;
        var invalidCount = 0;

        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, model)
            .Add(o => o.OnValidSubmit, (EditContext _) => validCount++)
            .Add(o => o.OnInvalidSubmit, (EditContext _) => invalidCount++)
            .Add(o => o.Body, BodyWithValidationSummary())
            .Add(o => o.Footer, SubmitButtonFooter()));

        // First attempt: invalid, blocked.
        cut.Find("form").Submit();
        Assert.Equal(0, validCount);
        Assert.Equal(1, invalidCount);

        // Fix the fields and resubmit: now valid.
        model.Name = "Grace";
        model.Age = 42;
        cut.Find("form").Submit();
        Assert.Equal(1, validCount);
        Assert.Equal(1, invalidCount);
    }

    [Fact]
    public void Cancel_Button_OnClick_Fires_Without_Triggering_Submit()
    {
        var model = new PersonModel { Name = "Ada", Age = 36 };
        var cancelled = false;
        var submitted = false;

        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, model)
            .Add(o => o.OnValidSubmit, (EditContext _) => submitted = true)
            .Add(o => o.Body, BodyWithValidationSummary())
            .Add(o => o.Footer, (RenderFragment)(b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "type", "button");
                b.AddAttribute(2, "data-testid", "cancel");
                b.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, () => cancelled = true));
                b.AddContent(4, "Cancel");
                b.CloseElement();
            })));

        cut.Find("[data-testid='cancel']").Click();

        // Cancel ran; the form's submit callback did not.
        Assert.True(cancelled);
        Assert.False(submitted);
    }
}
