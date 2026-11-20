using System.Runtime.CompilerServices;
using Lumeo.Services;

namespace Lumeo;

/// <summary>
/// One <see cref="SortableList{TItem}"/> as seen by the group registry. The registry
/// moves items between lists as plain <see cref="object"/>s, so lists with different
/// <c>TItem</c> can coexist in a group and simply refuse items they cannot hold.
/// </summary>
internal interface ISortableGroupMember
{
    string ContainerId { get; }
    string? Group { get; }
    bool Disabled { get; }
    int Count { get; }
    object? ItemAt(int index);
    bool CanAccept(object? item);
    /// <summary>Runs on the renderer's synchronization context (needed for the JS-initiated touch path).</summary>
    Task RunAsync(Func<Task> work);
    void RemoveForTransfer(int index);
    void InsertForTransfer(object? item, int index);
    void ResetDragState();
    void RequestHandleFocus(int index);
    Task NotifyTransferAsync(object? item, string group, string sourceId, int oldIndex, string targetId, int newIndex);
}

/// <summary>
/// Per-circuit coordination point for <see cref="SortableList{TItem}"/> instances that
/// share a <c>Group</c>. It remembers which list/row is currently being dragged so the
/// list the item is dropped on can resolve the actual item object (no serialization),
/// and performs the remove-from-source / insert-into-target move. One registry exists per
/// <see cref="IComponentInteropService"/> instance, which is scoped to the circuit, so
/// concurrent users never see each other's drags.
/// </summary>
internal sealed class SortableGroupRegistry
{
    private static readonly ConditionalWeakTable<IComponentInteropService, SortableGroupRegistry> Registries = new();

    private readonly List<ISortableGroupMember> _members = [];
    private readonly object _gate = new();

    private ISortableGroupMember? _dragSource;
    private int _dragIndex = -1;

    public static SortableGroupRegistry For(IComponentInteropService interop)
        => Registries.GetValue(interop, _ => new SortableGroupRegistry());

    public void Register(ISortableGroupMember member)
    {
        lock (_gate)
        {
            if (!_members.Contains(member))
                _members.Add(member);
        }
    }

    public void Unregister(ISortableGroupMember member)
    {
        lock (_gate)
        {
            _members.Remove(member);
            if (ReferenceEquals(_dragSource, member))
                ClearDrag();
        }
    }

    /// <summary>Lists in the same non-empty group as <paramref name="member"/>, in registration (render) order, including itself.</summary>
    public IReadOnlyList<ISortableGroupMember> Peers(ISortableGroupMember member)
    {
        if (string.IsNullOrEmpty(member.Group))
            return [member];
        lock (_gate)
        {
            return _members.Where(m => string.Equals(m.Group, member.Group, StringComparison.Ordinal)).ToList();
        }
    }

    public ISortableGroupMember? Find(string containerId)
    {
        lock (_gate)
        {
            return _members.FirstOrDefault(m => m.ContainerId == containerId);
        }
    }

    public void BeginDrag(ISortableGroupMember source, int index)
    {
        _dragSource = source;
        _dragIndex = index;
    }

    public void EndDrag(ISortableGroupMember source)
    {
        if (ReferenceEquals(_dragSource, source))
            ClearDrag();
    }

    /// <summary>
    /// True when a drag that started in a DIFFERENT list is in flight and
    /// <paramref name="target"/> may receive its item (same non-empty group,
    /// both enabled, item type fits).
    /// </summary>
    public bool IsForeignDragAcceptedBy(ISortableGroupMember target)
    {
        var source = _dragSource;
        if (source is null || ReferenceEquals(source, target) || _dragIndex < 0 || _dragIndex >= source.Count)
            return false;
        return AreCompatible(source, target) && target.CanAccept(source.ItemAt(_dragIndex));
    }

    /// <summary>Completes a pointer drag onto <paramref name="target"/>. Returns false when the drag is not a valid transfer.</summary>
    public async Task<bool> DropOnAsync(ISortableGroupMember target, int targetIndex)
    {
        var source = _dragSource;
        var index = _dragIndex;
        ClearDrag();
        if (source is null || ReferenceEquals(source, target))
            return false;
        return await TransferAsync(source, index, target, targetIndex);
    }

    /// <summary>Completes a touch drag reported by JS as source container + index, target container + index.</summary>
    public async Task TouchTransferAsync(string sourceId, int sourceIndex, string targetId, int targetIndex)
    {
        var source = Find(sourceId);
        var target = Find(targetId);
        if (source is null || target is null || ReferenceEquals(source, target))
            return;
        await target.RunAsync(() => TransferAsync(source, sourceIndex, target, targetIndex));
    }

    /// <summary>
    /// Moves one item from <paramref name="source"/> to <paramref name="target"/>. Fires the
    /// source list's ItemsChanged (item removed) first, then the target's (item inserted),
    /// then the OnTransfer callback on both. Returns false when the move is rejected.
    /// </summary>
    public async Task<bool> TransferAsync(ISortableGroupMember source, int sourceIndex, ISortableGroupMember target, int targetIndex)
    {
        if (ReferenceEquals(source, target) || !AreCompatible(source, target))
            return false;
        if (sourceIndex < 0 || sourceIndex >= source.Count)
            return false;

        var item = source.ItemAt(sourceIndex);
        if (!target.CanAccept(item))
            return false;

        var group = source.Group!;
        var insertAt = Math.Clamp(targetIndex, 0, target.Count);

        source.RemoveForTransfer(sourceIndex);
        target.InsertForTransfer(item, insertAt);
        source.ResetDragState();
        target.ResetDragState();

        await source.NotifyTransferAsync(item, group, source.ContainerId, sourceIndex, target.ContainerId, insertAt);
        await target.NotifyTransferAsync(item, group, source.ContainerId, sourceIndex, target.ContainerId, insertAt);
        return true;
    }

    private static bool AreCompatible(ISortableGroupMember a, ISortableGroupMember b)
        => !string.IsNullOrEmpty(a.Group)
           && string.Equals(a.Group, b.Group, StringComparison.Ordinal)
           && !a.Disabled && !b.Disabled;

    private void ClearDrag()
    {
        _dragSource = null;
        _dragIndex = -1;
    }
}
