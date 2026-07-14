using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Unit-level coverage for <see cref="Lumeo.DataGridRowKeys"/>'s DOM-key contract
/// (round-7 #4): value types resolve by a stable per-slot TOKEN, not by parsing the
/// key as a direct positional index — see that type's remarks for the full
/// captureRowRects/MoveRow timing this fixes. Complements the integration-level
/// coverage in <see cref="DataGridRowReorderTests"/>, which exercises the same
/// contract through the real <see cref="Lumeo.DataGrid{TItem}"/> component.
/// </summary>
public class DataGridRowKeysTests
{
    private struct Widget
    {
        public int Id;
        public Widget(int id) { Id = id; }
    }

    private class WidgetRef
    {
        public int Id;
    }

    [Fact]
    public void DomKeyFor_ValueType_Keys_Off_The_Token_Not_The_Items_Own_Fields()
    {
        // Two DIFFERENT struct values sharing the same token collapse to the same
        // key — the token is a pure slot identity, independent of field content.
        Assert.Equal(
            DataGridRowKeys.DomKeyFor(new Widget(1), 7L),
            DataGridRowKeys.DomKeyFor(new Widget(2), 7L));
        Assert.Equal("t:7", DataGridRowKeys.DomKeyFor(new Widget(1), 7L));

        // Different tokens for equal-content structs produce different keys.
        Assert.NotEqual(
            DataGridRowKeys.DomKeyFor(new Widget(1), 0L),
            DataGridRowKeys.DomKeyFor(new Widget(1), 1L));
    }

    [Fact]
    public void ResolveDomKeyIndex_ValueType_Follows_The_Token_Through_A_MoveRow_Style_Permutation()
    {
        // Mirrors exactly what DataGrid.MoveRow does: permute _displayedItems AND
        // _displayedItemTokens with the identical RemoveAt(0)+Insert(2).
        var items = new List<Widget> { new(1), new(2), new(3) };
        var tokens = new List<long> { 100, 101, 102 };

        var keyForSlot0 = DataGridRowKeys.DomKeyFor(items[0], tokens[0]); // "t:100", issued pre-move

        var moved = items[0];
        items.RemoveAt(0);
        items.Insert(2, moved);
        var movedToken = tokens[0];
        tokens.RemoveAt(0);
        tokens.Insert(2, movedToken);

        // The pre-move key must resolve to the row's NEW slot (2), not the OLD
        // slot (0, now occupied by a different item) and not fail to resolve.
        var resolved = DataGridRowKeys.ResolveDomKeyIndex(items, tokens, keyForSlot0);
        Assert.Equal(2, resolved);
        Assert.Equal(1, items[resolved].Id);

        // A round-6-style positional read (parsing "100" directly as an index)
        // would have returned -1 (out of bounds for a 3-row list) instead.
        Assert.NotEqual(0, resolved);
    }

    [Fact]
    public void ResolveDomKeyIndex_ValueType_Rejects_Unknown_Or_Malformed_Keys()
    {
        var items = new List<Widget> { new(1) };
        var tokens = new List<long> { 5 };

        Assert.Equal(-1, DataGridRowKeys.ResolveDomKeyIndex(items, tokens, "t:999")); // no such token
        Assert.Equal(-1, DataGridRowKeys.ResolveDomKeyIndex(items, tokens, ""));
        Assert.Equal(-1, DataGridRowKeys.ResolveDomKeyIndex(items, tokens, "not-a-token"));
        Assert.Equal(-1, DataGridRowKeys.ResolveDomKeyIndex(items, tokens, "v:0")); // round-6's retired prefix
    }

    [Fact]
    public void DomKeyFor_ReferenceType_Ignores_RowToken_And_Is_Stable_Per_Instance()
    {
        // round-8 (26-bit identity-hash birthday collision fix): DomKeyFor no
        // longer derives the string from RuntimeHelpers.GetHashCode (a 26-bit,
        // NOT-unique CoreCLR identity hash — see DataGridRowKeys' remarks).
        // It now caches a monotonically-issued "r:N" string per instance, so
        // the only observable contract left is: same instance -> same key,
        // every time, regardless of rowToken (which reference types ignore).
        var item = new WidgetRef { Id = 1 };
        var expected = DataGridRowKeys.DomKeyFor(item, 42L);

        Assert.Equal(expected, DataGridRowKeys.DomKeyFor(item, 42L));
        Assert.Equal(expected, DataGridRowKeys.DomKeyFor(item, 0L)); // token is a no-op here
        Assert.StartsWith("r:", expected);
    }

    [Fact]
    public void DomKeyFor_ReferenceType_Never_Collides_Across_100k_Distinct_Instances()
    {
        // The bug this fixes: RuntimeHelpers.GetHashCode is a 26-bit identity
        // hash shared by every live object in the process, so two of ~1,200
        // distinct rows already had a measured ~1.9% chance of colliding on
        // the same @key / data-row-key per render (birthday bound). The
        // ConditionalWeakTable-cached counter key is collision-free by
        // construction — assert it holds at a population far larger than the
        // 26-bit space (2^26 ~= 67,108,864) would ever tolerate collision-free.
        var seen = new HashSet<string>();
        for (var i = 0; i < 100_000; i++)
        {
            var key = DataGridRowKeys.DomKeyFor(new WidgetRef { Id = i }, 0L);
            Assert.True(seen.Add(key), $"Duplicate DOM key '{key}' at iteration {i}.");
        }
    }

