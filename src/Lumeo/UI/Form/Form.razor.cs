using System.Diagnostics.CodeAnalysis;

namespace Lumeo;

// Partial declaration carrying the trim annotation for the open TModel type parameter.
// Form.razor's generated partial declares `Form<TModel>` (no attribute); C# allows a
// generic type parameter's attributes/constraints to be declared on exactly one partial
// part, so this file is the sole place TModel's DynamicallyAccessedMembers lives.
//
// TModel is an arbitrary consumer POCO — Form serializes it via System.Text.Json (init
// snapshot + ResetValues round-trip) and reflects over its public properties
// (CopyPublicProperties). None of that is knowable at Lumeo-compile-time, so it can't be
// closed with a JsonSerializerContext; annotating TModel with [DynamicallyAccessedMembers(All)]
// instead makes the trimmer preserve every member of whatever concrete TModel a consumer
// instantiates Form<TModel> with (see #354).
public partial class Form<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TModel>
{
}
