namespace Lumeo;

/// <summary>
/// JSON-serializable snapshot of a <see cref="TabsList"/>'s current tab order.
/// Used to persist user-reordered tabs across sessions in tandem with
/// <see cref="TabsList.SavedLayout"/> and <see cref="TabsList.OnLayoutChanged"/>.
/// </summary>
/// <param name="Order">
/// The ordered list of <see cref="TabsTrigger.Value"/> identifiers, in the
/// order they should appear in the list.
/// </param>
/// <remarks>
/// The library never mutates the consumer's backing collection. The consumer
/// is responsible for:
/// <list type="bullet">
/// <item>Persisting the <see cref="TabsLayout"/> received from
/// <see cref="TabsList.OnLayoutChanged"/> (e.g. to local storage or a server).</item>
/// <item>Re-ordering its backing collection from <see cref="Order"/> on
/// initialisation, before passing it to <see cref="TabsList.SavedLayout"/>.</item>
/// </list>
/// This mirrors the existing reorder model where <c>OnReorder</c> reports the
/// drag without applying it — the consumer remains the source of truth.
/// </remarks>
public sealed record TabsLayout(IReadOnlyList<string> Order);
