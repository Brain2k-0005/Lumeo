using System.Globalization;
using System.Runtime.CompilerServices;

namespace Lumeo;

/// <summary>
/// Stable row-key resolver for <see cref="DataGridBody{TItem}"/>.
/// <para>
/// Blazor's <c>@key</c> dedupes siblings via <see cref="object.Equals(object?)"/>.
/// When <c>TItem</c> is a <see langword="record"/> (value-equality) and two
/// instances happen to be equal — e.g. two freshly-added "blank" rows from a
/// playground or a default-shaped row template — the framework throws
/// <c>InvalidOperationException: More than one sibling … has the same key
/// value</c> and unmounts the tree.
/// </para>
/// <para>
/// For reference types we return <see cref="RuntimeHelpers.GetHashCode(object)"/>,
/// which is the runtime's identity hash — stable per instance regardless of
/// any <c>Equals</c> override. For value types we return the boxed item itself
/// (no instance identity exists), which preserves the original behaviour.
/// </para>
/// </summary>
internal static class DataGridRowKeys
{
    public static object KeyFor<T>(T item)
    {
        if (item is null) return Unset;
        if (typeof(T).IsValueType) return item;
        return RuntimeHelpers.GetHashCode(item);
    }

    private static readonly object Unset = new();

    // --- DOM commit key (pointer-based row-reorder wire protocol) ---
    //
    // The row-reorder wire protocol (RegisterRowReorder / ReorderRowByKeyAsync,
    // see DataGrid.razor) needs a STRING identity to stamp into the DOM
    // (data-row-key) and read back at drag end. Naively stringifying KeyFor(item)
    // — i.e. calling ToString() on it — works for reference types (KeyFor already
    // returns an int identity hash there), but breaks for value types: KeyFor
    // returns the boxed item itself for those, and a struct's default
    // (unoverridden) ToString() returns only its declaring type's full name —
    // IDENTICAL for every instance of that type regardless of field values. Two
    // distinct rows then stamp the same data-row-key, and the identity-keyed
    // commit either becomes a same-key no-op or resolves to the wrong row.
    //
    // Value types have no instance identity to hash in the first place (that's
    // exactly why KeyFor falls back to boxed-value equality for @key), so there
    // is no truly stable per-instance key available. The contract here is
    // POSITIONAL for value types instead: DomKeyFor is only ever rendered when
    // DataGridContext.RowReorderPointerActive is true (see DataGridRow.RowKey),
    // and that flag guarantees a flat, fully-mounted DOM where DataGridRow's own
    // RowIndex parameter is 1:1 with the row's live index in _displayedItems
    // (see RowReorderPointerActive's remarks in DataGrid.razor) — so "this slot,
    // right now" is itself a well-defined, DOM-unique identity for the duration
    // of a render pass, even though it says nothing about which VALUE occupies
    // it. Reference types keep the true per-instance identity hash, unaffected
    // by index drift from a concurrent mutation during the drag's settle window.

    /// <summary>
    /// DOM-unique string key for the <c>data-row-key</c> attribute driving the
    /// pointer-based row-reorder wire protocol. See the remarks above this
    /// method's containing region for the reference-type vs. value-type
    /// identity contract; pair with <see cref="ResolveDomKeyIndex{T}"/> to
    /// resolve a previously-issued key back to a row.
    /// </summary>
    public static string DomKeyFor<T>(T item, int rowIndex)
    {
        if (item is null) return "";
        return typeof(T).IsValueType
            ? "v:" + rowIndex.ToString(CultureInfo.InvariantCulture)
            : RuntimeHelpers.GetHashCode(item).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Resolves a <see cref="DomKeyFor{T}"/>-issued key back to an index within
    /// <paramref name="items"/>, or -1 if it no longer resolves. Value-type keys
    /// ("v:N") resolve positionally (bounds-checked against the CURRENT list —
    /// see the positional contract above); reference-type keys resolve by
    /// matching the live per-instance identity hash of each item, which stays
    /// correct even if the list was mutated/reordered since the key was issued.
    /// </summary>
    public static int ResolveDomKeyIndex<T>(IReadOnlyList<T> items, string key)
    {
        if (string.IsNullOrEmpty(key)) return -1;
        if (typeof(T).IsValueType)
        {
            if (key.Length > 2 && key[0] == 'v' && key[1] == ':'
                && int.TryParse(key.AsSpan(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)
                && idx >= 0 && idx < items.Count)
                return idx;
            return -1;
        }
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is { } item && RuntimeHelpers.GetHashCode(item).ToString(CultureInfo.InvariantCulture) == key)
                return i;
        }
        return -1;
    }
}
