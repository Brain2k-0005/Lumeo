# Lumeo.Scheduler

`Scheduler` — a first-party Blazor calendar engine for [Lumeo](https://www.nuget.org/packages/Lumeo):
month, week, day, N-day, agenda, resource and resource-timeline views, recurrence, time
zones and drag-to-reschedule, with no third-party calendar library on the page.

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.Scheduler --prerelease
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

## Usage

```razor
@using Lumeo

<Scheduler @bind-Events="_events" InitialView="SchedulerView.Month" />

@code {
    private IEnumerable<SchedulerEvent> _events = new List<SchedulerEvent> {
        new("standup", "Daily standup", DateTime.Today.AddHours(9), DateTime.Today.AddHours(9.5)),
    };
}
```

Full docs: https://lumeo.nativ.sh/components/scheduler
