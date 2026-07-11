using System.Globalization;
using System.Runtime.CompilerServices;
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
    public void DomKeyFor_ReferenceType_Ignores_RowToken_And_Uses_The_Identity_Hash()
    {
        var item = new WidgetRef { Id = 1 };
        var expected = RuntimeHelpers.GetHashCode(item).ToString(CultureInfo.InvariantCulture);

        Assert.Equal(expected, DataGridRowKeys.DomKeyFor(item, 42L));
        Assert.Equal(expected, DataGridRowKeys.DomKeyFor(item, 0L)); // token is a no-op here
    }

    [Fact]
    public void ResolveDomKeyIndex_ReferenceType_Resolves_By_Identity_Hash_Regardless_Of_Tokens()
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
}
