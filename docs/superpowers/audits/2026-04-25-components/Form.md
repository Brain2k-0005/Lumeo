# Form

**Path:** `src/Lumeo/UI/Form/`
**Class:** Other (form container)
**Files:** DataAnnotationsFormValidator.cs, Form.razor, FormContext.cs, FormDescription.razor, FormField.razor, FormItem.razor, FormLabel.razor, FormMessage.razor, IFormValidator.cs

## Contract — OK
- All `.razor` files: `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root.
- `.cs` files: in `Lumeo` namespace.
- No raw colors, no `dark:` prefixes.
- No icons.

## API — OK
- Other/container class; judgement-based.
- Form: `Model`, `ChildContent`, `OnValidSubmit`, `OnInvalidSubmit`, `Validator`, `Class`, `AdditionalAttributes`.
- FormField: `Label`, `HelpText`, `Error`, `Required`, `Orientation`, `LabelWidth`, `Name`, `ChildContent`, `Class`, `AdditionalAttributes`.
- FormLabel, FormDescription, FormMessage, FormItem all present with appropriate minimal params.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/FormPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`form`)
- Files declared: 9 of 9
- Missing from registry: none
- Component deps declared: OK (label listed)
