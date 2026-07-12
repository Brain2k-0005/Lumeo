using System.Text.Json.Serialization;

namespace Lumeo.Serialization;

/// <summary>
/// Source-generated JSON metadata for Lumeo's own closed-shape DTOs: the QueryBuilder
/// serialized query tree (<see cref="QueryGroup"/>/<see cref="QueryRule"/>/<see cref="QueryNode"/>)
/// and <c>ConsentService</c>'s persisted consent record.
/// <para>
/// Reflection-based <c>JsonSerializer.Serialize/Deserialize&lt;T&gt;</c> carries
/// <c>RequiresUnreferencedCode</c> — using it inside a trimmable assembly (<c>IsTrimmable</c>)
/// produces IL2026 warnings and, worse, is genuinely unsafe once member-level trimming is
/// live: the target types' properties are only reachable through this reflection, so the
/// trimmer can (and did) remove them. A <see cref="JsonSerializerContext"/> makes the
/// (de)serialization plan for these types compile-time metadata instead, closing both
/// problems for the types listed below. See #354.
/// </para>
/// <para>
/// <see cref="QueryRule.Value"/>/<see cref="QueryRule.Value2"/> are declared <c>object?</c> —
/// a genuinely open slot public API lets consumer code box anything into (not just the
/// string/double/bool <c>QueryBuilderGroup.razor</c>'s built-in editors produce). Rather than
/// lean on System.Text.Json's polymorphic <c>object</c> resolution for that (which, combined
/// with a reflection fallback resolver, proved unreliable for a source-gen context holding a
/// <c>List</c> of a polymorphic base with an <c>object</c> leaf member), both properties carry
/// an explicit <c>[JsonConverter(typeof(QueryValueJsonConverter))]</c> that pattern-matches the
/// small set of framework scalar shapes directly — no runtime type resolution needed for that
/// member at all. string/double/bool/object stay registered here regardless, since Value/
/// Value2 aren't the only place they're useful (string alone backs Field/Operator).
/// </para>
/// <para>
/// Every property below carries an explicit <see cref="JsonPropertyNameAttribute"/> rather than
/// a <see cref="JsonSourceGenerationOptionsAttribute"/> naming policy: <c>QueryBuilderSerialization</c>
/// wraps this context in two independently-configured instances (WriteIndented for the pretty
/// JSON preview; PropertyNameCaseInsensitive for tolerant reads), and a naming policy set here
/// does not reliably carry over to a re-wrapped instance — explicit names sidestep that.
/// </para>
/// </summary>
[JsonSerializable(typeof(QueryGroup))]
[JsonSerializable(typeof(QueryRule))]
[JsonSerializable(typeof(QueryNode))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(Lumeo.Services.ConsentService.ConsentRecord))]
internal partial class LumeoJsonContext : JsonSerializerContext
{
}
