using System.Text.Json.Serialization;

namespace Lumeo.Serialization;

/// <summary>
/// Source-generated JSON metadata for <c>ConsentService</c>'s persisted consent record.
/// <para>
/// Reflection-based <c>JsonSerializer.Serialize/Deserialize&lt;T&gt;</c> carries
/// <c>RequiresUnreferencedCode</c> — using it inside a trimmable assembly (<c>IsTrimmable</c>)
/// produces IL2026 warnings and, worse, is genuinely unsafe once member-level trimming is
/// live: the target type's properties are only reachable through this reflection, so the
/// trimmer can (and did) remove them. A <see cref="JsonSerializerContext"/> makes the
/// (de)serialization plan compile-time metadata instead, closing both problems. See #354.
/// </para>
/// <para>
/// The QueryBuilder serialized query tree (<c>QueryGroup</c>/<c>QueryRule</c>/<c>QueryNode</c>)
/// used to live in this same shared context, but <c>lumeo add query-builder</c> vendors
/// <c>QueryBuilderModel.cs</c> standalone into consumer projects, where this type — being
/// `internal` to the Lumeo assembly — can't be referenced. That tree now has its own
/// self-contained <c>QueryBuilderJsonContext</c> defined directly in <c>QueryBuilderModel.cs</c>
/// so the vendored file compiles on its own (#364 review).
/// </para>
/// </summary>
[JsonSerializable(typeof(Lumeo.Services.ConsentService.ConsentRecord))]
internal partial class LumeoJsonContext : JsonSerializerContext
{
}
