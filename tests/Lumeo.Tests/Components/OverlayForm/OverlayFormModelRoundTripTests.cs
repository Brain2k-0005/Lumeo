using System.ComponentModel.DataAnnotations;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.OverlayForm;

/// <summary>
/// Regression for triage #45 (state-on-data-change, medium): the EditContext —
/// and with it the validation + field-modified state — must survive a transient
/// Model round-trip through null back to the SAME instance (e.g. a parent that
/// briefly clears Model while reloading and then restores the original object).
///
/// Before the fix, OnParametersSet cleared _lastModel on the null pass, so when
/// the same instance returned the ReferenceEquals guard saw a "change" and built
/// a fresh EditContext — discarding everything the previous context held. These
/// tests fail on that old behavior and pass once the context is preserved.
/// </summary>
public class OverlayFormModelRoundTripTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OverlayFormModelRoundTripTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class PersonModel
    {
        [Required(ErrorMessage = "Name is required")]
        public string? Name { get; set; }
    }

    private static RenderFragment BodyWithValidationSummary() => builder =>
    {
        builder.OpenComponent<ValidationSummary>(0);
        builder.CloseComponent();
    };

    private static RenderFragment SubmitButtonFooter() =>
        b => b.AddMarkupContent(0, "<button type=\"submit\">Save</button>");

    [Fact]
    public void EditContext_Instance_Survives_Transient_Null_Back_To_Same_Model()
    {
        var model = new PersonModel { Name = "Ada" };
        var capturedContexts = new List<EditContext>();

        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, model)
            .Add(o => o.OnValidSubmit, (EditContext ec) => capturedContexts.Add(ec))
            .Add(o => o.Body, BodyWithValidationSummary())
            .Add(o => o.Footer, SubmitButtonFooter()));

        // First submit records the EditContext the form built for this instance.
        cut.Find("form").Submit();

        // Parent briefly clears Model (reload), then restores the SAME instance.
        cut.Render(p => p.Add(o => o.Model, (object?)null));
        cut.Render(p => p.Add(o => o.Model, model));

        // Second submit must carry the very same EditContext: a transient null
        // round-trip on an unchanged instance must not rebuild it.
        cut.Find("form").Submit();

        Assert.Equal(2, capturedContexts.Count);
        Assert.Same(capturedContexts[0], capturedContexts[1]);
    }

    [Fact]
    public void Validation_State_Survives_Transient_Null_Back_To_Same_Model()
    {
        var model = new PersonModel { Name = null };

        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, model)
            .Add(o => o.Body, BodyWithValidationSummary())
            .Add(o => o.Footer, SubmitButtonFooter()));

        // Submit invalid: the DataAnnotationsValidator records a message on the
        // EditContext and the ValidationSummary renders it.
        cut.Find("form").Submit();
        cut.WaitForAssertion(() =>
            Assert.Contains("Name is required", cut.Find("ul.validation-errors").TextContent));

        // Transient null round-trip back to the same instance.
        cut.Render(p => p.Add(o => o.Model, (object?)null));
        cut.Render(p => p.Add(o => o.Model, model));

        // The previously-recorded validation message must still be present: a
        // rebuilt EditContext would have wiped it (the summary would be empty).
        Assert.Contains("Name is required", cut.Find("ul.validation-errors").TextContent);
    }

    [Fact]
    public void EditContext_Rebuilds_When_Model_Changes_To_A_Different_Instance()
    {
        var first = new PersonModel { Name = "Ada" };
        var second = new PersonModel { Name = "Grace" };
        var capturedContexts = new List<EditContext>();

        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, (object?)first)
            .Add(o => o.OnValidSubmit, (EditContext ec) => capturedContexts.Add(ec))
            .Add(o => o.Body, BodyWithValidationSummary())
            .Add(o => o.Footer, SubmitButtonFooter()));

        cut.Find("form").Submit();

        // A genuine instance change MUST rebuild the context (still controlled).
        cut.Render(p => p.Add(o => o.Model, (object?)second));
        cut.Find("form").Submit();

        Assert.Equal(2, capturedContexts.Count);
        Assert.NotSame(capturedContexts[0], capturedContexts[1]);
        Assert.Same(first, capturedContexts[0].Model);
        Assert.Same(second, capturedContexts[1].Model);
    }
}
