using Xunit;
using Lumeo.Internal;

namespace Lumeo.Tests.Internal;

public class CxMergeTests
{
    // --- Trivial / edge inputs ---

    [Fact]
    public void Merge_NoArgs_ReturnsEmpty()
        => Assert.Equal(string.Empty, Cx.Merge());

    [Fact]
    public void Merge_NullAndWhitespace_Filtered()
        => Assert.Equal(string.Empty, Cx.Merge(null, "  ", "", null));

    [Fact]
    public void Merge_SingleClass_Unchanged()
        => Assert.Equal("p-4", Cx.Merge("p-4"));

    [Fact]
    public void Merge_NullsInterspersed_AreFiltered()
        => Assert.Equal("flex p-4", Cx.Merge("flex", null, "p-4", "  "));

    // --- Basic conflict, last wins ---

    [Fact]
    public void Merge_Padding_LastWins()
        => Assert.Equal("p-0", Cx.Merge("p-6", "p-0"));

    [Fact]
    public void Merge_KeepsNonConflicting_DropsConflicting()
        => Assert.Equal("px-4 py-2 h-6", Cx.Merge("h-9 px-4 py-2", "h-6"));

    [Fact]
    public void Merge_ConflictAcrossPartBoundary()
        => Assert.Equal("p-0", Cx.Merge("p-6", "p-0")); // base vs Class

    [Fact]
    public void Merge_ConflictWithinSinglePart()
        => Assert.Equal("p-2", Cx.Merge("p-6 p-2"));

    // --- Independent axes / padding superset rule ---

    [Fact]
    public void Merge_PxPy_DoNotConflict()
        => Assert.Equal("px-2 py-3", Cx.Merge("px-2", "py-3"));

    [Fact]
    public void Merge_PSuperset_OverridesPxPy()
        => Assert.Equal("p-0", Cx.Merge("px-4 py-2", "p-0"));

    [Fact]
    public void Merge_LaterPx_RefinesEarlierP_BothKept()
        => Assert.Equal("p-4 px-2", Cx.Merge("p-4", "px-2"));

    [Fact]
    public void Merge_POverridesEarlierP()
        => Assert.Equal("flex p-2", Cx.Merge("p-4 flex", "p-2"));

    [Fact]
    public void Merge_Px_OverridesPl()
        => Assert.Equal("px-2", Cx.Merge("pl-1", "px-2"));

    [Fact]
    public void Merge_Margin_Superset()
        => Assert.Equal("m-0", Cx.Merge("mx-4 my-2", "m-0"));

    // --- Logical inset padding (ps/pe) vs physical/superset gutters ---

    [Fact]
    public void Merge_Px_OverridesPs()
        // px writes inline-start+end, so it supersedes the logical ps.
        => Assert.Equal("px-0", Cx.Merge("ps-4", "px-0"));

    [Fact]
    public void Merge_P_OverridesPs()
        => Assert.Equal("p-0", Cx.Merge("ps-4", "p-0"));

    [Fact]
    public void Merge_PsPe_Independent()
        // inline-start and inline-end are distinct properties.
        => Assert.Equal("ps-4 pe-2", Cx.Merge("ps-4", "pe-2"));

    [Fact]
    public void Merge_PlAndPs_BothKept()
        // physical (pl) and logical (ps) are different CSS properties; both survive.
        => Assert.Equal("pl-2 ps-4", Cx.Merge("pl-2", "ps-4"));

    // --- Width / height / size ---

    [Fact]
    public void Merge_Width_LastWins()
        => Assert.Equal("w-8", Cx.Merge("w-4", "w-8"));

    [Fact]
    public void Merge_Size_OverridesWidthAndHeight()
        => Assert.Equal("size-10", Cx.Merge("w-4 h-4", "size-10"));

    [Fact]
    public void Merge_MinMax_Independent()
        => Assert.Equal("w-4 min-w-0 max-w-full", Cx.Merge("w-4", "min-w-0", "max-w-full"));

    // --- Text size vs color coexist ---

    [Fact]
    public void Merge_TextSizeAndColor_Coexist()
        => Assert.Equal("text-sm text-red-500", Cx.Merge("text-sm", "text-red-500"));

