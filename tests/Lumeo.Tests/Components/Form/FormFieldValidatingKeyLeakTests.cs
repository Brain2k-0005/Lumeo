using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Form;

/// <summary>
/// Battle-test wave-2 (n=152, lifecycle, low): FormField registers itself in
/// <see cref="L.FormContext.ValidatingFields"/> under <c>FieldKey</c> when an async
/// validation starts, and must un-register under the SAME key when it finishes (or
/// the component is disposed). The original code recomputed <c>FieldKey</c> from the
/// live <c>Name</c> at every call, so if the <c>Name</c> parameter changed while a
/// validation was in flight the terminal un-register targeted the NEW key, leaving
/// the OLD key stuck in the set forever — <see cref="L.FormContext.IsAnyFieldValidating"/>
/// then never returned to false (e.g. a submit button stays disabled permanently).
///
/// The FormField is hosted inside <see cref="ValidatingKeyLeakHost"/>, which provides
/// the cascading <see cref="L.FormContext"/> ONCE and exposes a <c>FieldName</c>
/// parameter that flows into the FormField's <c>Name</c>. Re-rendering the HOST
/// (not the FormField) with a new <c>FieldName</c> drives the runtime Name change
/// without bUnit's "cannot provide a new cascading value through the Render method"
/// restriction.
/// </summary>
public class FormFieldValidatingKeyLeakTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public FormFieldValidatingKeyLeakTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Renders the host (which supplies the cascading FormContext once) with a FormField
    // whose Name starts as <paramref name="name"/>. The host is the cut so its FieldName
    // can be re-rendered to change the FormField's Name at runtime.
    private IRenderedComponent<ValidatingKeyLeakHost> RenderHost(
        L.FormContext context,
        string name,
        Func<object?, Task<string?>> validator)
        => _ctx.Render<ValidatingKeyLeakHost>(p => p
            .Add(h => h.Context, context)
            .Add(h => h.FieldName, name)
            .Add(h => h.AsyncValidator, validator));

    [Fact]
    public async Task Name_Change_Mid_Validation_Does_Not_Leak_The_Old_Validating_Key()
    {
        var ctx = new L.FormContext();

        // A validator the test holds open so validation stays in flight while we swap Name.
        var gate = new TaskCompletionSource<string?>();
        Func<object?, Task<string?>> validator = _ => gate.Task;

        var host = RenderHost(ctx, "email", validator);
        var field = host.FindComponent<L.FormField>();

        // Kick off validation (no debounce => RunValidationAsync runs immediately and
        // awaits the gate). ValidateAsync returns before the inner task registers, so
        // wait until the field is recorded as validating under its original Name.
        await field.InvokeAsync(() => field.Instance.ValidateAsync());
        host.WaitForState(() => ctx.IsAnyFieldValidating);
        Assert.Contains("email", ctx.ValidatingFields);

        // The field's Name parameter changes WHILE validation is still in flight — driven
        // by re-rendering the HOST's FieldName, so the cascade is never re-provided.
        host.Render(p => p
            .Add(h => h.Context, ctx)
            .Add(h => h.FieldName, "phone")
            .Add(h => h.AsyncValidator, validator));

        // Complete the in-flight validation. The terminal un-register must clear the
        // key the field actually registered with ("email"), not the current FieldKey
        // ("phone"). Without the fix "email" leaks and IsAnyFieldValidating stays true.
        gate.SetResult(null);

        host.WaitForAssertion(() =>
        {
            Assert.False(ctx.IsAnyFieldValidating);
            Assert.DoesNotContain("email", ctx.ValidatingFields);
            Assert.DoesNotContain("phone", ctx.ValidatingFields);
        });
    }

    [Fact]
    public async Task Dispose_Mid_Validation_Clears_The_Original_Validating_Key_After_A_Name_Change()
    {
        // Same leak via the disposal path: if the field is torn down while validation
        // is in flight AND its Name changed first, Dispose must clear the ORIGINAL key.
        var ctx = new L.FormContext();
        var gate = new TaskCompletionSource<string?>();
        Func<object?, Task<string?>> validator = _ => gate.Task;

        var host = RenderHost(ctx, "email", validator);
        var field = host.FindComponent<L.FormField>();

        await field.InvokeAsync(() => field.Instance.ValidateAsync());
        host.WaitForState(() => ctx.IsAnyFieldValidating);
        Assert.Contains("email", ctx.ValidatingFields);

        // Name changes mid-flight (via the host), then the component is disposed.
        host.Render(p => p
            .Add(h => h.Context, ctx)
            .Add(h => h.FieldName, "phone")
            .Add(h => h.AsyncValidator, validator));

        ((IDisposable)field.Instance).Dispose();

        // Disposal must un-register the key the field registered with ("email"),
        // leaving the set empty rather than leaking the stale entry.
        Assert.False(ctx.IsAnyFieldValidating);
        Assert.DoesNotContain("email", ctx.ValidatingFields);
    }

    [Fact]
    public async Task Normal_Validation_Cycle_Without_A_Name_Change_Still_Clears_The_Key()
    {
        // Guard the happy path: when Name does NOT change, the validating key must be
        // registered and then cleared exactly as before (no behavior change).
        var ctx = new L.FormContext();
        var gate = new TaskCompletionSource<string?>();
        Func<object?, Task<string?>> validator = _ => gate.Task;

        var host = RenderHost(ctx, "email", validator);
        var field = host.FindComponent<L.FormField>();

        await field.InvokeAsync(() => field.Instance.ValidateAsync());
        host.WaitForState(() => ctx.IsAnyFieldValidating);
        Assert.Contains("email", ctx.ValidatingFields);

        gate.SetResult(null);

        host.WaitForAssertion(() =>
        {
            Assert.False(ctx.IsAnyFieldValidating);
            Assert.DoesNotContain("email", ctx.ValidatingFields);
        });
    }
}
