using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Mention;

/// <summary>
/// #205 — the Mention textarea now carries the combobox ARIA contract
/// (role=combobox, aria-autocomplete=list, aria-expanded/-controls/
/// -activedescendant), and the suggestion dropdown is positioned absolutely
/// against the component's relative wrapper (so it tracks the textarea on
/// scroll) instead of being pinned at a stale fixed viewport point.
/// </summary>
public class MentionComboboxTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MentionComboboxTests()
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
        // Caret at selectionStart=2 (after typing "@a") with a small offset.
        module.Setup<ComponentInteropService.TextareaCaretInfo>("getTextareaCaretPosition", _ => true)
            .SetResult(new ComponentInteropService.TextareaCaretInfo(0, 0, 2, OffsetTop: 4, OffsetLeft: 6, LineHeight: 18));
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Mention.MentionOption> People() =>
    [
        new("Alice", "alice"),
        new("Bob", "bob"),
    ];

    private IRenderedComponent<L.Mention> Render(Action<ComponentParameterCollectionBuilder<L.Mention>>? extra = null)
        => _ctx.Render<L.Mention>(p =>
        {
            p.Add(c => c.Options, People());
            extra?.Invoke(p);
        });

    // --- Combobox ARIA on the textarea ---

    [Fact]
    public void Textarea_Has_Combobox_Role_And_Autocomplete()
    {
        var cut = Render();
        var ta = cut.Find("textarea");
        Assert.Equal("combobox", ta.GetAttribute("role"));
        Assert.Equal("list", ta.GetAttribute("aria-autocomplete"));
        Assert.Equal("listbox", ta.GetAttribute("aria-haspopup"));
    }

    [Fact]
    public void Collapsed_Combobox_Reports_Not_Expanded()
    {
        var cut = Render();
        Assert.Equal("false", cut.Find("textarea").GetAttribute("aria-expanded"));
        // No active descendant while closed.
        Assert.True(string.IsNullOrEmpty(cut.Find("textarea").GetAttribute("aria-activedescendant")));
    }

    // --- Opening the dropdown (typing a trigger) ---

    [Fact]
    public void Typing_Trigger_Opens_Listbox_And_Sets_Aria()
    {
        var cut = Render();
        cut.Find("textarea").Input("@a");

        // Listbox now exists with both options (Alice matches "a"; Bob matches "b" via value? — only "a" filters).
        var listbox = cut.Find("[role='listbox']");
        Assert.NotNull(listbox);
        Assert.Equal("true", cut.Find("textarea").GetAttribute("aria-expanded"));

        // aria-controls points at the listbox and activedescendant at the first option.
        Assert.Equal(listbox.GetAttribute("id"), cut.Find("textarea").GetAttribute("aria-controls"));
        var firstOption = cut.FindAll("[role='option']").First();
        Assert.Equal(firstOption.GetAttribute("id"), cut.Find("textarea").GetAttribute("aria-activedescendant"));
    }

    [Fact]
    public void Open_Dropdown_Is_Absolutely_Positioned()
    {
        var cut = Render();
        cut.Find("textarea").Input("@");

        var listbox = cut.Find("[role='listbox']");
        var style = listbox.GetAttribute("style") ?? "";
        // The dropdown follows the textarea via absolute positioning within the
        // relative wrapper (not fixed at a stale viewport point).
        Assert.Contains("position: absolute", style);
        Assert.DoesNotContain("position: fixed", style);
    }

    [Fact]
    public void Options_Are_Role_Option_With_Aria_Selected()
    {
        var cut = Render();
        cut.Find("textarea").Input("@");

        var options = cut.FindAll("[role='option']");
        Assert.Equal(2, options.Count);
        // First option is the active one.
        Assert.Equal("true", options[0].GetAttribute("aria-selected"));
        Assert.Equal("false", options[1].GetAttribute("aria-selected"));
    }
}