    [Fact]
    public void Merge_TextColor_LastWins()
        => Assert.Equal("text-blue-500", Cx.Merge("text-red-500", "text-blue-500"));

    [Fact]
    public void Merge_TextSize_LastWins()
        => Assert.Equal("text-lg", Cx.Merge("text-sm", "text-lg"));

    // text-wrap is its own group: it coexists with font size and with text color.
    [Fact]
    public void Merge_TextSizeAndWrap_Coexist()
        => Assert.Equal("text-sm text-wrap", Cx.Merge("text-sm", "text-wrap"));

    [Fact]
    public void Merge_TextColorAndWrap_Coexist()
        => Assert.Equal("text-red-500 text-wrap", Cx.Merge("text-red-500", "text-wrap"));

    // text-sm/6 (font-size with line-height suffix) is the font-size group, so it
    // conflicts with text-sm — not with text color.
    [Fact]
    public void Merge_TextSizeWithLineHeight_LastWins()
        => Assert.Equal("text-sm/6", Cx.Merge("text-sm", "text-sm/6"));

    // size vs color stay independent: only the size token is replaced.
    [Fact]
    public void Merge_TextSizeReplaced_ColorKept()
        => Assert.Equal("text-red-500 text-lg", Cx.Merge("text-sm text-red-500", "text-lg"));

    // --- Variant prefixes are part of the key ---

    [Fact]
    public void Merge_HoverVariant_SameVariantConflicts()
        => Assert.Equal("hover:bg-blue-500", Cx.Merge("hover:bg-red-500", "hover:bg-blue-500"));

    [Fact]
    public void Merge_BaseAndVariant_BothKept()
        => Assert.Equal("bg-red-500 hover:bg-blue-500", Cx.Merge("bg-red-500", "hover:bg-blue-500"));

    [Fact]
    public void Merge_HoverPadding_DistinctFromBasePadding()
        => Assert.Equal("p-2 hover:p-4", Cx.Merge("p-2", "hover:p-4"));

    [Fact]
    public void Merge_StackedVariants_AreKeys()
        => Assert.Equal("focus-visible:ring-4", Cx.Merge("focus-visible:ring-2", "focus-visible:ring-4"));

    [Fact]
    public void Merge_ResponsiveHoverStack()
        => Assert.Equal("sm:hover:p-4", Cx.Merge("sm:hover:p-2", "sm:hover:p-4"));

    [Fact]
    public void Merge_DifferentVariantOrder_NotMerged()
        => Assert.Equal("sm:hover:p-2 hover:sm:p-4", Cx.Merge("sm:hover:p-2", "hover:sm:p-4"));

    [Fact]
    public void Merge_DarkVariant_DistinctFromBase()
        => Assert.Equal("bg-white dark:bg-black", Cx.Merge("bg-white", "dark:bg-black"));

    [Fact]
    public void Merge_ArbitraryVariant_Selector()
        => Assert.Equal("[&>*]:p-4", Cx.Merge("[&>*]:p-2", "[&>*]:p-4"));

    [Fact]
    public void Merge_MaxWidthVariant()
        => Assert.Equal("max-md:p-4", Cx.Merge("max-md:p-2", "max-md:p-4"));

    // --- Important flag ---

    [Fact]
    public void Merge_LeadingImportant_SharesGroup_KeepsBang()
        => Assert.Equal("!p-0", Cx.Merge("p-6", "!p-0"));

    [Fact]
    public void Merge_ImportantBeatsLaterPlain_InSameGroup()
        // An !important rule wins its conflict group regardless of source order:
        // a later plain token cannot evict it (CSS specificity keeps !important on top).
        => Assert.Equal("!p-6", Cx.Merge("!p-6", "p-0"));

    [Fact]
    public void Merge_TrailingImportant_V4Form()
        => Assert.Equal("p-0!", Cx.Merge("p-6", "p-0!"));

    [Fact]
    public void Merge_ImportantWithVariant()
        => Assert.Equal("hover:!p-0", Cx.Merge("hover:p-6", "hover:!p-0"));

