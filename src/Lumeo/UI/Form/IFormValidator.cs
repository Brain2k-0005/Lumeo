using System.Diagnostics.CodeAnalysis;

namespace Lumeo;

public interface IFormValidator
{
    // model's actual type is only known at the call site (Form<TModel>'s Model), so an
    // implementation is free to use reflection (e.g. DataAnnotationsFormValidator does).
    // RequiresUnreferencedCode documents that honestly instead of silently swallowing an
    // ILLink warning here; see DataAnnotationsFormValidator and Form.razor's call sites
    // (both are trim-safe because TModel carries [DynamicallyAccessedMembers(All)]).
    [RequiresUnreferencedCode("Implementations may reflect over the model's runtime type " +
        "(e.g. DataAnnotationsFormValidator). Safe when the model's type preserves its " +
        "public members, as Form<TModel> guarantees via [DynamicallyAccessedMembers(All)] on TModel.")]
    Dictionary<string, List<string>> Validate(object model);

    [RequiresUnreferencedCode("Implementations may reflect over the model's runtime type " +
        "(e.g. DataAnnotationsFormValidator). Safe when the model's type preserves its " +
        "public members, as Form<TModel> guarantees via [DynamicallyAccessedMembers(All)] on TModel.")]
    List<string> ValidateField(object model, string fieldName);
}
