using System.ComponentModel.DataAnnotations;

namespace Lumeo.Docs.Models;

/// <summary>
/// Demo POCO used by the LumeoForm docs page — annotated with <c>[LumeoForm]</c>
/// so the source generator emits a <c>RenderForm(model, onSubmit)</c> method.
/// </summary>
[LumeoForm(Title = "Contact us", SubmitLabel = "Send message")]
public partial class ContactFormModel
{
    [Required]
    [Display(Name = "Full name", Description = "As it should appear on the invoice.")]
    [StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required]
    [DataType(DataType.EmailAddress)]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    [StringLength(64, MinimumLength = 8)]
    public string Password { get; set; } = "";

    [Range(1, 120)]
    [Display(Name = "Age")]
    public int Age { get; set; } = 25;

    [Display(Name = "Subscribe to newsletter")]
    public bool Subscribe { get; set; }

    [Display(Name = "Preferred contact")]
    public ContactPreference Preference { get; set; } = ContactPreference.Email;
}

public enum ContactPreference
{
    Email,
    Phone,
    SmsMessage,
}
