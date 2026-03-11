using System.ComponentModel.DataAnnotations;

namespace Lumeo;

public class DataAnnotationsFormValidator : IFormValidator
{
    public Dictionary<string, List<string>> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, true);

        var errors = new Dictionary<string, List<string>>();
        foreach (var result in results)
        {
            foreach (var memberName in result.MemberNames)
            {
                if (!errors.ContainsKey(memberName))
                    errors[memberName] = new List<string>();
                if (result.ErrorMessage is not null)
                    errors[memberName].Add(result.ErrorMessage);
            }
        }
        return errors;
    }

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
