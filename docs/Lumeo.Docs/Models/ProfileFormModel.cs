using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Lumeo.Docs.Models;

/// <summary>
/// Compile-the-output fixture for <c>[LumeoForm]</c>: exercises every input mapping the
/// generator gained in the Wave D pass — MultilineText→Textarea, Phone/Url→typed Input,
/// TimeOnly/TimeSpan→TimePicker, List&lt;string&gt;→TagInput, nullable numeric clear-to-null,
/// Range→Min/Max, StringLength→MaxLength, and [Display(Order)] sorting. Because the docs
/// project references the generator, the generated <c>RenderForm</c> is compiled against
/// the real Lumeo components here — so a wrong parameter name or type fails the build.
/// </summary>
[LumeoForm(Title = "Profile")]
public partial class ProfileFormModel
{
    [Display(Name = "Display name", Order = 1)]
    [StringLength(40)]
    public string DisplayName { get; set; } = "";

    [Display(Name = "Bio", Order = 2)]
    [DataType(DataType.MultilineText)]
    [StringLength(500)]
    public string? Bio { get; set; }

    [Display(Name = "Phone", Order = 3)]
    [DataType(DataType.PhoneNumber)]
    public string? Phone { get; set; }

    [Display(Name = "Website", Order = 4)]
    [DataType(DataType.Url)]
    public string? Website { get; set; }

    [Display(Name = "Preferred start time", Order = 5)]
    public TimeOnly StartTime { get; set; }

    [Display(Name = "Session length", Order = 6)]
    public TimeSpan SessionLength { get; set; }

    [Display(Name = "Interests", Order = 7)]
    public List<string> Interests { get; set; } = new();

    [Display(Name = "Lucky number", Order = 8)]
    public int? LuckyNumber { get; set; }

    [Display(Name = "Completion", Order = 9)]
    [Range(0, 100)]
    public int Completion { get; set; }

    // Boolean must NOT be implicitly required (false is valid).
    [Display(Name = "Public profile", Order = 10)]
    public bool IsPublic { get; set; }
}