    // important wins over a later plain token (leading-! form, both orders)
    [Fact]
    public void Merge_ImportantFirst_BeatsLaterPlain()
        => Assert.Equal("!p-0", Cx.Merge("!p-0", "p-2"));

    [Fact]
    public void Merge_PlainFirst_LosesToLaterImportant()
        => Assert.Equal("!p-0", Cx.Merge("p-2", "!p-0"));

    // important vs important in the same group: last wins (unchanged rule).
    [Fact]
    public void Merge_ImportantVsImportant_LastWins()
        => Assert.Equal("!p-0", Cx.Merge("!p-2", "!p-0"));

    // important wins over later plain even under a shared variant chain.
    [Fact]
    public void Merge_ImportantWithVariant_BeatsLaterPlain()
        => Assert.Equal("hover:!p-0", Cx.Merge("hover:!p-0", "hover:p-2"));

    // --- Arbitrary values ---

    [Fact]
    public void Merge_ArbitraryValue_ConflictsWithinGroup()
        => Assert.Equal("p-[3px]", Cx.Merge("p-2", "p-[3px]"));

    [Fact]
    public void Merge_ArbitraryHeight_LastWins()
        => Assert.Equal("h-[calc(100%-1rem)]", Cx.Merge("h-4", "h-[calc(100%-1rem)]"));

    [Fact]
    public void Merge_ArbitraryBgColor()
        => Assert.Equal("bg-[#fff]", Cx.Merge("bg-red-500", "bg-[#fff]"));

    [Fact]
    public void Merge_ArbitraryProperty_OwnKey_Preserved()
        => Assert.Equal("[mask-type:luminance] p-4", Cx.Merge("[mask-type:luminance]", "p-4"));

    [Fact]
    public void Merge_DistinctArbitraryProperties_BothKept()
        => Assert.Equal("[mask-type:luminance] [mask-type:alpha]",
            Cx.Merge("[mask-type:luminance]", "[mask-type:alpha]"));

    // --- Negative values ---

    [Fact]
    public void Merge_NegativeMargin_ConflictsWithPositive()
        => Assert.Equal("mt-4", Cx.Merge("-mt-2", "mt-4"));

    [Fact]
    public void Merge_NegativeOverridesPositive()
        => Assert.Equal("-mt-2", Cx.Merge("mt-4", "-mt-2"));

    // --- Unknown / non-tailwind classes never dropped ---

    [Fact]
    public void Merge_UnknownClasses_Preserved()
        => Assert.Equal("lumeo-press-scale group sr-only",
            Cx.Merge("lumeo-press-scale", "group", "sr-only"));

    [Fact]
    public void Merge_UnknownClasses_KeptAlongsideMerged()
        // tailwind-merge drops the earlier conflict (p-6) and keeps the last
        // occurrence (p-0) in its own position; unknown classes keep order.
        => Assert.Equal("group sr-only p-0", Cx.Merge("group p-6 sr-only", "p-0"));

    [Fact]
    public void Merge_DuplicateUnknown_NotDeduped()
        => Assert.Equal("group group", Cx.Merge("group", "group"));

    // --- Rounded ---

    [Fact]
    public void Merge_Rounded_LastWins()
        => Assert.Equal("rounded-lg", Cx.Merge("rounded-md", "rounded-lg"));

    [Fact]
    public void Merge_RoundedSuperset_OverridesCorner()
        => Assert.Equal("rounded-none", Cx.Merge("rounded-t-lg", "rounded-none"));

    [Fact]
    public void Merge_RoundedSide_RefinesBase_BothKept()
        => Assert.Equal("rounded-md rounded-t-lg", Cx.Merge("rounded-md", "rounded-t-lg"));

    // --- Border width vs color ---

    [Fact]
    public void Merge_BorderWidth_LastWins()
        => Assert.Equal("border-4", Cx.Merge("border-2", "border-4"));

    [Fact]
    public void Merge_BorderWidthAndColor_Coexist()
        => Assert.Equal("border-2 border-red-500", Cx.Merge("border-2", "border-red-500"));

    [Fact]
    public void Merge_BorderColor_LastWins()
        => Assert.Equal("border-blue-500", Cx.Merge("border-red-500", "border-blue-500"));

