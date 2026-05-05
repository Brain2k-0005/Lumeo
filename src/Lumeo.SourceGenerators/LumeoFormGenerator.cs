using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Lumeo.SourceGenerators;

/// <summary>
/// Incremental generator that emits a <c>RenderForm</c> method on every class marked
/// with <c>[LumeoForm]</c>. The generated method returns a Blazor
/// <see cref="Microsoft.AspNetCore.Components.RenderFragment"/> that renders a
/// <c>&lt;Form&gt;</c> with one <c>&lt;FormField&gt;</c> per public property,
/// picking the right Lumeo input component based on the property type.
///
/// The marker attribute <c>Lumeo.LumeoFormAttribute</c> itself is also
/// contributed as a post-initialization source — consumers don't need to ship it.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class LumeoFormGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Lumeo.LumeoFormAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The Lumeo.LumeoFormAttribute type itself is declared in the Lumeo runtime
        // (src/Lumeo/Attributes/LumeoFormAttribute.cs). No post-initialization emission
        // here — avoids duplicate definitions when Lumeo references the generator.
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => BuildModel(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        context.RegisterSourceOutput(targets, static (spc, model) =>
        {
            var source = Emit(model);
            spc.AddSource($"{model.HintName}.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    // ------------------------------------------------------------------ model

    private sealed class FormModel
    {
        public string Namespace { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string FullyQualifiedName { get; set; } = "";
        public string HintName { get; set; } = "";
        public bool IsValueType { get; set; }
        public bool IncludeSubmitButton { get; set; } = true;
        public string SubmitLabel { get; set; } = "Submit";
        public string? Title { get; set; }
        public List<FieldModel> Fields { get; set; } = new();
    }

    private sealed class FieldModel
    {
        public string PropertyName { get; set; } = "";
        public string PropertyTypeFq { get; set; } = ""; // fully-qualified C# type
        public string Label { get; set; } = "";
        public string? HelpText { get; set; }
        public bool Required { get; set; }
        public InputKind Kind { get; set; }
        public List<EnumMember>? EnumMembers { get; set; }
    }

    private sealed class EnumMember
    {
        public string Name { get; set; } = "";
        public string FqValue { get; set; } = ""; // e.g. global::My.Ns.MyEnum.Option1
    }

    private enum InputKind
    {
        Text,
        Email,
        Password,
        Number,
        Checkbox,
        Date,
        Enum,
        // fallback
        Unsupported
    }

    private static FormModel? BuildModel(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;

        var attr = ctx.Attributes.FirstOrDefault();
        var includeSubmit = true;
        var submitLabel = "Submit";
        string? title = null;

        if (attr is not null)
        {
            foreach (var named in attr.NamedArguments)
            {
                switch (named.Key)
                {
                    case "IncludeSubmitButton":
                        if (named.Value.Value is bool b) includeSubmit = b;
                        break;
                    case "SubmitLabel":
                        if (named.Value.Value is string s) submitLabel = s;
                        break;
                    case "Title":
                        title = named.Value.Value as string;
                        break;
                }
            }
        }

        var model = new FormModel
        {
            Namespace = type.ContainingNamespace.IsGlobalNamespace
                ? ""
                : type.ContainingNamespace.ToDisplayString(),
            ClassName = type.Name,
            FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            HintName = (type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".") + type.Name + ".LumeoForm",
            IsValueType = type.IsValueType,
            IncludeSubmitButton = includeSubmit,
            SubmitLabel = submitLabel,
            Title = title,
        };

        foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility != Accessibility.Public) continue;
            if (member.IsStatic || member.IsIndexer || member.IsReadOnly) continue;
            if (member.SetMethod is null || member.SetMethod.DeclaredAccessibility != Accessibility.Public) continue;

            var field = BuildField(member);
            if (field.Kind == InputKind.Unsupported) continue;
            model.Fields.Add(field);
        }

        return model;
    }

    private static FieldModel BuildField(IPropertySymbol prop)
    {
        var type = prop.Type;
        var fqType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var f = new FieldModel
        {
            PropertyName = prop.Name,
            PropertyTypeFq = fqType,
            Label = SplitPascal(prop.Name),
        };

        // Inspect attributes for [Required], [Display(Name, Description)], [DataType]
        foreach (var ad in prop.GetAttributes())
        {
            var name = ad.AttributeClass?.ToDisplayString();
            switch (name)
            {
                case "System.ComponentModel.DataAnnotations.RequiredAttribute":
                    f.Required = true;
                    break;
                case "System.ComponentModel.DataAnnotations.DisplayAttribute":
                    foreach (var na in ad.NamedArguments)
                    {
                        if (na.Key == "Name" && na.Value.Value is string dn) f.Label = dn;
                        else if (na.Key == "Description" && na.Value.Value is string dd) f.HelpText = dd;
                    }
                    break;
            }
        }

        // value-type, non-nullable → implicitly required
        if (type.IsValueType && type.NullableAnnotation != NullableAnnotation.Annotated)
            f.Required = true;

        // Pick input kind
        f.Kind = MapKind(type, prop);

        if (f.Kind == InputKind.Enum)
        {
            var enumType = UnwrapNullable(type) as INamedTypeSymbol;
            if (enumType is not null && enumType.TypeKind == TypeKind.Enum)
            {
                f.EnumMembers = enumType.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(fs => fs.IsStatic && fs.HasConstantValue)
                    .Select(fs => new EnumMember
                    {
                        Name = fs.Name,
                        FqValue = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + fs.Name,
                    })
                    .ToList();
            }
        }

        return f;
    }

    private static InputKind MapKind(ITypeSymbol type, IPropertySymbol prop)
    {
        var underlying = UnwrapNullable(type);
        var name = underlying.ToDisplayString();

        // [DataType(DataType.EmailAddress)] / .Password
        foreach (var ad in prop.GetAttributes())
        {
            if (ad.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.DataTypeAttribute"
                && ad.ConstructorArguments.Length > 0
                && ad.ConstructorArguments[0].Value is int dt)
            {
                // DataType enum: Password = 11, EmailAddress = 10
                if (dt == 10) return InputKind.Email;
                if (dt == 11) return InputKind.Password;
            }
        }

        if (underlying.TypeKind == TypeKind.Enum) return InputKind.Enum;

        return name switch
        {
            "string" => InputKind.Text,
            "bool" => InputKind.Checkbox,
            "int" or "long" or "short" or "byte"
                or "double" or "float" or "decimal"
                or "uint" or "ulong" or "ushort" or "sbyte"
                                  => InputKind.Number,
            "System.DateTime"
                or "System.DateOnly"
                or "System.DateTimeOffset"
                                  => InputKind.Date,
            _ => InputKind.Unsupported
        };
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol t)
    {
        if (t is INamedTypeSymbol nt && nt.IsGenericType &&
            nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            nt.TypeArguments.Length == 1)
        {
            return nt.TypeArguments[0];
        }
        return t;
    }

    private static string SplitPascal(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 8);
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ emit

    private static string Emit(FormModel m)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS1591, CS8618, CS0108");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(m.Namespace))
        {
            sb.Append("namespace ").Append(m.Namespace).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("partial class ").Append(m.ClassName).AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Generated by <c>[LumeoForm]</c>. Returns a <see cref=\"global::Microsoft.AspNetCore.Components.RenderFragment\"/>");
        sb.AppendLine("    /// that renders a Lumeo <c>&lt;Form&gt;</c> bound to <paramref name=\"model\"/> with one <c>&lt;FormField&gt;</c>");
        sb.AppendLine("    /// per public property.");
        sb.AppendLine("    /// </summary>");
        sb.Append("    public static global::Microsoft.AspNetCore.Components.RenderFragment RenderForm(")
          .Append(m.FullyQualifiedName).Append(" model, global::Microsoft.AspNetCore.Components.EventCallback<")
          .Append(m.FullyQualifiedName).AppendLine("> onValidSubmit)");
        sb.AppendLine("    {");
        sb.AppendLine("        return __builder =>");
        sb.AppendLine("        {");

        int seq = 0;
        int next() => seq++;

        // <Form Model="model" OnValidSubmit="onValidSubmit">
        sb.Append("            __builder.OpenComponent<global::Lumeo.Form<").Append(m.FullyQualifiedName).Append(">>(").Append(next()).AppendLine(");");
        sb.Append("            __builder.AddAttribute(").Append(next()).AppendLine(", \"Model\", model);");
        sb.Append("            __builder.AddAttribute(").Append(next()).AppendLine(", \"OnValidSubmit\", onValidSubmit);");
        sb.Append("            __builder.AddAttribute(").Append(next()).AppendLine(", \"Validator\", (global::Lumeo.IFormValidator)new global::Lumeo.DataAnnotationsFormValidator());");

        sb.Append("            __builder.AddAttribute(").Append(next()).AppendLine(", \"ChildContent\", (global::Microsoft.AspNetCore.Components.RenderFragment)((__form) =>");
        sb.AppendLine("            {");

        if (!string.IsNullOrEmpty(m.Title))
        {
            sb.Append("                __form.OpenElement(").Append(next()).AppendLine(", \"h3\");");
            sb.Append("                __form.AddAttribute(").Append(next()).AppendLine(", \"class\", \"text-lg font-semibold\");");
            sb.Append("                __form.AddContent(").Append(next()).Append(", ").Append(Str(m.Title!)).AppendLine(");");
            sb.AppendLine("                __form.CloseElement();");
        }

        foreach (var f in m.Fields)
        {
            EmitField(sb, m, f, next);
        }

        if (m.IncludeSubmitButton)
        {
            sb.Append("                __form.OpenComponent<global::Lumeo.Button>(").Append(next()).AppendLine(");");
            sb.Append("                __form.AddAttribute(").Append(next()).AppendLine(", \"type\", \"submit\");");
            sb.Append("                __form.AddAttribute(").Append(next()).AppendLine(", \"ChildContent\", (global::Microsoft.AspNetCore.Components.RenderFragment)((__b) =>");
            sb.AppendLine("                {");
            sb.Append("                    __b.AddContent(").Append(next()).Append(", ").Append(Str(m.SubmitLabel)).AppendLine(");");
            sb.AppendLine("                }));");
            sb.AppendLine("                __form.CloseComponent();");
        }

        sb.AppendLine("            }));");
        sb.AppendLine("            __builder.CloseComponent();");

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitField(StringBuilder sb, FormModel m, FieldModel f, Func<int> next)
    {
        // <FormField Label="..." Required="..." HelpText="...">
        sb.Append("                __form.OpenComponent<global::Lumeo.FormField>(").Append(next()).AppendLine(");");
        sb.Append("                __form.AddAttribute(").Append(next()).Append(", \"Label\", ").Append(Str(f.Label)).AppendLine(");");
        sb.Append("                __form.AddAttribute(").Append(next()).Append(", \"Required\", ").Append(f.Required ? "true" : "false").AppendLine(");");
        sb.Append("                __form.AddAttribute(").Append(next()).Append(", \"Name\", ").Append(Str(f.PropertyName)).AppendLine(");");
        if (!string.IsNullOrEmpty(f.HelpText))
        {
            sb.Append("                __form.AddAttribute(").Append(next()).Append(", \"HelpText\", ").Append(Str(f.HelpText!)).AppendLine(");");
        }
        sb.Append("                __form.AddAttribute(").Append(next()).AppendLine(", \"ChildContent\", (global::Microsoft.AspNetCore.Components.RenderFragment)((__field) =>");
        sb.AppendLine("                {");

        switch (f.Kind)
        {
            case InputKind.Text:
            case InputKind.Email:
                EmitTextInput(sb, f, "global::Lumeo.Input", next, isEmail: f.Kind == InputKind.Email);
                break;

            case InputKind.Password:
                EmitTextInput(sb, f, "global::Lumeo.PasswordInput", next);
                break;

            case InputKind.Number:
                EmitNumberInput(sb, f, next);
                break;

            case InputKind.Checkbox:
                EmitCheckbox(sb, f, next);
                break;

            case InputKind.Date:
                EmitDatePicker(sb, f, next);
                break;

            case InputKind.Enum:
                EmitEnumSelect(sb, f, next);
                break;
        }

        sb.AppendLine("                }));");
        sb.AppendLine("                __form.CloseComponent();");
    }

    // Lumeo.Input / PasswordInput: non-generic, `Value` is string?, `ValueChanged` is EventCallback<string?>.
    private static void EmitTextInput(StringBuilder sb, FieldModel f, string componentFq, Func<int> next, bool isEmail = false)
    {
        sb.Append("                    __field.OpenComponent<").Append(componentFq).Append(">(").Append(next()).AppendLine(");");
        if (isEmail)
            sb.Append("                    __field.AddAttribute(").Append(next()).AppendLine(", \"type\", \"email\");");

        sb.Append("                    __field.AddAttribute(").Append(next()).Append(", \"Value\", model.").Append(f.PropertyName).AppendLine(");");
        sb.Append("                    __field.AddAttribute(").Append(next())
          .Append(", \"ValueChanged\", global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string?>(model, __v => model.")
          .Append(f.PropertyName).AppendLine(" = __v!));");
        sb.AppendLine("                    __field.CloseComponent();");
    }

    // Lumeo.NumberInput: non-generic, operates on double?. We coerce the model property via a lambda.
    private static void EmitNumberInput(StringBuilder sb, FieldModel f, Func<int> next)
    {
        sb.Append("                    __field.OpenComponent<global::Lumeo.NumberInput>(").Append(next()).AppendLine(");");
        // Convert underlying numeric value to double? for the component.
        sb.Append("                    __field.AddAttribute(").Append(next()).Append(", \"Value\", (double?)model.").Append(f.PropertyName).AppendLine(");");
        sb.Append("                    __field.AddAttribute(").Append(next())
          .Append(", \"ValueChanged\", global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<double?>(model, __v => model.")
          .Append(f.PropertyName).Append(" = (").Append(f.PropertyTypeFq).AppendLine(")(__v ?? 0)));");
        sb.AppendLine("                    __field.CloseComponent();");
    }

    // Lumeo.Checkbox uses `Checked` / `CheckedChanged`.
    private static void EmitCheckbox(StringBuilder sb, FieldModel f, Func<int> next)
    {
        sb.Append("                    __field.OpenComponent<global::Lumeo.Checkbox>(").Append(next()).AppendLine(");");
        sb.Append("                    __field.AddAttribute(").Append(next()).Append(", \"Checked\", model.").Append(f.PropertyName).AppendLine(");");
        sb.Append("                    __field.AddAttribute(").Append(next())
          .Append(", \"CheckedChanged\", global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<bool>(model, __v => model.")
          .Append(f.PropertyName).AppendLine(" = __v));");
        sb.AppendLine("                    __field.CloseComponent();");
    }

    // Lumeo.DatePicker: non-generic. DateOnly? via Value, DateTime? via DateTimeValue.
    private static void EmitDatePicker(StringBuilder sb, FieldModel f, Func<int> next)
    {
        sb.Append("                    __field.OpenComponent<global::Lumeo.DatePicker>(").Append(next()).AppendLine(");");
        var underlying = f.PropertyTypeFq.TrimEnd('?');
        if (underlying == "global::System.DateTime" || underlying == "global::System.DateTimeOffset")
        {
            sb.Append("                    __field.AddAttribute(").Append(next()).Append(", \"DateTimeValue\", (global::System.DateTime?)model.").Append(f.PropertyName).AppendLine(");");
            sb.Append("                    __field.AddAttribute(").Append(next())
              .Append(", \"DateTimeValueChanged\", global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<global::System.DateTime?>(model, __v => model.")
              .Append(f.PropertyName).Append(" = (").Append(f.PropertyTypeFq).AppendLine(")(__v ?? default)));");
        }
        else // DateOnly
        {
            sb.Append("                    __field.AddAttribute(").Append(next()).Append(", \"Value\", (global::System.DateOnly?)model.").Append(f.PropertyName).AppendLine(");");
            sb.Append("                    __field.AddAttribute(").Append(next())
              .Append(", \"ValueChanged\", global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<global::System.DateOnly?>(model, __v => model.")
              .Append(f.PropertyName).Append(" = (").Append(f.PropertyTypeFq).AppendLine(")(__v ?? default)));");
        }
        sb.AppendLine("                    __field.CloseComponent();");
    }

    // Lumeo.Select is non-generic and operates on string. For enums we convert Enum <-> string via ToString()/Enum.Parse.
    private static void EmitEnumSelect(StringBuilder sb, FieldModel f, Func<int> next)
    {
        var typeArg = f.PropertyTypeFq;
        sb.Append("                    __field.OpenComponent<global::Lumeo.Select>(").Append(next()).AppendLine(");");
        sb.Append("                    __field.AddAttribute(").Append(next()).Append(", \"Value\", model.").Append(f.PropertyName).AppendLine(".ToString());");
        sb.Append("                    __field.AddAttribute(").Append(next())
          .Append(", \"ValueChanged\", global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string?>(model, __v => { if (!string.IsNullOrEmpty(__v)) model.")
          .Append(f.PropertyName).Append(" = (").Append(typeArg).Append(")global::System.Enum.Parse(typeof(").Append(typeArg).AppendLine("), __v!); }));");

        sb.Append("                    __field.AddAttribute(").Append(next())
          .AppendLine(", \"ChildContent\", (global::Microsoft.AspNetCore.Components.RenderFragment)((__sel) =>");
        sb.AppendLine("                    {");

        if (f.EnumMembers is not null)
        {
            foreach (var em in f.EnumMembers)
            {
                var memberName = em.Name; // Name w/o qualifier for Value string (ToString() of enum gives member name)
                sb.Append("                        __sel.OpenComponent<global::Lumeo.SelectItem>(").Append(next()).AppendLine(");");
                sb.Append("                        __sel.AddAttribute(").Append(next()).Append(", \"Value\", ").Append(Str(memberName)).AppendLine(");");
                sb.Append("                        __sel.AddAttribute(").Append(next())
                  .Append(", \"ChildContent\", (global::Microsoft.AspNetCore.Components.RenderFragment)((__b) => __b.AddContent(")
                  .Append(next()).Append(", ").Append(Str(SplitPascal(em.Name))).AppendLine(")));");
                sb.AppendLine("                        __sel.CloseComponent();");
            }
        }

        sb.AppendLine("                    }));");
        sb.AppendLine("                    __field.CloseComponent();");
    }

    private static string Str(string s)
    {
        // Verbatim-safe C# string literal.
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32) sb.Append("\\u").Append(((int)c).ToString("X4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
