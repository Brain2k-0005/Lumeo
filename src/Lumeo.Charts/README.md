# Lumeo.Charts

30+ chart types for [Lumeo](https://www.nuget.org/packages/Lumeo) — `<Chart>` and its
sub-type components, powered by Apache ECharts. Declarative `EChartOption` bindings or
raw `OptionJson` when you need an ECharts feature Lumeo hasn't wrapped yet.

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Charts
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

```html
<!-- host page (index.html / App.razor) -->
<script src="_content/Lumeo.Charts/js/echarts.min.js"></script>
<script src="_content/Lumeo.Charts/js/chart-interop.js"></script>
```

## Usage

```razor
@using Lumeo

<BarChart Categories="@months" Series="@sales" ShowLegend="true" />

@code {
    private List<string> months = new() { "Jan", "Feb", "Mar" };
    private List<BarChart.ChartSeriesData> sales = new() {
        new() { Name = "Revenue", Values = new() { 120, 150, 90 } },
    };
}
```

Full docs: https://lumeo.nativ.sh/components/chart
