using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Command;

/// <summary>
/// #214 — the Command palette now (1) scrolls the active item into view as the
/// keyboard moves the highlight, and (2) filters with a forgiving subsequence
/// match (a contiguous substring still wins) instead of substring-only.
/// </summary>
public class CommandScrollAndFilterTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public CommandScrollAndFilterTests()
    {
        _ctx.AddLumeoServices();
        // Override the real interop with the tracking double so we can assert
        // the scroll-into-view call without a real DOM scroll container.
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed record ItemSpec(string Label, string? FilterValue = null);

    private IRenderedComponent<IComponent> RenderPalette(params ItemSpec[] items)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.CommandList>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(list =>
                {
                    var seq = 0;
                    foreach (var item in items)
                    {
                        list.OpenComponent<L.CommandItem>(seq++);
                        if (item.FilterValue is not null)
                            list.AddAttribute(seq++, "FilterValue", item.FilterValue);
                        var label = item.Label;
                        list.AddAttribute(seq++, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
                        list.CloseComponent();
                    }
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<IComponent> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    // --- Scroll-into-view (#214) ---

    [Fact]
    public void ArrowDown_Scrolls_The_Highlighted_Item_Into_View()
    {
        var cut = RenderPalette(new ItemSpec("Save"), new ItemSpec("Open"), new ItemSpec("Quit"));

        var before = _interop.ScrollIntoViewCalls.Count;
        cut.Find("input").KeyDown("ArrowDown");

        // A scroll-into-view fired for the now-highlighted "Save".
        Assert.True(_interop.ScrollIntoViewCalls.Count > before);
        var save = FindOption(cut, "Save")!;
        Assert.Contains(_interop.ScrollIntoViewCalls, c => c.ElementId == save.GetAttribute("id"));
    }

    [Fact]
    public void Navigation_Targets_The_Current_Highlight_Each_Move()
    {
        var cut = RenderPalette(new ItemSpec("Save"), new ItemSpec("Open"), new ItemSpec("Quit"));
        var input = cut.Find("input");

        input.KeyDown("ArrowDown"); // Save
        input.KeyDown("ArrowDown"); // Open
        var open = FindOption(cut, "Open")!;

        // The most recent scroll target is the latest highlight.
        Assert.Equal(open.GetAttribute("id"), _interop.ScrollIntoViewCalls[^1].ElementId);
    }

    // --- Smarter (subsequence) filter (#214) ---

    [Fact]
    public void Subsequence_Match_Finds_Items_Substring_Would_Miss()
    {
        var cut = RenderPalette(
            new ItemSpec("Git Push", FilterValue: "Git Push"),
            new ItemSpec("Settings", FilterValue: "Settings"));

        // "gp" is not a substring of "Git Push" but is a subsequence.
        cut.Find("input").Input("gp");

        Assert.NotNull(FindOption(cut, "Git Push"));
        Assert.Null(FindOption(cut, "Settings"));
    }

    [Fact]
    public void Substring_Match_Still_Works()
    {
        var cut = RenderPalette(
            new ItemSpec("Save", FilterValue: "Save"),
            new ItemSpec("Open", FilterValue: "Open"));

        cut.Find("input").Input("ave");

        Assert.NotNull(FindOption(cut, "Save"));
        Assert.Null(FindOption(cut, "Open"));
    }

    [Fact]
    public void Multi_Word_Query_Ignores_Its_Spaces()
    {
        var cut = RenderPalette(
            new ItemSpec("Git Push", FilterValue: "Git: Push to remote"),
            new ItemSpec("Delete", FilterValue: "Delete file"));

        cut.Find("input").Input("git push");

        Assert.NotNull(FindOption(cut, "Git Push"));
        Assert.Null(FindOption(cut, "Delete"));
    }

    [Fact]
    public void Non_Matching_Query_Hides_Everything()
    {
        var cut = RenderPalette(
            new ItemSpec("Save", FilterValue: "Save"),
            new ItemSpec("Open", FilterValue: "Open"));

        cut.Find("input").Input("zzzz");

        Assert.Empty(cut.FindAll("button[role='option']"));
    }
}
