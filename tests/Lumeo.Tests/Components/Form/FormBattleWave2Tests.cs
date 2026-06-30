using System.ComponentModel.DataAnnotations;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Form;

/// <summary>
/// Battle-test wave-2 high-severity regressions for the Form component.
/// </summary>
public class FormBattleWave2Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FormBattleWave2Tests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ---------------------------------------------------------------------
    // Bug n=4: ResetValues() snapshot must refresh when the Model parameter
    // instance changes. The original code captured the snapshot once in
    // OnInitializedAsync and never refreshed it, so after a Model swap
    // ResetValues() restored the PREVIOUS model's values onto the new one.
    // ---------------------------------------------------------------------

    private class SnapshotModel
    {
        public string? Name { get; set; }
    }

    [Fact]
    public async Task ResetValues_After_Model_Instance_Swap_Restores_New_Model_Baseline()
    {
        // Baseline 1: first instance with Name="first".
        var modelA = new SnapshotModel { Name = "first" };

        var cut = _ctx.Render<L.Form<SnapshotModel>>(p => p
            .Add(f => f.Model, modelA)
            .AddChildContent("<button type=\"submit\">go</button>"));

        // Parent swaps in a brand-new Model instance with a different baseline.
        var modelB = new SnapshotModel { Name = "second" };
        cut.Render(p => p.Add(f => f.Model, modelB));

        // User edits the new model, then asks to reset.
        modelB.Name = "edited";
        await cut.InvokeAsync(() => cut.Instance.ResetValues());

        // ResetValues must restore modelB's OWN baseline ("second"), NOT clobber
        // it with modelA's stale snapshot ("first"). Without the fix the snapshot
        // is never refreshed, so modelB.Name would become "first".
        Assert.Equal("second", modelB.Name);
    }

    [Fact]
    public async Task ResetValues_Without_Swap_Still_Restores_Original_Baseline()
    {
        // Guard the common edit-then-reset flow on a single instance still works:
        // same-instance mutations must keep the ORIGINAL baseline.
        var model = new SnapshotModel { Name = "original" };

        var cut = _ctx.Render<L.Form<SnapshotModel>>(p => p
            .Add(f => f.Model, model)
            .AddChildContent("<button type=\"submit\">go</button>"));

        model.Name = "changed";
        await cut.InvokeAsync(() => cut.Instance.ResetValues());

        Assert.Equal("original", model.Name);
    }

    // ---------------------------------------------------------------------
    // Bug n=5: DataAnnotationsFormValidator must not silently drop class-level
    // (form-level) validation errors whose ValidationResult.MemberNames is empty
    // (e.g. IValidatableObject or a class-scoped validation attribute).
    // ---------------------------------------------------------------------

    private class FormLevelModel : IValidatableObject
    {
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Password != ConfirmPassword)
            {
                // Empty MemberNames => a form-level error.
                yield return new ValidationResult("Passwords do not match.");
            }
        }
    }

    [Fact]
    public void Validator_Captures_FormLevel_Error_Under_Sentinel_Key()
    {
        var validator = new L.DataAnnotationsFormValidator();
        var model = new FormLevelModel { Password = "a", ConfirmPassword = "b" };

        var errors = validator.Validate(model);

        // Without the fix, the empty-MemberNames result is dropped entirely and
        // the dictionary is empty.
        Assert.True(errors.ContainsKey(L.DataAnnotationsFormValidator.FormLevelErrorKey));
        Assert.Contains(
            "Passwords do not match.",
            errors[L.DataAnnotationsFormValidator.FormLevelErrorKey]);
    }

    [Fact]
    public void FormLevel_Validation_Failure_Blocks_OnValidSubmit()
    {
        // Integration: a class-level validation failure must keep the Form invalid
        // so OnValidSubmit never fires. Previously the dropped error left
        // FormContext.IsValid == true and the destructive submit ran anyway.
        var model = new FormLevelModel { Password = "a", ConfirmPassword = "b" };
        var validCount = 0;

        var cut = _ctx.Render<L.Form<FormLevelModel>>(p => p
            .Add(f => f.Model, model)
            .Add(f => f.Validator, new L.DataAnnotationsFormValidator())
            .Add(f => f.OnValidSubmit, (FormLevelModel _) => { validCount++; })
            .AddChildContent("<button type=\"submit\">go</button>"));

        cut.Find("form").Submit();
        Assert.Equal(0, validCount); // form-level error blocks submit

        // Fix the mismatch -> the form-level error clears and submit proceeds.
        model.ConfirmPassword = "a";
        cut.Find("form").Submit();
        Assert.Equal(1, validCount);
    }

    [Fact]
    public void Validator_Still_Maps_Member_Level_Errors_By_Member_Name()
    {
        // Ensure the form-level fallback did not regress ordinary member-level
        // errors (non-empty MemberNames must still key by the member name).
        var validator = new L.DataAnnotationsFormValidator();
        var model = new MemberLevelModel(); // Required Email is null

        var errors = validator.Validate(model);

        Assert.True(errors.ContainsKey(nameof(MemberLevelModel.Email)));
        Assert.False(errors.ContainsKey(L.DataAnnotationsFormValidator.FormLevelErrorKey));
    }

    private class MemberLevelModel
    {
        [Required]
        public string? Email { get; set; }
    }
}
