using Xunit;

namespace Lumeo.Tests.Serialization;

/// <summary>
/// Regression tests for a trimmed-publish production crash: under
/// <c>PublishTrimmed</c> (Lumeo assemblies are IsTrimmable since 4.3.0), the IL
/// linker strips constructor PARAMETER NAMES from types that are never
/// constructed with <c>Activator.CreateInstance</c> or a reflection-visible call
/// site. JSRuntime's reflection-based System.Text.Json then throws
/// <c>NotSupportedException("ConstructorContainsNullParameterNames")</c> the
/// first time it needs to (de)serialize a POSITIONAL RECORD (or any type whose
/// only constructor takes parameters) across the JS interop boundary — crashing
/// the component at runtime. Hit live twice: anonymous JS-interop option bags
/// (fixed by using <c>Dictionary&lt;string, object?&gt;</c> instead — not a type
/// with a fixed constructor, so nothing to strip) and
/// <c>Lumeo.Services.PreventDefaultKeyRule</c> (crashed nearly every docs page
/// via the keyboard scroll-suppression interop call registered by ~60
/// components).
///
/// System.Text.Json picks a type's PARAMETERLESS constructor over a parameterized
/// one when both exist (unless <c>[JsonConstructor]</c> says otherwise), and
/// (de)serializes via properties instead — positional-record <c>init</c>
/// accessors are settable by STJ. So every record below got a parameterless
/// constructor added (chaining into its own defaults) with ZERO call-site
/// changes required; see each type's own "Trim safety" comment for detail.
///
/// This test is a compile-time-checked list: it does not itself exercise a
/// trimmed publish (that is a separate, heavier E2E gate — not built here), but
/// it pins that every payload type known to cross the JS interop boundary has a
/// public parameterless constructor, so a NEW positional record added to
/// interop without one fails review here instead of crashing in production.
/// </summary>
public class InteropPayloadTrimSafetyTests
{
    public static TheoryData<Type> InteropPayloadTypes => new()
    {
        // src/Lumeo — outgoing arg to RegisterPreventDefaultKeys (InvokeVoidAsync),
        // used by ~60 components (Accordion, AudioPlayer, Calendar, Card, Carousel,
        // Cascader, Chip, Combobox, DataGrid, ... TreeView).
        typeof(Lumeo.Services.PreventDefaultKeyRule),

        // src/Lumeo — InvokeAsync<T> return values (JS -> .NET deserialization).
        typeof(Lumeo.Services.ElementRect),
        typeof(Lumeo.Services.ViewportSize),
        typeof(Lumeo.Services.RipplePoint),
        typeof(Lumeo.Services.MediaState),
        typeof(Lumeo.Services.ComponentInteropService.TextareaCaretInfo),
        typeof(Lumeo.Services.ComponentInteropService.TabMeasurement),
        typeof(Lumeo.Services.Interop.RichTextActiveState),

        // src/Lumeo.Gantt — [JSInvokable] parameter type (JsOnTaskClick/
        // JsOnDateChange/JsOnProgressChange deserialize this from JS).
        typeof(Lumeo.GanttTask),

        // src/Lumeo.Scheduler — [JSInvokable] parameter types (JsOnEventClick/
        // JsOnEventChange/JsOnDateSelect deserialize these from JS).
        typeof(Lumeo.SchedulerEvent),
        typeof(Lumeo.SchedulerDateRange),

        // src/Lumeo.Editor — returned to JS from the OnTriggerQuery [JSInvokable]
        // method (IReadOnlyList<TriggerItem>).
        typeof(Lumeo.TriggerItem),
    };

    [Theory]
    [MemberData(nameof(InteropPayloadTypes))]
    public void InteropPayloadType_Has_Public_Parameterless_Constructor(Type payloadType)
    {
        // A positional record's ONLY constructor is the parameterized primary one
        // unless a parameterless ctor is added explicitly. Under a trimmed publish
        // the linker strips that parameterized ctor's parameter names, and
        // JSRuntime's reflection-based System.Text.Json throws
        // NotSupportedException("ConstructorContainsNullParameterNames") the moment
        // it needs to resolve JsonTypeInfo for the type — whether serializing
        // .NET -> JS or deserializing JS -> .NET. A public parameterless
        // constructor makes STJ prefer property-based (de)serialization instead,
        // which needs no constructor parameter metadata at all.
        var ctor = payloadType.GetConstructor(Type.EmptyTypes);
        Assert.True(
            ctor is not null && ctor.IsPublic,
            $"{payloadType.FullName} crosses the JS interop boundary but has no public " +
            "parameterless constructor — it WILL throw " +
            "NotSupportedException(\"ConstructorContainsNullParameterNames\") under a " +
            "trimmed publish. Add one that chains into the type's own defaults (see " +
            "Lumeo.Services.PreventDefaultKeyRule for the pattern).");
    }
}
