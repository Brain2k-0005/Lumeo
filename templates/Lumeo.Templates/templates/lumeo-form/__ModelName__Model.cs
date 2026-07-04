using System.ComponentModel.DataAnnotations;
using Lumeo;

/// <summary>
/// Form model scaffolded by <c>dotnet new lumeo-form</c>.
///
/// The <c>[LumeoForm]</c> attribute is a Roslyn source generator (shipped in the
/// Lumeo core package) that emits a static <c>RenderForm(model, onValidSubmit)</c>
/// method on this class. It builds a Lumeo <c>&lt;Form&gt;</c> with one
/// <c>&lt;FormField&gt;</c> per public property, picks the right input per type,
/// and wires the built-in <c>DataAnnotationsFormValidator</c> so the
/// DataAnnotations below run at submit time and errors render under each field.
///
/// The class MUST be <c>partial</c> so the generator can extend it.
/// See <c>__PageName__.razor</c> for how the generated form is rendered.
/// </summary>
[LumeoForm(Title = "__ModelName__", SubmitLabel = "Submit")]
public partial class __ModelName__Model
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(80, MinimumLength = 2)]
    [Display(Name = "Name", Description = "Your full name.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [DataType(DataType.EmailAddress)]
    [Display(Name = "Email", Description = "We'll never share your address.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Message must be 2000 characters or fewer.")]
    [Display(Name = "Message")]
    public string? Message { get; set; }
}
