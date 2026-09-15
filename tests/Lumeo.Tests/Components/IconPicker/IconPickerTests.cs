using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.IconPicker;

public class IconPickerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public IconPickerTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Minimal, pack-agnostic test fixture: real IconSource values (no icon pack
    // dependency needed — IconSource.Stroke is a plain factory), so the picker
    // exercises exactly what a real Lucide/Tabler/… list would give it.
    private static readonly IReadOnlyList<L.IconPickerItem> TestIcons = new List<L.IconPickerItem>
    {
        new("Home", L.IconSource.Stroke("<path d=\"M3 3\" />"), new[] { "house" }),
        new("Star", L.IconSource.Stroke("<path d=\"M3 3\" />")),
        new("Heart", L.IconSource.Stroke("<path d=\"M3 3\" />")),
        new("Settings", L.IconSource.Stroke("<path d=\"M3 3\" />")),
    };

    // --- Rendering ---

    [Fact]
    public void Renders_Trigger_Button()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p.Add(c => c.Icons, TestIcons));
        Assert.NotNull(cut.Find("button[type='button']"));
    }

    [Fact]
    public void Shows_Default_Placeholder_When_No_Value()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p.Add(c => c.Icons, TestIcons));
        Assert.Contains("Pick an icon", cut.Markup);
    }

    [Fact]
    public void Shows_Custom_Placeholder()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Placeholder, "Choose a category icon"));
        Assert.Contains("Choose a category icon", cut.Markup);
    }

    [Fact]
    public void Grid_Not_Rendered_When_Closed()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p.Add(c => c.Icons, TestIcons));
        Assert.Empty(cut.FindAll("[role='option']"));
    }

    // --- Opening ---

    [Fact]
    public void Opening_Shows_All_Icons()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        var options = cut.FindAll("[role='option']");
        Assert.Equal(TestIcons.Count, options.Count);
        foreach (var icon in TestIcons)
        {
            Assert.Contains(icon.Name, cut.Markup);
        }
    }

    [Fact]
    public void Clicking_Trigger_Opens_Grid()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p.Add(c => c.Icons, TestIcons));
        Assert.Empty(cut.FindAll("[role='option']"));

        cut.Find("button[type='button']").Click();

        Assert.Equal(TestIcons.Count, cut.FindAll("[role='option']").Count);
    }

    // --- Search ---

    [Fact]
    public void Search_Narrows_Results_By_Name()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        cut.Find("input[type='search']").Input("star");

        var options = cut.FindAll("[role='option']");
        Assert.Single(options);
        Assert.Contains("Star", cut.Markup);
        Assert.DoesNotContain("Heart", cut.Markup);
    }

    [Fact]
    public void Search_Matches_Keywords_Not_Just_Name()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        // "house" is a Keyword on the "Home" item, not part of its Name.
        cut.Find("input[type='search']").Input("house");

        var options = cut.FindAll("[role='option']");
        Assert.Single(options);
        Assert.Contains("Home", cut.Markup);
    }

    [Fact]
    public void Search_With_No_Matches_Shows_Empty_State()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        cut.Find("input[type='search']").Input("zzz-no-match");

        Assert.Empty(cut.FindAll("[role='option']"));
        Assert.Contains("No icons found", cut.Markup);
    }

    [Fact]
    public void Searchable_False_Renders_No_Search_Box()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.Searchable, false));

        Assert.Empty(cut.FindAll("input[type='search']"));
        Assert.Equal(TestIcons.Count, cut.FindAll("[role='option']").Count);
    }

    // --- Selection ---

    [Fact]
    public void Clicking_An_Icon_Raises_ValueChanged_And_Closes()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.ValueChanged, cb));

        var starOption = cut.FindAll("[role='option']").Single(o => o.GetAttribute("aria-label") == "Star");
        starOption.Click();

        Assert.Equal("Star", selected);
        Assert.Empty(cut.FindAll("[role='option']"));
    }

    [Fact]
    public void Selected_Icon_Renders_On_Trigger()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Value, "Heart")
            .Add(c => c.ShowLabel, true));

        Assert.Contains("Heart", cut.Markup);
    }

    // --- Clearable ---

    [Fact]
    public void Clearable_Clear_Button_Resets_Value_To_Null()
    {
        string? lastValue = "unset";
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => lastValue = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Value, "Heart")
            .Add(c => c.Clearable, true)
            .Add(c => c.ValueChanged, cb));

        cut.Find("button[aria-label='Clear']").Click();

        Assert.Null(lastValue);
    }

    [Fact]
    public void Clear_Button_Not_Rendered_Without_Clearable()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Value, "Heart"));

        Assert.Empty(cut.FindAll("button[aria-label='Clear']"));
    }

    // --- Keyboard ---

    [Fact]
    public void ArrowRight_Then_Enter_Selects_First_Icon()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.ValueChanged, cb));

        var search = cut.Find("input[type='search']");
        search.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        search.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(TestIcons[0].Name, selected);
    }

    [Fact]
    public void Escape_Closes_The_Popover()
    {
        bool? openValue = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openValue = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.OpenChanged, cb));

        var dialog = cut.Find("[role='dialog']");
        dialog.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(openValue);
    }

    // --- Disabled ---

    [Fact]
    public void Disabled_Trigger_Blocks_Open()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Disabled, true));

        var button = cut.Find("button[type='button']");
        Assert.True(button.HasAttribute("disabled"));

        var ex = Record.Exception(() => button.Click());
        // A disabled native button either no-ops the click or bUnit throws on the
        // attempt — either way, the grid must never appear.
        Assert.Empty(cut.FindAll("[role='option']"));
    }

    // --- Row-index math (fix round 1) ---

    [Fact]
    public void Second_Row_Icon_Selects_The_Correct_Item()
    {
        // Columns=2 over 4 icons -> two rows: [Home, Star] / [Heart, Settings].
        // Regression guard for the RenderRow rewrite: startIndex is now RowIndex * Columns,
        // not FilteredIcons.IndexOf(row[0]) — assert row 2 still resolves to the right icons.
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.Columns, 2)
            .Add(c => c.ValueChanged, cb));

        var heartOption = cut.FindAll("[role='option']").Single(o => o.GetAttribute("aria-label") == "Heart");
        heartOption.Click();

        Assert.Equal("Heart", selected);
    }

    [Fact]
    public void Third_Row_Icon_Selects_The_Correct_Item()
    {
        // Columns=2 over 6 icons -> three rows. Row 3's startIndex (RowIndex(2) * Columns(2)
        // = 4) must resolve to the 5th/6th icons, not whatever IndexOf used to find by
        // structural equality. Same RenderRow the Virtualize branch calls (bUnit's Virtualize
        // renders zero rows in its zero-height test container — see
        // DataBound_Virtualize_Renders_Without_Crash in ComboboxDataBoundTests — so row math
        // is verified here, through the non-virtualized branch, which shares the exact same
        // RenderRow/Rows code).
        var sixIcons = new List<L.IconPickerItem>(TestIcons)
        {
            new("Bell", L.IconSource.Stroke("<path d=\"M3 3\" />")),
            new("Folder", L.IconSource.Stroke("<path d=\"M3 3\" />")),
        };
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, sixIcons)
            .Add(c => c.Open, true)
            .Add(c => c.Columns, 2)
            .Add(c => c.ValueChanged, cb));

        var folderOption = cut.FindAll("[role='option']").Single(o => o.GetAttribute("aria-label") == "Folder");
        folderOption.Click();

        Assert.Equal("Folder", selected);
    }

    [Fact]
    public void Icons_Past_The_Virtualize_Threshold_Render_Without_Crashing()
    {
        // bUnit gives Virtualize a zero-height container, so it renders zero visible rows
        // (same limitation ComboboxDataBoundTests.DataBound_Virtualize_Renders_Without_Crash
        // documents) — this only proves the Virtualize branch doesn't throw for a large list;
        // row-index correctness is covered by the non-virtualized tests above, which share
        // the same RenderRow/Rows code.
        var manyIcons = Enumerable.Range(0, 600)
            .Select(i => new L.IconPickerItem($"Icon{i:D4}", L.IconSource.Stroke("<path d=\"M3 3\" />")))
            .ToList();

        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, manyIcons)
            .Add(c => c.Open, true)
            .Add(c => c.Columns, 8));

        Assert.NotNull(cut.Markup);
    }

    // --- Trigger size parity with Input (fix round 1) ---

    [Theory]
    [InlineData(L.Size.Xxs)]
    [InlineData(L.Size.Xs)]
    [InlineData(L.Size.Sm)]
    [InlineData(L.Size.Md)]
    [InlineData(L.Size.Lg)]
    [InlineData(L.Size.Xl)]
    [InlineData(L.Size.Xxl)]
    public void Trigger_Height_Matches_Input_Ladder_At_Every_Size(L.Size size)
    {
        // "Sits flush next to any other sized control" means the trigger's height (and
        // padding/text-size, which visually rides along) must be byte-for-byte identical to
        // Input's at the same Size rung — not just close. Render both live and compare the
        // actual token sets rather than duplicating Input's literal strings a second time
        // here, so this test still catches drift if Input's ladder itself changes later.
        var inputCut = _ctx.Render<L.Input>(p => p.Add(c => c.Size, size));
        var inputClasses = inputCut.Find("input").GetAttribute("class") ?? "";

        var pickerCut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Size, size));
        var triggerClasses = pickerCut.Find("button[type='button']").GetAttribute("class") ?? "";

        Assert.Equal(ExtractToken(inputClasses, "h-"), ExtractToken(triggerClasses, "h-"));
        Assert.Equal(ExtractPxToken(inputClasses), ExtractPxToken(triggerClasses));
        Assert.Equal(ExtractBaseTextToken(inputClasses), ExtractBaseTextToken(triggerClasses));
    }

    private static string? ExtractToken(string classes, string prefix) =>
        classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(c => c.StartsWith(prefix, StringComparison.Ordinal));

    // "px-N" only — excludes "ps-"/"pe-" (start/end padding), which some Input branches use
    // for icon insets that don't apply to IconPicker's trigger.
    private static string? ExtractPxToken(string classes) =>
        classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(c => c.StartsWith("px-", StringComparison.Ordinal));

    // The unprefixed "text-*" token (e.g. "text-base"), not the "md:text-*" responsive
    // variant — both components emit the same pair, comparing one is enough to prove parity.
    private static string? ExtractBaseTextToken(string classes) =>
        classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(c => (c.StartsWith("text-", StringComparison.Ordinal) || c.StartsWith("leading-", StringComparison.Ordinal))
                                  && !c.Contains(':'));

    // --- Roving focus / Tab order (fix round 1) ---

    [Fact]
    public void No_Inert_Tab_Stop_Between_Search_And_Options()
    {
        // ScrollArea's viewport div used to sit between the search input and the grid with
        // an unconditional tabindex="0" — an inert Tab stop a keyboard user would land on
        // with nothing to do. Dropped in favor of Command's pattern (overflow directly on the
        // role=listbox div). With Searchable on, NOTHING in the popover should carry
        // tabindex="0": the search input is natively focusable (no tabindex needed), the grid
        // itself is tabindex="-1" (roving via aria-activedescendant, not a real Tab stop),
        // and every option button is tabindex="-1" too.
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        Assert.Empty(cut.FindAll("[tabindex='0']"));

        var searchInput = cut.Find("input[type='search']");
        Assert.False(searchInput.HasAttribute("tabindex"));

        var grid = cut.Find("[role='listbox']");
        Assert.Equal("-1", grid.GetAttribute("tabindex"));
    }

    [Fact]
    public void Grid_Is_The_Tab_Stop_When_Not_Searchable()
    {
        // Without a search box, the grid itself must be reachable by Tab — it's the only
        // entry point into the popover's content.
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true)
            .Add(c => c.Searchable, false));

        var grid = cut.Find("[role='listbox']");
        Assert.Equal("0", grid.GetAttribute("tabindex"));
    }

    [Fact]
    public void Popover_Content_Has_No_ScrollArea_Wrapper()
    {
        // Regression guard: ScrollArea renders data-slot="scroll-area" — assert it's gone.
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        Assert.Empty(cut.FindAll("[data-slot='scroll-area']"));
    }

    // --- Popover content scales with Size (owner report: search box, grid cell/glyph size
    // and row height were identical at every trigger Size — only the trigger itself scaled) ---

    [Fact]
    public void Search_Field_Uses_Lumeo_Input()
    {
        // "In every component where other components are used, they use sizing and are not
        // native" (owner's rule) — the search box must be the real Input component, not a
        // raw <input>: data-slot="input-control" is Input's own marker on its actual <input>.
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Open, true));

        var search = cut.Find("[data-slot='input-control']");
        Assert.Equal("search", search.GetAttribute("type"));
        // The Lumeo focus ring (focus-within on the wrapper, since the actual focus target
        // is the inner <input>), not the browser's default outline.
        Assert.Contains("focus-within:ring", search.ParentElement!.GetAttribute("class"));
    }

    public static IEnumerable<object[]> AllSizes => new List<object[]>
    {
        new object[] { L.Size.Xxs }, new object[] { L.Size.Xs }, new object[] { L.Size.Sm },
        new object[] { L.Size.Md }, new object[] { L.Size.Lg }, new object[] { L.Size.Xl }, new object[] { L.Size.Xxl },
    };

    public static IEnumerable<object[]> SizeLadder => new List<object[]>
    {
        new object[] { L.Size.Xxs, 24, 12, 2 },
        new object[] { L.Size.Xs,  28, 14, 3 },
        new object[] { L.Size.Sm,  32, 16, 4 },
        new object[] { L.Size.Md,  36, 18, 4 },
        new object[] { L.Size.Lg,  40, 20, 6 },
        new object[] { L.Size.Xl,  44, 22, 6 },
        new object[] { L.Size.Xxl, 48, 24, 8 },
    };

    [Theory]
    [MemberData(nameof(AllSizes))]
    public void Search_Input_Matches_Input_Ladder_At_Every_Size(L.Size size)
    {
        // Cross-check against a standalone Input at the same Size/Variant rather than
        // re-deriving Input's ladder a second time here, so this still catches drift if
        // Input's own ladder changes later (same technique as Trigger_Height_Matches_Input_
        // Ladder_At_Every_Size above).
        var inputCut = _ctx.Render<L.Input>(p => p
            .Add(c => c.Size, size)
            .Add(c => c.Variant, L.Input.InputVariant.Search));
        var expectedWrapperClasses = inputCut.Find("[data-slot='input-control']").ParentElement!.GetAttribute("class") ?? "";

        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Size, size)
            .Add(c => c.Open, true));
        var actualWrapperClasses = cut.Find("[data-slot='input-control']").ParentElement!.GetAttribute("class") ?? "";

        Assert.Equal(ExtractToken(expectedWrapperClasses, "h-"), ExtractToken(actualWrapperClasses, "h-"));
        Assert.Equal(ExtractBaseTextToken(expectedWrapperClasses), ExtractBaseTextToken(actualWrapperClasses));
    }

    [Theory]
    [MemberData(nameof(SizeLadder))]
    public void Grid_Scales_With_Size(L.Size size, int expectedCellPx, int expectedGlyphPx, int expectedGapPx)
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Size, size)
            .Add(c => c.Open, true));

        var metrics = cut.Instance.Metrics;
        Assert.Equal(expectedCellPx, metrics.Cell);
        Assert.Equal(expectedGlyphPx, metrics.Glyph);
        Assert.Equal(expectedGapPx, metrics.Gap);

        // Virtualize's ItemSize (see RowItemSize's use at the <Virtualize> call site) must
        // match the actual rendered row height at every rung, or the virtualized list jumps.
        Assert.Equal((float)(expectedCellPx + expectedGapPx), cut.Instance.RowItemSize);

        var option = cut.FindAll("[role='option']").First();
        var optionClasses = (option.GetAttribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in metrics.CellClass.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.Contains(token, optionClasses);
        }

        var glyph = option.QuerySelector("[data-slot='svg-glyph']");
        Assert.NotNull(glyph);
        Assert.Equal(metrics.GlyphClass, glyph!.GetAttribute("class"));
    }

    [Fact]
    public void Popover_Width_Follows_Columns_And_Cell_Size()
    {
        var cut = _ctx.Render<L.IconPicker>(p => p
            .Add(c => c.Icons, TestIcons)
            .Add(c => c.Size, L.Size.Xl)
            .Add(c => c.Columns, 5)
            .Add(c => c.Open, true));

        // Xl metrics: cell=44px, gap=6px. width = cols*cell + (cols-1)*gap + 16 (p-2 padding).
        var expectedWidth = 5 * 44 + 4 * 6 + 16;
        var content = cut.Find("[data-slot='popover-content']");
        var style = content.GetAttribute("style") ?? "";

        Assert.Contains($"width:{expectedWidth}px", style);
        // Clamped to the viewport so a large Columns/Size combo never overflows the screen.
        Assert.Contains("max-width:calc(100vw - 2rem)", style);
    }
}
