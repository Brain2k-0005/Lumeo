using System.Runtime.CompilerServices;

// The native charting engine's core (scales, ticks, geometry, LTTB, layout,
// coordinate systems, interaction state) is deliberately `internal` — it is
// implementation surface for chart-type wrappers within THIS package, not a
// public API for direct consumer use (spec §5: consumers touch the typed
// wrapper components' own parameters, not the rendering core). Exposing it to
// the test assembly lets it be unit-tested directly as the pure functions it
// mostly is, per the task's rigor standard, without inflating the package's
// public surface (and its PublicAPI.*.txt tracking burden) for types nobody
// outside this assembly is meant to new up.
[assembly: InternalsVisibleTo("Lumeo.Tests")]