    [Fact]
    public void Merge_BareBorder_IsWidth()
        => Assert.Equal("border-0", Cx.Merge("border", "border-0"));

    // bare border-0 sets all sides' width, so it clears a per-side width token.
    [Fact]
    public void Merge_BorderSideWidth_ClearedByAllSides()
        => Assert.Equal("border-0", Cx.Merge("border-b", "border-0"));

    // border-0 invalidates only width: the per-side color and style survive.
    [Fact]
    public void Merge_AllSidesWidth_KeepsColorAndStyle()
        => Assert.Equal("border-border/40 border-dashed border-0",
            Cx.Merge("border border-border/40 border-dashed", "border-0"));

    // a later subordinate (border-l-4) refines the border-x superset; both kept
    // (tailwind-merge: a later subordinate does NOT clear an earlier superset).
    [Fact]
    public void Merge_BorderXSuperset_RefinedByLaterSide_BothKept()
        => Assert.Equal("border-x-2 border-l-4", Cx.Merge("border-x-2", "border-l-4"));

    // --- Background: color / image / size are distinct groups ---

    [Fact]
    public void Merge_BgColorAndSize_Coexist()
        => Assert.Equal("bg-red-500 bg-cover", Cx.Merge("bg-red-500", "bg-cover"));

    [Fact]
    public void Merge_BgColor_LastWins()
        => Assert.Equal("bg-blue-500", Cx.Merge("bg-red-500", "bg-blue-500"));

    [Fact]
    public void Merge_BgImageAndColor_Coexist()
        => Assert.Equal("bg-gradient-to-r bg-red-500", Cx.Merge("bg-gradient-to-r", "bg-red-500"));

    // --- Border style is distinct from width and color ---

    [Fact]
    public void Merge_BorderWidthColorStyle_AllCoexist()
        => Assert.Equal("border border-border/40 border-dashed",
            Cx.Merge("border border-border/40", "border-dashed"));

    // --- Grid col span / start / end are distinct groups ---

    [Fact]
    public void Merge_GridColSpanStartEnd_CoexistAndSpanLastWins()
        => Assert.Equal("col-start-1 col-end-3 col-span-4",
            Cx.Merge("col-span-2 col-start-1 col-end-3", "col-span-4"));

    // --- ring-inset coexists with ring width and color ---

    [Fact]
    public void Merge_RingInset_CoexistsWithWidthAndColor()
        => Assert.Equal("ring-ring ring-2 ring-inset",
            Cx.Merge("ring-ring ring-2", "ring-inset"));

    [Fact]
    public void Merge_RingColor_LastWins()
        => Assert.Equal("ring-blue-500", Cx.Merge("ring-red-500", "ring-blue-500"));

    // --- Gap ---

    [Fact]
    public void Merge_Gap_Superset()
        => Assert.Equal("gap-0", Cx.Merge("gap-x-2 gap-y-4", "gap-0"));

    [Fact]
    public void Merge_GapXY_Independent()
        => Assert.Equal("gap-x-2 gap-y-4", Cx.Merge("gap-x-2", "gap-y-4"));

    // --- Display / position / overflow ---

    [Fact]
    public void Merge_Display_LastWins()
        => Assert.Equal("grid", Cx.Merge("flex", "grid"));

    [Fact]
    public void Merge_HiddenOverridesBlock()
        => Assert.Equal("hidden", Cx.Merge("block", "hidden"));

    [Fact]
    public void Merge_Position_LastWins()
        => Assert.Equal("absolute", Cx.Merge("relative", "absolute"));

    [Fact]
    public void Merge_Overflow_Superset()
        => Assert.Equal("overflow-hidden", Cx.Merge("overflow-x-auto overflow-y-scroll", "overflow-hidden"));

    [Fact]
    public void Merge_OverflowXY_Independent()
        => Assert.Equal("overflow-x-auto overflow-y-scroll",
            Cx.Merge("overflow-x-auto", "overflow-y-scroll"));

    // --- z / opacity / shadow / ring / leading / tracking / font-weight ---

    [Fact]
    public void Merge_ZIndex_LastWins()
        => Assert.Equal("z-50", Cx.Merge("z-10", "z-50"));

