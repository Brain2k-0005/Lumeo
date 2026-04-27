using System.ComponentModel.DataAnnotations;

/// <summary>
/// Form model scaffolded by `dotnet new lumeo-form`.
/// Uses System.ComponentModel.DataAnnotations validation —
/// rendered by Blazor's DataAnnotationsValidator inside the EditForm
/// in <c>__PageName__.razor</c>.
/// </summary>
public sealed class __ModelName__Model
{
    [Required(ErrorMessage = "Name is required.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Message must be 2000 characters or fewer.")]
    [Display(Name = "Message")]
    public string? Message { get; set; }
}
