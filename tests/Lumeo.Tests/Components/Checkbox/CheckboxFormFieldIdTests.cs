using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Checkbox;

/// <summary>
/// Codex P2 — FormField id ownership for Checkbox.
///
/// Inside a FormField the field's generated ControlId must own the checkbox
/// &lt;button id&gt; so the FormField &lt;Label For&gt; always resolves to it. The
/// EffectiveId branch used to prefer a consumer-supplied <c>Id</c> parameter
/// (<c>Id ?? ControlId</c>), so setting <c>Id</c> on a field checkbox put the
/// consumer id on the button while the label's <c>for</c> stayed on the generated
/// ControlId — a broken association. ControlId now wins inside a FormField; the
/// <c>Id</c> parameter still applies to a standalone checkbox.
/// </summary>
public class CheckboxFormFieldIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CheckboxFormFieldIdTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Consumer_Id_Inside_FormField_Does_Not_Win_Over_ControlId()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Accept the terms");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.Checkbox>(0);
                b.AddAttribute(1, "Id", "consumer-id");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var buttonId = cut.Find("button[role='checkbox']").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(buttonId));
        // The FormField's generated ControlId must own both the button id and the
        // label's `for`; the consumer-supplied Id must NOT have leaked onto the button.
        Assert.Equal(labelFor, buttonId);
        Assert.NotEqual("consumer-id", buttonId);
    }

    [Fact]
    public void Consumer_Id_Outside_FormField_Still_Applies()
    {
        // Guard: outside a FormField there is no generated ControlId to defer to, so
        // the consumer-supplied Id still owns the button (keeps the standalone
        // <label for> in sync via the component's own label rendering).
        var cut = _ctx.Render<L.Checkbox>(p => p.Add(c => c.Id, "standalone-id"));

        Assert.Equal("standalone-id", cut.Find("button[role='checkbox']").GetAttribute("id"));
    }
}
