using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.Checkbox;

/// <summary>
/// Triage #146 (low, state-on-data-change) — the effective DOM id was latched once in
/// <c>OnInitialized</c> into a private <c>_id</c> field and never recomputed. When the
/// <c>Id</c> parameter changed after the first render, the <c>&lt;button id&gt;</c> and the
/// <c>&lt;label for&gt;</c> kept pointing at the stale first-render value, so the
/// <c>for</c>/<c>id</c> association broke (clicking the label no longer targeted the button).
///
/// The fix derives the id per render (<c>EffectiveId</c>) while caching only the random
/// fallback, so a runtime <c>Id</c> change flows through to BOTH attributes and they stay in
/// sync. These tests re-render via <c>cut.Render(p =&gt; p.Add(...))</c> and assert on the
/// rendered <c>id</c>/<c>for</c> attributes — the directly observable mechanism.
/// </summary>
public class CheckboxIdRecomputeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CheckboxIdRecomputeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Changing_Id_After_First_Render_Updates_Button_Id()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Id, "first-id"));

        Assert.Equal("first-id", cut.Find("button").GetAttribute("id"));

        // Consumer swaps the Id at runtime (e.g. the field was re-keyed by a parent).
        cut.Render(p => p
            .Add(c => c.Id, "second-id"));

        // Without the fix the id is latched to "first-id"; with the fix it recomputes.
        Assert.Equal("second-id", cut.Find("button").GetAttribute("id"));
    }

    [Fact]
    public void Changing_Id_After_First_Render_Keeps_Label_For_In_Sync()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Label, "Accept terms")
            .Add(c => c.Id, "first-id"));

        Assert.Equal("first-id", cut.Find("label").GetAttribute("for"));
        Assert.Equal("first-id", cut.Find("button").GetAttribute("id"));

        cut.Render(p => p
            .Add(c => c.Label, "Accept terms")
            .Add(c => c.Id, "second-id"));

        // The label's `for` must follow the button's `id` so clicking the label still
        // resolves to the checkbox button.
        var buttonId = cut.Find("button").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");
        Assert.Equal("second-id", buttonId);
        Assert.Equal("second-id", labelFor);
        Assert.Equal(buttonId, labelFor);
    }
}
