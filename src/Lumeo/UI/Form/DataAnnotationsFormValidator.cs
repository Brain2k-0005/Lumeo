using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Lumeo;

public class DataAnnotationsFormValidator : IFormValidator
{
    /// <summary>
    /// Sentinel key under which form-level (class-level) validation errors are
    /// recorded — i.e. results whose <see cref="ValidationResult.MemberNames"/> is
    /// empty (produced by <see cref="IValidatableObject"/> or a class-scoped
    /// validation attribute). A FormMessage with this Name surfaces them.
    /// </summary>
    public const string FormLevelErrorKey = "__form";

    [RequiresUnreferencedCode("DataAnnotations validation reflects over the model's runtime " +
        "type (ValidationContext resolves DisplayNameAttribute; Validator walks [Validation] " +
        "attributes on its members). Safe when the model's type preserves its public members.")]
    public Dictionary<string, List<string>> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, true);

        var errors = new Dictionary<string, List<string>>();
        foreach (var result in results)
        {
            // Class-level / form-level validation (e.g. IValidatableObject or a
            // class-scoped attribute) yields an empty MemberNames. Without a
            // sentinel key those errors are silently dropped, so the form would
            // report valid while DataAnnotations actually failed. Record them
            // under FormLevelErrorKey so a form-level FormMessage can surface them.
            var memberNames = result.MemberNames.Any()
                ? result.MemberNames
                : new[] { FormLevelErrorKey };
            foreach (var memberName in memberNames)
            {
                if (!errors.ContainsKey(memberName))
                    errors[memberName] = new List<string>();
                if (result.ErrorMessage is not null)
                    errors[memberName].Add(result.ErrorMessage);
            }
        }
        return errors;
    }

    [RequiresUnreferencedCode("DataAnnotations validation reflects over the model's runtime " +
        "type (ValidationContext resolves DisplayNameAttribute; Type.GetProperty(fieldName) " +
        "looks up the target member). Safe when the model's type preserves its public members.")]
    public List<string> ValidateField(object model, string fieldName)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model) { MemberName = fieldName };
        var prop = model.GetType().GetProperty(fieldName);
        if (prop is null) return new();
        Validator.TryValidateProperty(prop.GetValue(model), context, results);
        return results.Where(r => r.ErrorMessage is not null).Select(r => r.ErrorMessage!).ToList();
    }
}
