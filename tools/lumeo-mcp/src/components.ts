/**
 * Hand-curated catalog of Lumeo's most-used components.
 *
 * Scope: top ~30 of the ~135 shipping components, focused on the ones
 * LLMs most often need to emit markup for. Each entry carries enough
 * information for an LLM to produce compilable, idiomatic Lumeo Razor
 * without having to inspect the library source.
 */

export interface ComponentParam {
  name: string;
  type: string;
  default: string;
  description: string;
}

export interface ComponentSlot {
  name: string;
  description: string;
}

export interface ComponentDoc {
  name: string;
  category: string;
  description: string;
  params: ComponentParam[];
  slots: ComponentSlot[];
  example: string;
  cssVars: string[];
}

// Shared base parameters every Lumeo component exposes.
const baseParams: ComponentParam[] = [
  { name: "Class", type: "string?", default: "null", description: "Additional Tailwind classes merged onto the root element." },
  { name: "AdditionalAttributes", type: "Dictionary<string, object>?", default: "null", description: "Unmatched HTML attributes forwarded to the root element." },
];

const p = (items: ComponentParam[]): ComponentParam[] => [...items, ...baseParams];

export const components: ComponentDoc[] = [
  // ─────────────────────────── Forms ───────────────────────────
  {
    name: "Button",
    category: "Forms",
    description: "Versatile button with variants, sizes, icons, and loading state.",
    params: p([
      { name: "Variant", type: "Button.ButtonVariant", default: "Default", description: "Default, Destructive, Outline, Secondary, Ghost, Link." },
      { name: "Size", type: "Button.ButtonSize", default: "Default", description: "Default, Sm, Lg, Icon." },
      { name: "Loading", type: "bool", default: "false", description: "Shows spinner and disables the button." },
      { name: "Disabled", type: "bool", default: "false", description: "Disables the button." },
      { name: "Type", type: "string", default: "\"button\"", description: "HTML button type: button, submit, reset." },
      { name: "OnClick", type: "EventCallback<MouseEventArgs>", default: "—", description: "Click handler." },
    ]),
    slots: [
      { name: "ChildContent", description: "Button body (text)." },
      { name: "LeftIcon", description: "Icon rendered before the label." },
      { name: "RightIcon", description: "Icon rendered after the label." },
    ],
    example: `<Button Variant="Button.ButtonVariant.Outline" Size="Button.ButtonSize.Sm" OnClick="HandleClick">
    <LeftIcon><Icon Name="Plus" Size="Icon.IconSize.Sm" /></LeftIcon>
    Create
</Button>`,
    cssVars: ["--color-primary", "--color-primary-foreground", "--color-destructive", "--color-secondary"],
  },
  {
    name: "Input",
    category: "Forms",
    description: "Single-line text input with two-way binding.",
    params: p([
      { name: "Value", type: "string?", default: "null", description: "Current value (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<string?>", default: "—", description: "Raised on input change." },
      { name: "Placeholder", type: "string?", default: "null", description: "Placeholder text." },
      { name: "Type", type: "string", default: "\"text\"", description: "HTML input type." },
      { name: "Disabled", type: "bool", default: "false", description: "Disables the input." },
      { name: "Invalid", type: "bool", default: "false", description: "Renders in error state." },
    ]),
    slots: [
      { name: "Prefix", description: "Content before the input (icon, text)." },
      { name: "Suffix", description: "Content after the input." },
    ],
    example: `<Input @bind-Value="_email" Type="email" Placeholder="you@example.com" />`,
    cssVars: ["--color-input", "--color-ring"],
  },
  {
    name: "PasswordInput",
    category: "Forms",
    description: "Password field with show/hide toggle and optional strength meter.",
    params: p([
      { name: "Value", type: "string?", default: "null", description: "Current value (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<string?>", default: "—", description: "Raised on change." },
      { name: "Placeholder", type: "string?", default: "null", description: "Placeholder text." },
      { name: "ShowStrengthMeter", type: "bool", default: "false", description: "Renders a strength meter below the field." },
    ]),
    slots: [],
    example: `<PasswordInput @bind-Value="_password" ShowStrengthMeter="true" />`,
    cssVars: ["--color-input", "--color-ring"],
  },
  {
    name: "NumberInput",
    category: "Forms",
    description: "Numeric input with step, min, max, and increment/decrement buttons.",
    params: p([
      { name: "Value", type: "decimal?", default: "null", description: "Current value (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<decimal?>", default: "—", description: "Raised on change." },
      { name: "Min", type: "decimal?", default: "null", description: "Minimum allowed value." },
      { name: "Max", type: "decimal?", default: "null", description: "Maximum allowed value." },
      { name: "Step", type: "decimal", default: "1", description: "Increment step." },
    ]),
    slots: [],
    example: `<NumberInput @bind-Value="_quantity" Min="0" Max="100" Step="1" />`,
    cssVars: ["--color-input"],
  },
  {
    name: "Checkbox",
    category: "Forms",
    description: "Boolean checkbox with optional label.",
    params: p([
      { name: "Checked", type: "bool", default: "false", description: "Checked state (two-way bindable as Checked/CheckedChanged)." },
      { name: "CheckedChanged", type: "EventCallback<bool>", default: "—", description: "Raised on toggle." },
      { name: "Disabled", type: "bool", default: "false", description: "Disables the checkbox." },
      { name: "Label", type: "string?", default: "null", description: "Text label next to the checkbox." },
    ]),
    slots: [
      { name: "ChildContent", description: "Custom label content (overrides Label)." },
    ],
    example: `<Checkbox @bind-Checked="_agree" Label="I agree to the terms" />`,
    cssVars: ["--color-primary", "--color-primary-foreground"],
  },
  {
    name: "Switch",
    category: "Forms",
    description: "On/off toggle switch.",
    params: p([
      { name: "Checked", type: "bool", default: "false", description: "On/off state (two-way bindable)." },
      { name: "CheckedChanged", type: "EventCallback<bool>", default: "—", description: "Raised on toggle." },
      { name: "Disabled", type: "bool", default: "false", description: "Disables the switch." },
    ]),
    slots: [],
    example: `<Switch @bind-Checked="_notifications" />`,
    cssVars: ["--color-primary"],
  },
  {
    name: "Select",
    category: "Forms",
    description: "Dropdown select with options.",
    params: p([
      { name: "Value", type: "TValue?", default: "default", description: "Current selection (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<TValue?>", default: "—", description: "Raised on selection." },
      { name: "Placeholder", type: "string?", default: "null", description: "Placeholder when no value selected." },
    ]),
    slots: [
      { name: "ChildContent", description: "One or more <SelectItem Value=\"...\">…</SelectItem> entries." },
    ],
    example: `<Select TValue="string" @bind-Value="_country" Placeholder="Select a country">
    <SelectItem Value="@("us")">United States</SelectItem>
    <SelectItem Value="@("de")">Germany</SelectItem>
    <SelectItem Value="@("fr")">France</SelectItem>
</Select>`,
    cssVars: ["--color-popover", "--color-popover-foreground", "--color-accent"],
  },
  {
    name: "Combobox",
    category: "Forms",
    description: "Searchable dropdown select, supports async data and custom item templates.",
    params: p([
      { name: "Items", type: "IEnumerable<TItem>", default: "[]", description: "Source list." },
      { name: "Value", type: "TItem?", default: "default", description: "Current selection (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<TItem?>", default: "—", description: "Raised on selection." },
      { name: "ItemLabel", type: "Func<TItem, string>", default: "—", description: "Maps item to display label." },
      { name: "Placeholder", type: "string?", default: "null", description: "Placeholder text." },
    ]),
    slots: [
      { name: "ItemTemplate", description: "Custom template per item (RenderFragment<TItem>)." },
    ],
    example: `<Combobox TItem="string" Items="_countries" @bind-Value="_country" ItemLabel="c => c" Placeholder="Search country..." />`,
    cssVars: ["--color-popover", "--color-accent"],
  },
  {
    name: "DatePicker",
    category: "Forms",
    description: "Calendar date picker with popover.",
    params: p([
      { name: "Value", type: "DateOnly?", default: "null", description: "Selected date (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<DateOnly?>", default: "—", description: "Raised on selection." },
      { name: "MinDate", type: "DateOnly?", default: "null", description: "Earliest selectable date." },
      { name: "MaxDate", type: "DateOnly?", default: "null", description: "Latest selectable date." },
      { name: "Placeholder", type: "string?", default: "null", description: "Placeholder text when empty." },
    ]),
    slots: [],
    example: `<DatePicker @bind-Value="_dob" Placeholder="Pick a date" />`,
    cssVars: ["--color-popover", "--color-accent"],
  },
  {
    name: "Slider",
    category: "Forms",
    description: "Range slider for numeric values.",
    params: p([
      { name: "Value", type: "double", default: "0", description: "Current value (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<double>", default: "—", description: "Raised on change." },
      { name: "Min", type: "double", default: "0", description: "Minimum value." },
      { name: "Max", type: "double", default: "100", description: "Maximum value." },
      { name: "Step", type: "double", default: "1", description: "Step size." },
    ]),
    slots: [],
    example: `<Slider @bind-Value="_volume" Min="0" Max="100" Step="5" />`,
    cssVars: ["--color-primary", "--color-muted"],
  },
  {
    name: "Textarea",
    category: "Forms",
    description: "Multi-line text input with optional auto-resize.",
    params: p([
      { name: "Value", type: "string?", default: "null", description: "Current value (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<string?>", default: "—", description: "Raised on input." },
      { name: "Rows", type: "int", default: "3", description: "Visible rows." },
      { name: "AutoResize", type: "bool", default: "false", description: "Grow height to fit content." },
      { name: "Placeholder", type: "string?", default: "null", description: "Placeholder text." },
    ]),
    slots: [],
    example: `<Textarea @bind-Value="_message" Rows="4" Placeholder="Your message..." AutoResize="true" />`,
    cssVars: ["--color-input"],
  },
  {
    name: "Form",
    category: "Forms",
    description: "Form wrapper integrating with EditForm + DataAnnotations validation.",
    params: p([
      { name: "Model", type: "object", default: "—", description: "Bound model instance." },
      { name: "OnValidSubmit", type: "EventCallback<EditContext>", default: "—", description: "Raised when the form validates and submits." },
    ]),
    slots: [
      { name: "ChildContent", description: "FormField entries and submit button." },
    ],
    example: `<Form Model="_model" OnValidSubmit="HandleSubmit">
    <FormField For="() => _model.Email" Label="Email">
        <Input @bind-Value="_model.Email" Type="email" />
    </FormField>
    <Button Type="submit">Save</Button>
</Form>`,
    cssVars: [],
  },
  {
    name: "FormField",
    category: "Forms",
    description: "Labelled form row with validation message binding.",
    params: p([
      { name: "For", type: "Expression<Func<object?>>", default: "—", description: "Lambda selecting the model property." },
      { name: "Label", type: "string?", default: "null", description: "Label text." },
      { name: "HelpText", type: "string?", default: "null", description: "Description beneath the field." },
      { name: "Required", type: "bool", default: "false", description: "Shows required asterisk." },
    ]),
    slots: [
      { name: "ChildContent", description: "The input control (Input, Select, etc.)." },
    ],
    example: `<FormField For="() => _model.Name" Label="Full name" Required="true">
    <Input @bind-Value="_model.Name" />
</FormField>`,
    cssVars: [],
  },

  // ─────────────────────────── Layout ───────────────────────────
  {
    name: "Card",
    category: "Layout",
    description: "Content surface with optional header, body and footer slots.",
    params: p([]),
    slots: [
      { name: "ChildContent", description: "Full card body (use CardHeader/CardContent/CardFooter inside)." },
    ],
    example: `<Card>
    <CardHeader>
        <CardTitle>Team members</CardTitle>
        <CardDescription>Invite your team to collaborate.</CardDescription>
    </CardHeader>
    <CardContent>
        <Lumeo.Text>Content goes here.</Lumeo.Text>
    </CardContent>
    <CardFooter>
        <Button>Invite</Button>
    </CardFooter>
</Card>`,
    cssVars: ["--color-card", "--color-card-foreground", "--color-border"],
  },
  {
    name: "Bento",
    category: "Layout",
    description: "Bento grid — asymmetric feature grid with spannable cells.",
    params: p([
      { name: "Columns", type: "int", default: "3", description: "Base column count on md+ screens." },
      { name: "Gap", type: "int", default: "4", description: "Tailwind gap value (0-12)." },
    ]),
    slots: [
      { name: "ChildContent", description: "One or more <BentoCell> children." },
    ],
    example: `<Bento Columns="3" Gap="4">
    <BentoCell ColSpan="2" RowSpan="2">
        <Heading Level="3">Analytics</Heading>
    </BentoCell>
    <BentoCell>Stat 1</BentoCell>
    <BentoCell>Stat 2</BentoCell>
</Bento>`,
    cssVars: ["--color-card", "--color-border"],
  },
  {
    name: "Splitter",
    category: "Layout",
    description: "Resizable split pane container (horizontal or vertical).",
    params: p([
      { name: "Orientation", type: "Splitter.Direction", default: "Horizontal", description: "Horizontal or Vertical split." },
      { name: "DefaultSize", type: "double", default: "50", description: "Initial first-pane size percent." },
    ]),
    slots: [
      { name: "First", description: "Left/top pane." },
      { name: "Second", description: "Right/bottom pane." },
    ],
    example: `<Splitter Orientation="Splitter.Direction.Horizontal" DefaultSize="30">
    <First><Sidebar /></First>
    <Second><main>Content</main></Second>
</Splitter>`,
    cssVars: ["--color-border"],
  },

  // ─────────────────────────── Overlay ───────────────────────────
  {
    name: "Dialog",
    category: "Overlay",
    description: "Modal dialog with backdrop, focus trap, and scroll lock.",
    params: p([
      { name: "Open", type: "bool", default: "false", description: "Open state (two-way bindable)." },
      { name: "OpenChanged", type: "EventCallback<bool>", default: "—", description: "Raised on open/close." },
    ]),
    slots: [
      { name: "ChildContent", description: "DialogHeader / DialogContent / DialogFooter." },
    ],
    example: `<Dialog @bind-Open="_dialogOpen">
    <DialogHeader>
        <DialogTitle>Are you sure?</DialogTitle>
        <DialogDescription>This cannot be undone.</DialogDescription>
    </DialogHeader>
    <DialogFooter>
        <Button Variant="Button.ButtonVariant.Outline" OnClick='() => _dialogOpen = false'>Cancel</Button>
        <Button Variant="Button.ButtonVariant.Destructive" OnClick="Confirm">Delete</Button>
    </DialogFooter>
</Dialog>`,
    cssVars: ["--color-background", "--color-foreground", "--color-border"],
  },
  {
    name: "Sheet",
    category: "Overlay",
    description: "Slide-in side panel (left, right, top, bottom).",
    params: p([
      { name: "Open", type: "bool", default: "false", description: "Open state (two-way bindable)." },
      { name: "OpenChanged", type: "EventCallback<bool>", default: "—", description: "Raised on open/close." },
      { name: "Side", type: "Sheet.SheetSide", default: "Right", description: "Left, Right, Top, Bottom." },
    ]),
    slots: [
      { name: "ChildContent", description: "SheetHeader / SheetContent / SheetFooter." },
    ],
    example: `<Sheet @bind-Open="_sheetOpen" Side="Sheet.SheetSide.Right">
    <SheetHeader>
        <SheetTitle>Edit profile</SheetTitle>
    </SheetHeader>
    <SheetContent>
        <Input @bind-Value="_name" />
    </SheetContent>
</Sheet>`,
    cssVars: ["--color-background", "--color-border"],
  },
  {
    name: "Popover",
    category: "Overlay",
    description: "Anchored floating panel, click to open/close.",
    params: p([
      { name: "Open", type: "bool", default: "false", description: "Open state (two-way bindable)." },
      { name: "OpenChanged", type: "EventCallback<bool>", default: "—", description: "Raised on toggle." },
      { name: "Placement", type: "string", default: "\"bottom\"", description: "top, right, bottom, left (with -start/-end variants)." },
    ]),
    slots: [
      { name: "TriggerContent", description: "The element that toggles the popover." },
      { name: "ChildContent", description: "Popover body." },
    ],
    example: `<Popover Placement="bottom-start">
    <TriggerContent><Button>Options</Button></TriggerContent>
    <ChildContent>
        <Stack Gap="2" Class="p-3">
            <Lumeo.Text>Hello from the popover.</Lumeo.Text>
        </Stack>
    </ChildContent>
</Popover>`,
    cssVars: ["--color-popover", "--color-popover-foreground"],
  },
  {
    name: "Tooltip",
    category: "Overlay",
    description: "Hover/focus tooltip.",
    params: p([
      { name: "Content", type: "string?", default: "null", description: "Tooltip text (shortcut for Content slot)." },
      { name: "Placement", type: "string", default: "\"top\"", description: "top, right, bottom, left." },
      { name: "Delay", type: "int", default: "300", description: "Open delay in ms." },
    ]),
    slots: [
      { name: "ChildContent", description: "Element that triggers the tooltip." },
      { name: "ContentTemplate", description: "Custom tooltip body (overrides Content)." },
    ],
    example: `<Tooltip Content="Save the document" Placement="top">
    <Button Size="Button.ButtonSize.Icon"><Icon Name="Save" /></Button>
</Tooltip>`,
    cssVars: ["--color-popover", "--color-popover-foreground"],
  },
  {
    name: "Toast",
    category: "Overlay",
    description: "Transient notification. Trigger from C# via ToastService.",
    params: p([]),
    slots: [],
    example: `@inject ToastService Toasts

<Button OnClick='() => Toasts.Show("Saved", variant: ToastVariant.Success)'>Save</Button>

@* Required once in MainLayout: *@
<ToastProvider />`,
    cssVars: ["--color-background", "--color-foreground", "--color-border"],
  },

  // ─────────────────────────── Data ───────────────────────────
  {
    name: "DataGrid",
    category: "Data",
    description: "Feature-rich data grid — sort, filter, paginate, select, group, virtualize, export.",
    params: p([
      { name: "Items", type: "IEnumerable<TItem>", default: "[]", description: "Source data." },
      { name: "Sortable", type: "bool", default: "true", description: "Enable column sorting." },
      { name: "Filterable", type: "bool", default: "false", description: "Show column filters." },
      { name: "PageSize", type: "int?", default: "null", description: "Rows per page (null disables pagination)." },
      { name: "Selectable", type: "bool", default: "false", description: "Render a selection checkbox column." },
    ]),
    slots: [
      { name: "Columns", description: "One or more <DataGridColumn TItem=\"...\"> children." },
    ],
    example: `<DataGrid TItem="User" Items="_users" Sortable="true" PageSize="10">
    <Columns>
        <DataGridColumn TItem="User" Field="@(u => u.Name)" Title="Name" />
        <DataGridColumn TItem="User" Field="@(u => u.Email)" Title="Email" />
        <DataGridColumn TItem="User" Field="@(u => u.Role)" Title="Role" />
    </Columns>
</DataGrid>`,
    cssVars: ["--color-border", "--color-muted", "--color-card"],
  },
  {
    name: "Avatar",
    category: "Data",
    description: "User avatar with image, initials fallback, optional status dot.",
    params: p([
      { name: "Src", type: "string?", default: "null", description: "Image URL." },
      { name: "Alt", type: "string?", default: "null", description: "Alt text." },
      { name: "Fallback", type: "string?", default: "null", description: "Initials fallback (e.g. \"MB\")." },
      { name: "Size", type: "Avatar.AvatarSize", default: "Md", description: "Xs, Sm, Md, Lg, Xl." },
    ]),
    slots: [],
    example: `<Avatar Src="/img/mike.jpg" Fallback="MB" Size="Avatar.AvatarSize.Md" Alt="Mike Berger" />`,
    cssVars: ["--color-muted", "--color-muted-foreground"],
  },
  {
    name: "Badge",
    category: "Data",
    description: "Small status/label pill.",
    params: p([
      { name: "Variant", type: "Badge.BadgeVariant", default: "Default", description: "Default, Secondary, Destructive, Outline, Success, Warning." },
    ]),
    slots: [
      { name: "ChildContent", description: "Badge content." },
      { name: "IconContent", description: "Leading icon." },
    ],
    example: `<Badge Variant="Badge.BadgeVariant.Success">Active</Badge>`,
    cssVars: ["--color-primary", "--color-destructive", "--color-secondary"],
  },
  {
    name: "Table",
    category: "Data",
    description: "Lightweight table primitive (manual rows). For feature-rich data use DataGrid.",
    params: p([]),
    slots: [
      { name: "ChildContent", description: "TableHeader, TableBody, TableRow, TableCell children." },
    ],
    example: `<Table>
    <TableHeader>
        <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Email</TableHead>
        </TableRow>
    </TableHeader>
    <TableBody>
        @foreach (var u in _users)
        {
            <TableRow>
                <TableCell>@u.Name</TableCell>
                <TableCell>@u.Email</TableCell>
            </TableRow>
        }
    </TableBody>
</Table>`,
    cssVars: ["--color-border", "--color-muted"],
  },

  // ─────────────────────────── Navigation ───────────────────────────
  {
    name: "Tabs",
    category: "Navigation",
    description: "Tabbed content switcher.",
    params: p([
      { name: "Value", type: "string?", default: "null", description: "Active tab value (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<string?>", default: "—", description: "Raised on change." },
    ]),
    slots: [
      { name: "ChildContent", description: "TabsList + TabsContent blocks." },
    ],
    example: `<Tabs @bind-Value="_activeTab">
    <TabsList>
        <TabsTrigger Value="@("overview")">Overview</TabsTrigger>
        <TabsTrigger Value="@("settings")">Settings</TabsTrigger>
    </TabsList>
    <TabsContent Value="@("overview")">Overview content</TabsContent>
    <TabsContent Value="@("settings")">Settings content</TabsContent>
</Tabs>`,
    cssVars: ["--color-muted", "--color-background"],
  },
  {
    name: "Sidebar",
    category: "Navigation",
    description: "App sidebar with collapsible menu groups, items and icons.",
    params: p([
      { name: "Collapsed", type: "bool", default: "false", description: "Collapsed state (two-way bindable)." },
      { name: "CollapsedChanged", type: "EventCallback<bool>", default: "—", description: "Raised on toggle." },
    ]),
    slots: [
      { name: "ChildContent", description: "SidebarHeader, SidebarContent (with SidebarMenu/SidebarMenuButton), SidebarFooter." },
    ],
    example: `<Sidebar @bind-Collapsed="_collapsed">
    <SidebarHeader><Heading Level="4">Acme</Heading></SidebarHeader>
    <SidebarContent>
        <SidebarMenu>
            <SidebarMenuButton Href="/dashboard">
                <IconContent><Icon Name="LayoutDashboard" /></IconContent>
                Dashboard
            </SidebarMenuButton>
        </SidebarMenu>
    </SidebarContent>
</Sidebar>`,
    cssVars: ["--color-sidebar", "--color-sidebar-foreground", "--color-sidebar-accent"],
  },
  {
    name: "Breadcrumb",
    category: "Navigation",
    description: "Hierarchical navigation trail.",
    params: p([]),
    slots: [
      { name: "ChildContent", description: "BreadcrumbItem / BreadcrumbSeparator entries." },
    ],
    example: `<Breadcrumb>
    <BreadcrumbItem Href="/">Home</BreadcrumbItem>
    <BreadcrumbSeparator />
    <BreadcrumbItem Href="/settings">Settings</BreadcrumbItem>
    <BreadcrumbSeparator />
    <BreadcrumbItem>Profile</BreadcrumbItem>
</Breadcrumb>`,
    cssVars: ["--color-muted-foreground", "--color-foreground"],
  },
  {
    name: "Pagination",
    category: "Navigation",
    description: "Page navigation control.",
    params: p([
      { name: "Page", type: "int", default: "1", description: "Current page (1-based, two-way bindable)." },
      { name: "PageChanged", type: "EventCallback<int>", default: "—", description: "Raised on page change." },
      { name: "TotalPages", type: "int", default: "1", description: "Total page count." },
      { name: "SiblingCount", type: "int", default: "1", description: "Pages shown around the current one." },
    ]),
    slots: [],
    example: `<Pagination @bind-Page="_page" TotalPages="10" />`,
    cssVars: ["--color-border", "--color-accent"],
  },
  {
    name: "BottomNav",
    category: "Navigation",
    description: "Mobile bottom navigation bar.",
    params: p([
      { name: "Value", type: "string?", default: "null", description: "Active item value (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<string?>", default: "—", description: "Raised on selection." },
    ]),
    slots: [
      { name: "ChildContent", description: "One or more BottomNavItem children." },
    ],
    example: `<BottomNav @bind-Value="_tab">
    <BottomNavItem Value="@("home")"><Icon Name="Home" />Home</BottomNavItem>
    <BottomNavItem Value="@("search")"><Icon Name="Search" />Search</BottomNavItem>
    <BottomNavItem Value="@("profile")"><Icon Name="User" />Profile</BottomNavItem>
</BottomNav>`,
    cssVars: ["--color-background", "--color-border", "--color-primary"],
  },

  // ─────────────────────────── AI ───────────────────────────
  {
    name: "PromptInput",
    category: "AI",
    description: "Chat/prompt input — auto-resizing textarea with send button and attachments.",
    params: p([
      { name: "Value", type: "string?", default: "null", description: "Draft prompt (two-way bindable)." },
      { name: "ValueChanged", type: "EventCallback<string?>", default: "—", description: "Raised on change." },
      { name: "OnSubmit", type: "EventCallback<string>", default: "—", description: "Raised on send (Enter or button)." },
      { name: "Placeholder", type: "string?", default: "\"Ask anything...\"", description: "Placeholder text." },
      { name: "Disabled", type: "bool", default: "false", description: "Disable send while streaming." },
    ]),
    slots: [
      { name: "Actions", description: "Extra buttons (attach, model picker) beside send." },
    ],
    example: `<PromptInput @bind-Value="_draft" OnSubmit="HandleSend" Disabled="_isStreaming" />`,
    cssVars: ["--color-input", "--color-ring", "--color-primary"],
  },
  {
    name: "StreamingText",
    category: "AI",
    description: "Renders a token stream with cursor, supports markdown and auto-scroll.",
    params: p([
      { name: "Text", type: "string", default: "\"\"", description: "Current accumulated text." },
      { name: "IsStreaming", type: "bool", default: "false", description: "Show blinking cursor while true." },
      { name: "Markdown", type: "bool", default: "true", description: "Render as markdown." },
    ]),
    slots: [],
    example: `<StreamingText Text="@_assistantMessage" IsStreaming="_isStreaming" Markdown="true" />`,
    cssVars: ["--color-foreground", "--color-muted-foreground"],
  },
  {
    name: "AgentMessage",
    category: "AI",
    description: "Single chat message bubble — user or assistant.",
    params: p([
      { name: "Role", type: "AgentMessage.MessageRole", default: "Assistant", description: "User, Assistant, System." },
      { name: "Avatar", type: "string?", default: "null", description: "Avatar URL." },
      { name: "Name", type: "string?", default: "null", description: "Display name." },
    ]),
    slots: [
      { name: "ChildContent", description: "Message body (markdown, tool calls, etc.)." },
      { name: "Actions", description: "Per-message actions (copy, regenerate)." },
    ],
    example: `<AgentMessage Role="AgentMessage.MessageRole.Assistant" Name="Claude">
    <StreamingText Text="@_message" IsStreaming="false" />
</AgentMessage>`,
    cssVars: ["--color-card", "--color-muted", "--color-border"],
  },

  // ─────────────────────────── Motion ───────────────────────────
  {
    name: "Marquee",
    category: "Motion",
    description: "Auto-scrolling horizontal or vertical marquee.",
    params: p([
      { name: "Direction", type: "Marquee.MarqueeDirection", default: "Left", description: "Left, Right, Up, Down." },
      { name: "Speed", type: "double", default: "40", description: "Pixels per second." },
      { name: "PauseOnHover", type: "bool", default: "true", description: "Pause animation on hover." },
    ]),
    slots: [
      { name: "ChildContent", description: "Items to scroll." },
    ],
    example: `<Marquee Direction="Marquee.MarqueeDirection.Left" Speed="40" PauseOnHover="true">
    <Badge>React</Badge>
    <Badge>Blazor</Badge>
    <Badge>Vue</Badge>
    <Badge>Svelte</Badge>
</Marquee>`,
    cssVars: [],
  },
  {
    name: "NumberTicker",
    category: "Motion",
    description: "Animated numeric counter that counts up/down to a value.",
    params: p([
      { name: "Value", type: "double", default: "0", description: "Target value." },
      { name: "Duration", type: "int", default: "1500", description: "Animation duration in ms." },
      { name: "Decimals", type: "int", default: "0", description: "Decimal places to display." },
      { name: "Prefix", type: "string?", default: "null", description: "Text before the number (e.g. \"$\")." },
      { name: "Suffix", type: "string?", default: "null", description: "Text after the number (e.g. \"%\")." },
    ]),
    slots: [],
    example: `<NumberTicker Value="12450" Duration="2000" Prefix="$" />`,
    cssVars: ["--color-foreground"],
  },
];

export const CATEGORIES: string[] = Array.from(new Set(components.map(c => c.category))).sort();
