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
}
