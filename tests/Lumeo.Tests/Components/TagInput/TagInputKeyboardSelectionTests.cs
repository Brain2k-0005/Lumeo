using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using TagInputCmp = global::Lumeo.TagInput<string>;

namespace Lumeo.Tests.Components.TagInput;

/// <summary>
/// #190: the suggestions list had no keyboard selection. It now follows the
/// ARIA combobox contract — ArrowDown/Up move a highlight, Enter commits the
/// highlighted suggestion, aria-activedescendant tracks it, and the input
/// carries role="combobox".
/// </summary>
public class TagInputKeyboardSelectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TagInputKeyboardSelectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<string> Fruits() => ["apple", "apricot", "banana"];

    [Fact]
    public void Input_Has_Combobox_Role_And_Autocomplete()
    {
        var cut = _ctx.Render<TagInputCmp>();
        var input = cut.Find("input[type='text']");
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
    }

    [Fact]
    public void Typing_Opens_Matching_Suggestions()
    {
        var cut = _ctx.Render<TagInputCmp>(p => p.Add(t => t.Suggestions, Fruits()));
        cut.Find("input[type='text']").Input("ap");

        var options = cut.FindAll("[role='option']");
        Assert.Equal(2, options.Count); // apple, apricot
    }

    [Fact]
    public void ArrowDown_Highlights_First_Then_Second()
    {
        var cut = _ctx.Render<TagInputCmp>(p => p.Add(t => t.Suggestions, Fruits()));
        var input = cut.Find("input[type='text']");
        input.Input("ap");

        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("true", cut.FindAll("[role='option']")[0].GetAttribute("aria-selected"));

        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("true", cut.FindAll("[role='option']")[1].GetAttribute("aria-selected"));
    }

    [Fact]
    public void ArrowDown_Wraps_To_Top()
    {
        var cut = _ctx.Render<TagInputCmp>(p => p.Add(t => t.Suggestions, Fruits()));
        var input = cut.Find("input[type='text']");
        input.Input("ap");

        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // 0
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // 1
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // wrap -> 0

        Assert.Equal("true", cut.FindAll("[role='option']")[0].GetAttribute("aria-selected"));
    }

    [Fact]
    public void ArrowUp_From_None_Highlights_Last()
    {
        var cut = _ctx.Render<TagInputCmp>(p => p.Add(t => t.Suggestions, Fruits()));
        var input = cut.Find("input[type='text']");
        input.Input("ap");

        input.KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });

        var options = cut.FindAll("[role='option']");
        Assert.Equal("true", options[^1].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Enter_Commits_Highlighted_Suggestion()
    {
        List<string>? tags = null;
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.Suggestions, Fruits())
            .Add(t => t.TagsChanged, v => tags = v));
        var input = cut.Find("input[type='text']");

        input.Input("ap");
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // apple
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // apricot
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.NotNull(tags);
        Assert.Equal(new List<string> { "apricot" }, tags);
    }

    [Fact]
    public void Enter_Without_Highlight_Still_Adds_Free_Text()
    {
        List<string>? tags = null;
        var cut = _ctx.Render<TagInputCmp>(p => p
            .Add(t => t.Suggestions, Fruits())
            .Add(t => t.TagsChanged, v => tags = v));
        var input = cut.Find("input[type='text']");

        input.Input("cherry"); // no match -> no highlight
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new List<string> { "cherry" }, tags);
    }

    [Fact]
    public void ActiveDescendant_Points_To_Highlighted_Option()
    {
        var cut = _ctx.Render<TagInputCmp>(p => p.Add(t => t.Suggestions, Fruits()));
        var input = cut.Find("input[type='text']");
        input.Input("ap");
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        var activeId = cut.Find("input[type='text']").GetAttribute("aria-activedescendant");
        var firstOptionId = cut.FindAll("[role='option']")[0].GetAttribute("id");
        Assert.Equal(firstOptionId, activeId);
    }

    [Fact]
    public void Typing_Resets_The_Highlight()
    {
        var cut = _ctx.Render<TagInputCmp>(p => p.Add(t => t.Suggestions, Fruits()));
        var input = cut.Find("input[type='text']");
        input.Input("ap");
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        // A further keystroke changes the filter, so the highlight clears.
        input.Input("apr");

        Assert.Null(cut.Find("input[type='text']").GetAttribute("aria-activedescendant"));
    }
}
