# Lumeo.Gantt

`GanttChart` — a draggable project Gantt chart for [Lumeo](https://www.nuget.org/packages/Lumeo):
task bars, dependencies, milestones, hierarchy and progress tracking. Renders with a
custom Lumeo SVG engine — no third-party charting dependency (no Frappe Gantt at runtime).

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.Gantt --prerelease
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

## Usage

```razor
@using Lumeo

<GanttChart Tasks="_tasks" ViewMode="GanttViewMode.Day" Height="420px" />

@code {
    private readonly DateTime d = DateTime.Today;
    private List<GanttTask> _tasks = new();

    protected override void OnInitialized()
    {
        _tasks = new() {
            new("discovery", "Discovery & Planning", d.AddDays(-10), d.AddDays(-1), Progress: 100),
            new("build", "Build", d.AddDays(-1), d.AddDays(5), Dependencies: new[] { "discovery" }),
        };
    }
}
```

Full docs: https://lumeo.nativ.sh/components/gantt-chart
