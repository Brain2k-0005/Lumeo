using Microsoft.AspNetCore.Components;

namespace Lumeo;

/// <summary>The built-in value editors by key: <c>text</c>, <c>number</c>, <c>range</c>, <c>date</c>,
/// <c>date-range</c>, <c>select</c>, <c>multiselect</c>, <c>boolean</c>. Hand a dictionary with the
/// same keys (or new ones a field names in <see cref="FilterField.Editor"/>) to <c>Filters.Editors</c>
/// to replace or extend them.</summary>
public static class FilterEditors
{
    private static RenderFragment<FilterEditorContext> Of<T>() where T : IComponent => ctx => builder =>
    {
        builder.OpenComponent<T>(0);
        builder.AddAttribute(1, "Context", ctx);
        builder.CloseComponent();
    };

    public static IReadOnlyDictionary<string, RenderFragment<FilterEditorContext>> Default { get; } = new Dictionary<string, RenderFragment<FilterEditorContext>>
    {
        ["text"] = Of<FilterTextEditor>(),
        ["number"] = Of<FilterNumberEditor>(),
        ["range"] = Of<FilterRangeEditor>(),
        ["date"] = Of<FilterDateEditor>(),
        ["date-range"] = Of<FilterDateEditor>(),
        ["select"] = Of<FilterSelectEditor>(),
        ["multiselect"] = Of<FilterMultiSelectEditor>(),
        ["boolean"] = Of<FilterBooleanEditor>(),
    };

    /// <summary>The defaults with the given entries replaced or added.</summary>
    public static IReadOnlyDictionary<string, RenderFragment<FilterEditorContext>> With(IReadOnlyDictionary<string, RenderFragment<FilterEditorContext>>? overrides)
    {
        if (overrides is null || overrides.Count == 0) return Default;
        var merged = new Dictionary<string, RenderFragment<FilterEditorContext>>(Default);
        foreach (var (k, v) in overrides) merged[k] = v;
        return merged;
    }
}

internal static class FilterIds
{
    public static string New(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..8];
}

/// <summary>The size ladder of <see cref="Filters"/>: every chip segment, button and editor control
/// on the same rung, from the geometry tokens.</summary>
public static class FilterStyles
{
    public static string ControlHeight(Lumeo.Size size) => size == Lumeo.Size.Sm
        ? "h-[var(--lumeo-control-h-sm,calc(var(--spacing,0.25rem)*7))]"
        : "h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]";

    public static string IconWidth(Lumeo.Size size) => size == Lumeo.Size.Sm
        ? "w-[var(--lumeo-control-h-sm,calc(var(--spacing,0.25rem)*7))]"
        : "w-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]";

    public static string PaddingX(Lumeo.Size size) => size == Lumeo.Size.Sm ? "px-2" : "px-2.5";

    /// <summary>The row an inline editor's input and its fused apply/discard buttons share. It has
    /// no border of its own: the editor's popover is the frame, so the popover is exactly one
    /// control tall (the chip's height plus the popover's 1px border), not a boxed input inside a
    /// padded panel. The parts inside are flat, the buttons divided by a hairline.</summary>
    public static string FieldGroup => "flex w-full items-stretch overflow-hidden rounded-md bg-popover";
    /// <summary>The input inside <see cref="FieldGroup"/>: no border, radius, shadow or ring of its own.</summary>
    public static string FusedInput => "w-full min-w-0 rounded-none border-0 bg-transparent shadow-none focus-visible:ring-0 focus-within:ring-0";

    /// <summary>An editor's input on the chip geometry: the bar's control height, 14px text and
    /// the chips' padding, instead of the Input component's own size ladder (whose Sm rung is
    /// 32px with 12px text while the chips beside it are 28px with 14px). Fused, it also drops
    /// its own border, radius and ring for the group's.</summary>
    public static string InputClass(Lumeo.Size size, bool fused)
        => Cx.Join(fused ? FusedInput : "w-full", ControlHeight(size), "py-0 text-sm md:text-sm", PaddingX(size));

    public static global::Lumeo.Button.ButtonSize ButtonSizeOf(Lumeo.Size size) => size == Lumeo.Size.Sm ? global::Lumeo.Button.ButtonSize.Sm : global::Lumeo.Button.ButtonSize.Default;

    /// <summary>The menu width the chip menu and the group menu share.</summary>
    public const string MenuClass = "w-max min-w-32 max-w-[min(24rem,calc(100vw-2rem))]";
}
