using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Mention;

/// <summary>
/// Battle-test #70 (keyboard-a11y, low): the suggestion-dropdown keyboard handler
/// only knew ArrowDown/ArrowUp/Enter/Tab/Escape, so there was no way to jump the
/// highlight to the first or last option. The fix adds Home/End to the
/// HandleKeyDown switch (Home => _activeIndex=0, End => _activeIndex=last) and
/// registers Home/End in the prevent-default key set so the textarea caret does
/// not jump to line start/end while the open dropdown consumes them.
///
/// The test asserts the highlight (aria-selected on the options + the textarea's
/// aria-activedescendant) moves to the last/first option on End/Home — never real
/// DOM focus, since the combobox keeps focus on the textarea.
/// </summary>
public class MentionHomeEndKeyTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MentionHomeEndKeyTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        // The dropdown opens only when GetTextareaCaretPosition returns non-null,
        // so stub the versioned components.js module to return a caret record.
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        module.Mode = JSRuntimeMode.Loose;
        // Caret at selectionStart=1 (after typing the bare trigger "@") so no
        // _searchTerm is set and the full Options list renders.
        module.Setup<ComponentInteropService.TextareaCaretInfo>("getTextareaCaretPosition", _ => true)
            .SetResult(new ComponentInteropService.TextareaCaretInfo(0, 0, 1, OffsetTop: 4, OffsetLeft: 6, LineHeight: 18));
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Three options so Home/End differ from a single Arrow step.
    private static List<L.Mention.MentionOption> People() =>
    [
        new("Alice", "alice"),
        new("Bob", "bob"),
        new("Carol", "carol"),
    ];

    private IRenderedComponent<L.Mention> RenderOpen()
    {
        var cut = _ctx.Render<L.Mention>(p => p.Add(c => c.Options, People()));
        // Typing the bare trigger opens the dropdown with _activeIndex = 0.
        cut.Find("textarea").Input("@");
        Assert.Equal(3, cut.FindAll("[role='option']").Count);
        return cut;
    }

    [Fact]
    public void End_Key_Highlights_Last_Option()
    {
        var cut = RenderOpen();

        // Initially the first option is active.
        var optsBefore = cut.FindAll("[role='option']");
        Assert.Equal("true", optsBefore[0].GetAttribute("aria-selected"));

        // Press End — without the fix this key is ignored and the first option
        // stays highlighted; with the fix the highlight jumps to the last option.
        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "End" });

        var opts = cut.FindAll("[role='option']");
        Assert.Equal("false", opts[0].GetAttribute("aria-selected"));
        Assert.Equal("false", opts[1].GetAttribute("aria-selected"));
        Assert.Equal("true", opts[2].GetAttribute("aria-selected"));

        // The textarea's aria-activedescendant tracks the last option (combobox).
        Assert.Equal(opts[2].GetAttribute("id"), cut.Find("textarea").GetAttribute("aria-activedescendant"));
    }

    [Fact]
    public void Home_Key_Highlights_First_Option_After_Moving_Away()
    {
        var cut = RenderOpen();

        // Move the highlight to the last option, then Home must bring it back to
        // the first. Before the fix Home was a no-op (handler ignored it).
        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("true", cut.FindAll("[role='option']")[2].GetAttribute("aria-selected"));

        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Home" });

        var opts = cut.FindAll("[role='option']");
        Assert.Equal("true", opts[0].GetAttribute("aria-selected"));
        Assert.Equal("false", opts[1].GetAttribute("aria-selected"));
        Assert.Equal("false", opts[2].GetAttribute("aria-selected"));
        Assert.Equal(opts[0].GetAttribute("id"), cut.Find("textarea").GetAttribute("aria-activedescendant"));
    }
}