    [Fact]
    public void Merge_Opacity_LastWins()
        => Assert.Equal("opacity-50", Cx.Merge("opacity-100", "opacity-50"));

    [Fact]
    public void Merge_Shadow_LastWins()
        => Assert.Equal("shadow-none", Cx.Merge("shadow-lg", "shadow-none"));

    // shadow size (shadow-sm/lg) stays its own group; last wins.
    [Fact]
    public void Merge_ShadowSize_LastWins()
        => Assert.Equal("shadow-lg", Cx.Merge("shadow-sm", "shadow-lg"));

    // a shadow color with /<opacity> suffix is a color, not a size — it coexists
    // with the box-shadow size group.
    [Fact]
    public void Merge_ShadowSizeAndColorWithOpacity_Coexist()
        => Assert.Equal("shadow-md shadow-primary/10", Cx.Merge("shadow-md", "shadow-primary/10"));

    // two shadow colors conflict; last wins.
    [Fact]
    public void Merge_ShadowColor_LastWins()
        => Assert.Equal("shadow-white/50", Cx.Merge("shadow-black/50", "shadow-white/50"));

    [Fact]
    public void Merge_RingWidth_LastWins()
        => Assert.Equal("ring-4", Cx.Merge("ring-2", "ring-4"));

    [Fact]
    public void Merge_RingWidthAndColor_Coexist()
        => Assert.Equal("ring-2 ring-red-500", Cx.Merge("ring-2", "ring-red-500"));

    [Fact]
    public void Merge_FontWeight_LastWins()
        => Assert.Equal("font-bold", Cx.Merge("font-normal", "font-bold"));

    [Fact]
    public void Merge_Leading_LastWins()
        => Assert.Equal("leading-tight", Cx.Merge("leading-none", "leading-tight"));

    [Fact]
    public void Merge_Tracking_LastWins()
        => Assert.Equal("tracking-wide", Cx.Merge("tracking-tight", "tracking-wide"));

    // --- items / justify / content ---

    [Fact]
    public void Merge_ItemsAndJustify_Independent()
        => Assert.Equal("items-center justify-between",
            Cx.Merge("items-center", "justify-between"));

    [Fact]
    public void Merge_Items_LastWins()
        => Assert.Equal("items-start", Cx.Merge("items-center", "items-start"));

    // --- inset ---

    [Fact]
    public void Merge_Inset_Superset()
        => Assert.Equal("inset-0", Cx.Merge("top-2 left-4", "inset-0"));

    [Fact]
    public void Merge_InsetX_OverridesLeftRight()
        => Assert.Equal("inset-x-0", Cx.Merge("left-2 right-4", "inset-x-0"));

    // inset-x writes inline-start+end, so it supersedes the logical end-* token.
    [Fact]
    public void Merge_InsetX_OverridesLogicalEnd()
        => Assert.Equal("inset-x-0", Cx.Merge("-end-12", "inset-x-0"));

    // inset clears every side including logical start/end.
    [Fact]
    public void Merge_Inset_OverridesLogicalStartEnd()
        => Assert.Equal("inset-0", Cx.Merge("start-2 end-2", "inset-0"));

    // physical left and logical start are different CSS properties; both survive.
    [Fact]
    public void Merge_LeftAndStart_BothKept()
        => Assert.Equal("left-2 start-4", Cx.Merge("left-2", "start-4"));

    // --- idempotence / no-conflict ordering ---

    [Fact]
    public void Merge_NoConflicts_PreservesOrder()
        => Assert.Equal("flex items-center gap-2 rounded-md",
            Cx.Merge("flex items-center gap-2 rounded-md"));

    [Fact]
    public void Merge_Idempotent()
    {
        var once = Cx.Merge("p-6 px-4 py-2", "h-6", "p-0");
        var twice = Cx.Merge(once);
        Assert.Equal(once, twice);
    }

    // --- realistic Lumeo-style usage ---

    [Fact]
    public void Merge_RealisticButtonOverride()
        => Assert.Equal("inline-flex items-center rounded-md text-sm font-medium h-8 px-2",
            Cx.Merge("inline-flex items-center rounded-md text-sm font-medium h-9 px-4", "h-8 px-2"));
}
