using System.Linq;
using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Input;

/// <summary>
/// Regression tests for triage #40 (edge-data): when <c>ShowCount</c> is enabled
/// the native <c>maxlength</c> hard-cap must be DROPPED so the field behaves as a
/// soft limit (matching Textarea / AntD / shadcn). With a hard <c>maxlength</c> the
/// browser silently blocks the extra characters, so the destructive over-limit
/// counter state (<c>IsOverLimit</c> -> <c>text-destructive</c>) is unreachable by
/// real typing. Without a counter the hard cap remains a plain input constraint.
///
/// Mirrors <see cref="InputStatePreservationTests"/> in structure. The edge input
/// reproduced here is "<c>ShowCount=true</c> + a small <c>MaxLength</c>": pre-fix
/// the input carries <c>maxlength</c> in the DOM (capping typing); post-fix the
/// attribute is absent so typing past the limit — and the warning state — is
/// reachable.
/// </summary>
public class InputSoftMaxLengthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InputSoftMaxLengthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ShowCount_With_MaxLength_Drops_Native_Maxlength_So_Soft_Limit_Is_Reachable()
    {
        // Edge input: a counter is shown with a small MaxLength. Pre-fix the
        // <input> rendered maxlength="3", so the browser would hard-block any 4th
        // character and the over-limit (destructive) counter could never be hit by
        // real typing. With the fix EffectiveMaxLength is null when ShowCount, so
        // the attribute is omitted entirely.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 3)
            .Add(t => t.Value, "ab"));

        var input = cut.Find("input");
        // Blazor omits an attribute bound to a null value, so the maxlength
        // attribute must be absent (not present-with-some-number) under ShowCount.
        Assert.False(input.HasAttribute("maxlength"));
    }

    [Fact]
    public void Without_ShowCount_MaxLength_Stays_A_Hard_Cap()
    {
        // The flip side / normal-path preservation: without a counter the native
        // maxlength must still be applied as a plain input constraint.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.MaxLength, 3));

        var input = cut.Find("input");
        Assert.True(input.HasAttribute("maxlength"));
        Assert.Equal("3", input.GetAttribute("maxlength"));
    }

    [Fact]
    public void Typing_Past_Soft_Limit_Reaches_Destructive_OverLimit_State()
    {
        // The behaviour the hard cap previously made unreachable: the user TYPES
        // past the limit (HandleInput on the real <input>, not a programmatic
        // Value param), and the counter must turn destructive. With maxlength
        // dropped this path is now reachable.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 3)
            .Add(t => t.Value, ""));

        cut.Find("input").Input("hello"); // 5 > 3

        var counterDivs = cut.FindAll("div.text-end").ToList();
        Assert.Single(counterDivs);
        Assert.Contains("text-destructive", counterDivs[0].GetAttribute("class") ?? "");
    }

    [Fact]
    public void Typing_Past_Soft_Limit_Marks_Input_Aria_Invalid()
    {
        // Secondary fix: the over-limit state is announced via aria-invalid (as
        // Textarea does), so assistive tech learns the field is now invalid.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 3)
            .Add(t => t.Value, ""));

        Assert.Equal("false", cut.Find("input").GetAttribute("aria-invalid"));

        cut.Find("input").Input("hello"); // 5 > 3 -> over limit

        Assert.Equal("true", cut.Find("input").GetAttribute("aria-invalid"));
    }
}
