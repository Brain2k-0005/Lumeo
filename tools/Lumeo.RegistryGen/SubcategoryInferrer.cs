namespace Lumeo.RegistryGen;

public static class SubcategoryInferrer
{
    private static readonly Dictionary<string, string> FormsMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Input"] = "Inputs", ["Textarea"] = "Inputs", ["NumberInput"] = "Inputs",
        ["OtpInput"] = "Inputs", ["PasswordStrength"] = "Inputs", ["MaskedInput"] = "Inputs",
        ["PinInput"] = "Inputs", ["SearchInput"] = "Inputs", ["PasswordInput"] = "Inputs",
        ["InputMask"] = "Inputs", ["TagInput"] = "Inputs",

        ["Select"] = "Selection", ["Combobox"] = "Selection", ["MultiSelect"] = "Selection",
        ["RadioGroup"] = "Selection", ["CheckboxGroup"] = "Selection", ["ToggleGroup"] = "Selection",
        ["TreeSelect"] = "Selection", ["Cascader"] = "Selection", ["Segmented"] = "Selection",
        ["Mention"] = "Selection",

        ["Button"] = "Buttons & Actions", ["IconButton"] = "Buttons & Actions",
        ["ButtonGroup"] = "Buttons & Actions", ["ToggleButton"] = "Buttons & Actions",
        ["Toggle"] = "Buttons & Actions",

        ["Form"] = "Form Composition", ["FormField"] = "Form Composition",
        ["Label"] = "Form Composition", ["FieldDescription"] = "Form Composition",
        ["FieldError"] = "Form Composition", ["Fieldset"] = "Form Composition",

        ["Slider"] = "Specialized", ["Switch"] = "Specialized", ["Rating"] = "Specialized",
        ["ColorPicker"] = "Specialized", ["DatePicker"] = "Specialized",
        ["TimePicker"] = "Specialized", ["DateTimePicker"] = "Specialized",
        ["RangePicker"] = "Specialized", ["FileUpload"] = "Specialized",
        ["InplaceEditor"] = "Specialized", ["Checkbox"] = "Specialized",
    };

    private static readonly Dictionary<string, string> DataDisplayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Table"] = "Tables", ["DataGrid"] = "Tables", ["DataTable"] = "Tables",

        ["Card"] = "Cards & Layout", ["KpiCard"] = "Cards & Layout", ["StatsCard"] = "Cards & Layout",
        ["Avatar"] = "Cards & Layout", ["AvatarGroup"] = "Cards & Layout",
        ["Badge"] = "Cards & Layout", ["Descriptions"] = "Cards & Layout",

        ["List"] = "Lists & Trees", ["Tree"] = "Lists & Trees", ["Timeline"] = "Lists & Trees",
        ["Steps"] = "Lists & Trees", ["Carousel"] = "Lists & Trees", ["Calendar"] = "Lists & Trees",

        ["Chart"] = "Charts",

        ["Progress"] = "Status & Indicators", ["Skeleton"] = "Status & Indicators",
        ["Spinner"] = "Status & Indicators", ["Chip"] = "Status & Indicators",
        ["Tag"] = "Status & Indicators", ["Delta"] = "Status & Indicators",
        ["Statistic"] = "Status & Indicators",
    };

    public static string? Infer(string componentName, string category)
    {
        return category switch
        {
            "Forms" => FormsMap.TryGetValue(componentName, out var s) ? s : null,
            "Data Display" => DataDisplayMap.TryGetValue(componentName, out var s) ? s : null,
            _ => null,
        };
    }
}
