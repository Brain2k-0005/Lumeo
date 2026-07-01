using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Mention;

/// <summary>
/// Regression: SelectOption used a stale _triggerStartIndex to slice Value.
/// In controlled usage the parent can shorten Value (e.g. clear/reset the box)
/// while the suggestion dropdown is still open, leaving _triggerStartIndex (and
/// the caret's selectionStart) pointing past the new, shorter end. Selecting an
/// option then sliced Value with the out-of-range index and threw
/// ArgumentOutOfRangeException, killing the circuit. The fix clamps the index
/// and bails out cleanly when the recorded trigger no longer fits.
/// </summary>
public class MentionStaleTriggerIndexTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MentionStaleTriggerIndexTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        module.Mode = JSRuntimeMode.Loose;
        // Caret sits at selectionStart=14 — the end of the original long Value
        // "hello world @a" (the trigger '@' is at index 12, so _triggerStartIndex
        // becomes 12 when typing). This selectionStart/index both point past the
        // shortened Value used below.
        module.Setup<ComponentInteropService.TextareaCaretInfo>("getTextareaCaretPosition", _ => true)
            .SetResult(new ComponentInteropService.TextareaCaretInfo(0, 0, 14, OffsetTop: 4, OffsetLeft: 6, LineHeight: 18));
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Mention.MentionOption> People() =>
    [
        new("Alice", "alice"),
        new("Bob", "bob"),
    ];

    [Fact]
    public void SelectOption_With_Stale_TriggerIndex_After_Parent_Shortens_Value_Does_Not_Throw()
    {
        string? lastValue = null;
        var cut = _ctx.Render<L.Mention>(p =>
        {
            p.Add(c => c.Options, People());
            p.Add(c => c.Value, "hello world @a");
            p.Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, val => lastValue = val));
        });

        // Type the trigger context. HandleInput records _triggerStartIndex = 12
        // (the '@' position) because the caret stub reports selectionStart = 14,
        // so textBeforeCaret = "hello world @a" and the last '@' is at index 12.
        cut.Find("textarea").Input("hello world @a");

        // The dropdown is open with options.
        Assert.NotEmpty(cut.FindAll("[role='option']"));

        // The parent shortens Value out from under the open dropdown (controlled
        // usage): the new Value is far shorter than the recorded _triggerStartIndex.
        cut.Render(p => p.Add(c => c.Value, "hi"));

        // Selecting an option must NOT throw ArgumentOutOfRangeException from
        // slicing the now-shorter Value with the stale index. Before the fix this
        // threw; after the fix it slices safely (clamped) or closes cleanly.
        var option = cut.FindAll("[role='option']").First();
        var ex = Record.Exception(() => option.Click());
        Assert.Null(ex);

        // Either way the dropdown is dismissed after selection.
        Assert.Empty(cut.FindAll("[role='option']"));
    }
}
