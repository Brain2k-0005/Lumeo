using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Form;

/// <summary>
/// Battle-test wave-2 (n=153, state-on-data-change, low): FormField's OnChange
/// async validation must NOT spuriously re-fire when the parent re-renders with a
/// new reference whose CONTENT is unchanged (e.g. an immutable "rebuild an equal
/// collection each render" update). The original guard used the static
/// object.Equals, which falls back to reference equality for sequences, so every
/// unrelated parent render kicked off a fresh validation pass the user never asked
/// for.
/// </summary>
public class FormFieldOnChangeRefireTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public FormFieldOnChangeRefireTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.FormField> RenderField(
        object? value,
        Func<object?, Task<string?>> validator)
        => _ctx.Render<L.FormField>(p => p
            .Add(f => f.AsyncValidator, validator)
            .Add(f => f.AsyncValidateOn, L.FormField.AsyncValidationTrigger.OnChange)
            .Add(f => f.Value, value));

    [Fact]
    public void OnChange_Does_Not_Refire_For_New_SameContent_Reference_Value()
    {
        var calls = 0;
        Func<object?, Task<string?>> validator = _ =>
        {
            calls++;
            return Task.FromResult<string?>(null);
        };

        // Initial render establishes the baseline value (first param-set is skipped,
        // so this must NOT validate).
        var cut = RenderField(new List<string> { "a", "b" }, validator);
        Assert.Equal(0, calls);

        // A REAL content change must trigger exactly one validation.
        cut.Render(p => p.Add(f => f.Value, new List<string> { "a", "b", "c" }));
        Assert.Equal(1, calls);

        // The parent now re-renders with a BRAND-NEW list reference that has the SAME
        // content as the last one. Without the fix the static object.Equals path treats
        // the new reference as a change and re-fires validation (calls would become 2).
        // With the content-aware comparison it is a no-op.
        cut.Render(p => p.Add(f => f.Value, new List<string> { "a", "b", "c" }));
        Assert.Equal(1, calls);

        // A further same-content rebuild still must not re-fire.
        cut.Render(p => p.Add(f => f.Value, new List<string> { "a", "b", "c" }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OnChange_Still_Fires_On_A_Genuine_Content_Change()
    {
        // Guard the happy path: a real edit (different content) must still validate,
        // so the de-duplication doesn't swallow legitimate changes.
        var calls = 0;
        Func<object?, Task<string?>> validator = _ =>
        {
            calls++;
            return Task.FromResult<string?>(null);
        };

        var cut = RenderField(new List<string> { "x" }, validator);
        Assert.Equal(0, calls);

        cut.Render(p => p.Add(f => f.Value, new List<string> { "x", "y" }));
        Assert.Equal(1, calls);

        cut.Render(p => p.Add(f => f.Value, new List<string> { "x", "y", "z" }));
        Assert.Equal(2, calls);
    }
}