    [Fact]
    public void ResolveDomKeyIndex_ReferenceType_Resolves_By_Cached_Identity_Key_Regardless_Of_Tokens()
    {
        var a = new WidgetRef { Id = 1 };
        var b = new WidgetRef { Id = 2 };
        var items = new List<WidgetRef> { a, b };
        // Reference types never read the tokens array — an empty one must not
        // affect resolution.
        var tokens = new List<long>();

        var key = DataGridRowKeys.DomKeyFor(a, 0L);
        Assert.Equal(0, DataGridRowKeys.ResolveDomKeyIndex(items, tokens, key));
    }

    [Fact]
    public void ResolveDomKeyIndex_ReferenceType_Rejects_A_Key_Never_Issued_To_Any_Live_Item()
    {
        var items = new List<WidgetRef> { new() { Id = 1 } };
        var tokens = new List<long>();

        Assert.Equal(-1, DataGridRowKeys.ResolveDomKeyIndex(items, tokens, "r:999999999"));
    }

    // ===========================================================================
    // KeyFor — the Blazor @key value itself (round-8: 26-bit identity-hash
    // birthday collision fix). This is the value plugged straight into
    // DataGridBody's `@key="DataGridRowKeys.KeyFor(item)"`; Blazor's
    // RenderTreeDiffBuilder compares siblings' keys with object.Equals, and
    // throws "More than one sibling … has the same key value" on a collision.
    // ===========================================================================

    [Fact]
    public void KeyFor_ValueType_Returns_The_Boxed_Item_Unchanged()
    {
        // Preserves the pre-existing value-type contract: value-equal boxed
        // structs compare equal as @key values (Blazor dedupes on Equals),
        // exactly as before this fix — only the reference-type path changed.
        object k1 = DataGridRowKeys.KeyFor(new Widget(1));
        object k2 = DataGridRowKeys.KeyFor(new Widget(1));
        Assert.Equal(k1, k2);

        object k3 = DataGridRowKeys.KeyFor(new Widget(2));
        Assert.NotEqual(k1, k3);
    }

    [Fact]
    public void KeyFor_ReferenceType_Same_Instance_Returns_The_Same_Key_Across_Calls()
    {
        var item = new WidgetRef { Id = 1 };
        var k1 = DataGridRowKeys.KeyFor(item);
        var k2 = DataGridRowKeys.KeyFor(item);

        Assert.Same(k1, k2); // reference equality: the cached key object itself
        Assert.Equal(k1, k2); // and therefore also Equals-equal, per @key's contract
    }

    [Fact]
    public void KeyFor_ReferenceType_Distinct_Instances_Never_Share_A_Key_Across_100k_Objects()
    {
        // This is the actual crash: RuntimeHelpers.GetHashCode is a 26-bit
        // identity hash (max observed value 2^26-1 = 67,108,862), so ~1,200
        // distinct reference-type rows already carried a measured ~1.9%
        // per-render collision probability (birthday bound; ~17% at 5k rows,
        // ~53% at 10k). The ConditionalWeakTable-cached key object is
        // collision-free by construction (reference equality, unbounded
        // range) — assert it holds far past where the identity hash would
        // have started colliding.
        var seen = new HashSet<object>();
        for (var i = 0; i < 100_000; i++)
        {
            var key = DataGridRowKeys.KeyFor(new WidgetRef { Id = i });
            Assert.True(seen.Add(key), $"Duplicate @key object at iteration {i}.");
        }
    }

    [Fact]
    public void KeyFor_ReferenceType_Distinct_Instances_With_Equal_Field_Values_Never_Collide()
    {
        // Two rows that are field-for-field identical (a very plausible
        // real-world shape — duplicate rows, freshly-cloned template rows)
        // must still get distinct keys: KeyFor keys off INSTANCE identity,
        // not field content, exactly as the pre-fix identity-hash path
        // intended (it just failed to deliver on that intent past ~1,200 rows).
        var a = new WidgetRef { Id = 1 };
        var b = new WidgetRef { Id = 1 };

        Assert.NotSame(a, b);
        Assert.NotEqual(DataGridRowKeys.KeyFor(a), DataGridRowKeys.KeyFor(b));
    }

    [Fact]
    public void KeyFor_ReferenceType_Is_Thread_Safe_Under_Concurrent_First_Access()
    {
        // ConditionalWeakTable.GetValue is documented thread-safe and
        // atomically creates-if-absent; concurrent first-time KeyFor calls
        // for the SAME instance (e.g. a re-render racing a background diff)
        // must still agree on exactly one key object.
        var item = new WidgetRef { Id = 1 };
        var keys = new object[64];

        Parallel.For(0, keys.Length, i => keys[i] = DataGridRowKeys.KeyFor(item));

        Assert.True(keys.All(k => ReferenceEquals(k, keys[0])));
    }
}
