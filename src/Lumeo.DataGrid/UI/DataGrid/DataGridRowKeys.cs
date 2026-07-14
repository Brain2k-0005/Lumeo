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
/// For reference types we used to return <see cref="RuntimeHelpers.GetHashCode(object)"/>
/// as a cheap stand-in for instance identity. That turned out to be a
/// PROBABILISTIC production crash of its own: CoreCLR's identity hash is
/// derived from a 26-bit field of the object header (the low bits of the
/// sync-block-index "hash code" slot get reused once assigned), so it is NOT
/// unique — it is a 26-bit space shared by every live object in the process.
/// By the birthday-paradox bound, a grid rendering ~1,200 distinct
/// reference-type rows without a user-supplied <c>RowKey</c> already has a
/// ~1.9% chance PER RENDER that two sibling rows collide on the same
/// identity hash (~17% at 5k rows, ~53% at 10k rows) — measured empirically
/// (2,000-trial probe; observed hash values top out at 2^26−1 = 67,108,862).
/// A collision throws the same "More than one sibling … has the same key
/// value" crash the record-equality case above throws deliberately, except
/// this one is silent, data-dependent, and reproduces only some of the time —
/// exactly the shape that flaked <c>DataGridGroupCollapsePersistenceTests
/// .Collapse_Survives_LotsOfData_Refresh</c> (1,200 rows) in CI.
/// </para>
/// <para>
/// The fix: cache one dedicated, never-reused key OBJECT per row instance in
/// a <see cref="ConditionalWeakTable{TKey,TValue}"/>. Blazor compares
/// reference-type <c>@key</c> values with <see cref="object.Equals(object?)"/>,
/// which for a plain <see cref="object"/> is reference equality — so hand out
/// a fresh <c>new object()</c> the first time an instance is seen and it is
/// collision-free by construction, for the lifetime of the process, no matter
/// how many rows render. The table is weak-keyed, so it does not keep row
/// instances (or their key objects) alive past the row's own lifetime — no
/// leak. For value types we still return the boxed item itself (no instance
/// identity exists to key off), which preserves the original behaviour.
/// </para>
/// </summary>
internal static class DataGridRowKeys
{
    // One unique key object per row instance, forever — reference equality
    // makes collisions structurally impossible (unlike a 26-bit identity
    // hash). ConditionalWeakTable.GetValue is thread-safe and atomically
    // creates-if-absent, so concurrent renders of the same item still agree
    // on a single key object.
    private static readonly ConditionalWeakTable<object, object> IdentityKeys = new();

    private static readonly ConditionalWeakTable<object, object>.CreateValueCallback NewIdentityKey =
        static _ => new object();

    public static object KeyFor<T>(T item)
    {
        if (item is null) return Unset;
        if (typeof(T).IsValueType) return item;
        return IdentityKeys.GetValue(item, NewIdentityKey);
    }

    private static readonly object Unset = new();

    // --- DOM commit key (pointer-based row-reorder wire protocol) ---
    //
    // The row-reorder wire protocol (RegisterRowReorder / ReorderRowByKeyAsync,
    // see DataGrid.razor) needs a STRING identity to stamp into the DOM
    // (data-row-key) and read back at drag end. Naively stringifying KeyFor(item)
    // — i.e. calling ToString() on it — doesn't work: KeyFor returns an opaque
    // `object` for reference types (a cached identity-key object, see above) and
    // the boxed item itself for value types, and a struct's default
    // (unoverridden) ToString() returns only its declaring type's full name —
    // IDENTICAL for every instance of that type regardless of field values. Two
    // distinct rows would then stamp the same data-row-key, and the identity-keyed
    // commit either becomes a same-key no-op or resolves to the wrong row. So
    // this region mints its own STRING identities instead, one cache per kind.
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
    // just its slot — across that one mutation. Reference types still use a
    // true per-instance identity string (now a cached counter value, not a
    // 26-bit hash — see DomIdentityKeys below), unaffected by any of this.

    // DOM keys need a STRING, not the `object` KeyFor hands out for @key — so
    // reference types get their own ConditionalWeakTable, mapping each row
    // instance to a string minted once from a monotonically increasing,
    // Interlocked counter ("r:1", "r:2", …). A counter can never repeat within
    // a process, so — like the object identity above — this is collision-free
    // by construction, unlike the 26-bit RuntimeHelpers.GetHashCode string it
    // replaces.
    private static readonly ConditionalWeakTable<object, string> DomIdentityKeys = new();
    private static long _domKeyCounter;

    private static readonly ConditionalWeakTable<object, string>.CreateValueCallback NewDomIdentityKey =
        static _ => "r:" + Interlocked.Increment(ref _domKeyCounter).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// DOM-unique string key for the <c>data-row-key</c> attribute driving the
    /// pointer-based row-reorder wire protocol. See the remarks above this
    /// method's containing region for the reference-type vs. value-type
    /// identity contract; pair with <see cref="ResolveDomKeyIndex{T}"/> to
    /// resolve a previously-issued key back to a row. <paramref name="rowToken"/>
    /// is the value type's stable per-slot token (see <c>DataGrid.RowTokenAt</c>)
    /// — ignored for reference types, which key off their own cached identity
    /// string (see <see cref="DomIdentityKeys"/>).
    /// </summary>
    public static string DomKeyFor<T>(T item, long rowToken)
    {
        if (item is null) return "";
        return typeof(T).IsValueType
            ? "t:" + rowToken.ToString(CultureInfo.InvariantCulture)
            : DomIdentityKeys.GetValue(item, NewDomIdentityKey);
    }

    /// <summary>
    /// Resolves a <see cref="DomKeyFor{T}"/>-issued key back to an index within
    /// <paramref name="items"/>, or -1 if it no longer resolves. Value-type keys
    /// ("t:N") resolve by matching the live per-slot token in
    /// <paramref name="tokens"/> (positionally paired with <paramref name="items"/>
    /// by <c>DataGrid.RowTokenAt</c>/<c>MoveRow</c>) — NOT by parsing N as a direct
    /// index, which broke the moment the token's slot moved (round-7 #4).
    /// Reference-type keys resolve by matching the live cached identity string
    /// (<see cref="DomIdentityKeys"/>) of each item — a lookup, not a recompute,
    /// so it agrees with whatever string <see cref="DomKeyFor{T}"/> actually
    /// issued for that instance — which stays correct even if the list was
    /// mutated/reordered since the key was issued.
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
            if (items[i] is { } item && DomIdentityKeys.TryGetValue(item, out var itemKey) && itemKey == key)
                return i;
        }
        return -1;
    }
}
