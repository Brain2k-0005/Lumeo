namespace Lumeo;

/// <summary>
/// Marks a property on a <c>[LumeoForm]</c>-annotated POCO so the Lumeo source generator
/// skips it entirely and emits no diagnostic. Use this to suppress
/// <c>LMF001</c> (read-only / init-only property) and <c>LMF002</c> (unsupported type)
/// for properties that intentionally do not belong in the generated form.
/// </summary>
/// <remarks>
/// The generator also honours <c>System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute</c>
/// (EF Core convention) as an equivalent silencer.
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class LumeoFormIgnoreAttribute : System.Attribute
{
}
