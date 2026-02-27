namespace Lumeo.Docs.Services;

public class PatternFilterService
{
    public string SelectedCategory { get; private set; } = "All";

    public event Action? OnChange;

    public void SetCategory(string category)
    {
        SelectedCategory = category;
        OnChange?.Invoke();
    }

    public record PatternCategory(string Name, int Count);

    public static readonly List<PatternCategory> Categories =
    [
        new("Accordion", 3),
        new("Alert", 2),
        new("AlertDialog", 2),
        new("AspectRatio", 1),
        new("Avatar", 3),
        new("Badge", 6),
        new("Breadcrumb", 2),
        new("Button", 6),
        new("Calendar", 2),
        new("Card", 3),
        new("Checkbox", 2),
        new("Collapsible", 2),
        new("Combobox", 2),
        new("Command", 2),
        new("ContextMenu", 2),
        new("Dialog", 2),
        new("Drawer", 3),
        new("DropdownMenu", 2),
        new("EmptyState", 2),
        new("FileUpload", 2),
        new("HoverCard", 2),
        new("Input", 3),
        new("Kbd", 2),
        new("Label", 2),
        new("OtpInput", 1),
        new("Pagination", 2),
        new("Popover", 2),
        new("Progress", 2),
        new("RadioGroup", 2),
        new("ScrollArea", 1),
        new("Select", 2),
        new("Separator", 1),
        new("Sheet", 5),
        new("Skeleton", 2),
        new("Slider", 3),
        new("Spinner", 2),
        new("Switch", 2),
        new("Table", 2),
        new("Tabs", 3),
        new("Textarea", 2),
        new("Toast", 6),
        new("Toggle", 2),
        new("ToggleGroup", 2),
        new("Tooltip", 2),
    ];

    public static int TotalCount => Categories.Sum(c => c.Count);
}
