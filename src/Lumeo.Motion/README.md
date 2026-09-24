# Lumeo.Motion

Motion primitives for [Lumeo](https://www.nuget.org/packages/Lumeo) —
`AnimatedBeam`, `BlurFade`, `BorderBeam`, `Confetti`, `Dock`, `Marquee`, `NumberTicker`,
`ShimmerButton`, `Sparkles`, `TextReveal`. Magic UI / Aceternity-style components for
high-energy Blazor interfaces. Most are pure CSS; a few use a small JS interop module.

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.Motion --prerelease
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

```html
<!-- host page (index.html / App.razor) -->
<script src="_content/Lumeo.Motion/js/motion-interop.js"></script>
```

## Usage

```razor
@using Lumeo

<Marquee PauseOnHover="true">
    <div class="mx-6 text-sm font-medium text-muted-foreground">Acme Corp</div>
    <div class="mx-6 text-sm font-medium text-muted-foreground">Globex</div>
</Marquee>
```

Full docs: https://lumeo.nativ.sh/components/marquee
