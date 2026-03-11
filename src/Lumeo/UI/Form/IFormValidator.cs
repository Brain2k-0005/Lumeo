namespace Lumeo;

public interface IFormValidator
{
    Dictionary<string, List<string>> Validate(object model);
    List<string> ValidateField(object model, string fieldName);
}
