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
    // is no true per-instance key available — round-6 substituted the row's
    // POSITIONAL index instead ("v:" + rowIndex), which held only until the very
    // reorder it exists to support: captureRowRects snapshots key -> rect BEFORE
    // MoveRow permutes _displayedItems, but the commit re-renders with keys
    // reassigned by the NEW positions, not carried with the moved item — so
    // animateRowReorder pairs each old rect with whatever item now happens to
    // sit at that same index, not with the item that was actually there,
    // misdirecting (or losing) the FLIP animation for the dragged row AND every
    // displaced sibling (round-7 #4).
    //
    // The fix: a stable per-SLOT token, minted once when DataGrid rebuilds
    // _displayedItems from scratch (DataGrid.EnsureRowTokens — a genuine
    // items-set event with no continuity to preserve, exactly like a fresh list
    // for Blazor's own @key) and then PERMUTED through the exact same
    // RemoveAt+Insert operation MoveRow performs on _displayedItems itself
    // (DataGrid.MoveRow), so a token stays glued to the same row content — not
    // just its slot — across that one mutation. Reference types still use the
    // true per-instance identity hash, unaffected by any of this.

    /// <summary>
    /// DOM-unique string key for the <c>data-row-key</c> attribute driving the
    /// pointer-based row-reorder wire protocol. See the remarks above this
    /// method's containing region for the reference-type vs. value-type
    /// identity contract; pair with <see cref="ResolveDomKeyIndex{T}"/> to
    /// resolve a previously-issued key back to a row. <paramref name="rowToken"/>
    /// is the value type's stable per-slot token (see <c>DataGrid.RowTokenAt</c>)
    /// — ignored for reference types, which key off their own identity hash.
    /// </summary>
    public static string DomKeyFor<T>(T item, long rowToken)
    {
        if (item is null) return "";
        return typeof(T).IsValueType
            ? "t:" + rowToken.ToString(CultureInfo.InvariantCulture)
            : RuntimeHelpers.GetHashCode(item).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Resolves a <see cref="DomKeyFor{T}"/>-issued key back to an index within
    /// <paramref name="items"/>, or -1 if it no longer resolves. Value-type keys
    /// ("t:N") resolve by matching the live per-slot token in
    /// <paramref name="tokens"/> (positionally paired with <paramref name="items"/>
    /// by <c>DataGrid.RowTokenAt</c>/<c>MoveRow</c>) — NOT by parsing N as a direct
    /// index, which broke the moment the token's slot moved (round-7 #4).
    /// Reference-type keys resolve by matching the live per-instance identity hash
    /// of each item, which stays correct even if the list was mutated/reordered
    /// since the key was issued.
    /// </summary>
    public static int ResolveDomKeyIndex<T>(IReadOnlyList<T> items, IReadOnlyList<long> tokens, string key)
    {
        if (string.IsNullOrEmpty(key)) return -1;
        if (typeof(T).IsValueType)
        {
            if (key.Length > 2 && key[0] == 't' && key[1] == ':'
                && long.TryParse(key.AsSpan(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var token))
            {
                var count = Math.Min(items.Count, tokens.Count);
                for (var i = 0; i < count; i++)
                    if (tokens[i] == token) return i;
            }
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
