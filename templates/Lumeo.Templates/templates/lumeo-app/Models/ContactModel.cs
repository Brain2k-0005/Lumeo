using System.ComponentModel.DataAnnotations;

namespace MyApp;

/// <summary>
/// Demo model for the Form page. Standard <c>System.ComponentModel.DataAnnotations</c>
/// attributes are enforced by Lumeo's <c>DataAnnotationsFormValidator</c> when the
/// <c>&lt;Form&gt;</c> is submitted; each <c>&lt;FormField Name="…"&gt;</c> shows the
/// matching error automatically.
/// </summary>
public sealed class ContactModel
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Name must be 2–80 characters.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string? Email { get; set; }

    [StringLength(500, ErrorMessage = "Message must be 500 characters or fewer.")]
    public string? Message { get; set; }
}
