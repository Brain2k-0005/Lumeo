namespace Lumeo;

/// <summary>
/// Marks a POCO so the Lumeo source generator emits a static <c>RenderForm</c> method
/// returning a pre-built <see cref="Microsoft.AspNetCore.Components.RenderFragment"/>
/// that wires a Lumeo <c>&lt;Form&gt;</c> with one <c>&lt;FormField&gt;</c> per public
/// property. Input components are picked based on property type — see
/// <c>docs/Lumeo.Docs/Pages/Docs/LumeoForm.razor</c> for the full type → component table.
/// </summary>
/// <remarks>
/// <para>
/// The generator honours <see cref="System.ComponentModel.DataAnnotations.RequiredAttribute"/>,
/// <see cref="System.ComponentModel.DataAnnotations.DisplayAttribute"/>
/// (<c>Name</c> → label, <c>Description</c> → help text), and
/// <see cref="System.ComponentModel.DataAnnotations.DataTypeAttribute"/>
/// (<c>EmailAddress</c> → <c>type="email"</c>, <c>Password</c> → <c>PasswordInput</c>).
/// </para>
/// <para>
/// Runtime validation is performed by the <c>DataAnnotationsFormValidator</c> injected
/// into the generated <c>&lt;Form&gt;</c>, so <see cref="System.ComponentModel.DataAnnotations.StringLengthAttribute"/>,
/// <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/> and friends are
/// enforced at submit time without any extra wiring.
/// </para>
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class LumeoFormAttribute : System.Attribute
{
    /// <summary>Optional heading rendered above the generated form.</summary>
    public string? Title { get; set; }

    /// <summary>When true (default), appends a submit button to the generated form.</summary>
    public bool IncludeSubmitButton { get; set; } = true;

    /// <summary>Label for the generated submit button.</summary>
    public string SubmitLabel { get; set; } = "Submit";
}
