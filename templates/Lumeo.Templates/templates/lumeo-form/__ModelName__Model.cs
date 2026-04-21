using System.ComponentModel.DataAnnotations;
using Lumeo;

[LumeoForm(Title = "__ModelName__", SubmitLabel = "Submit")]
public partial class __ModelName__Model
{
    [Required, Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Message")]
    public string? Message { get; set; }
}
