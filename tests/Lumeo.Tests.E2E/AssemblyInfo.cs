using Xunit;

// Gate-stability fix: this assembly has no xunit.runner.json, so xUnit v2 fell back
// to its own default — parallelize test COLLECTIONS with maxParallelThreads =
// Environment.ProcessorCount. Every test class in this project is its own implicit
// collection except the Gantt specs (which already share one explicit sequential
// collection, GanttSequentialCollection in Gantt/GanttParityTestBase.cs). That left
// ~20 independent collections eligible to run concurrently, and PlaywrightTestBase
// launches a brand-new headless Chromium PROCESS per [Fact]/[Theory]
// (IAsyncLifetime.InitializeAsync runs per test-class-instance, and xUnit constructs
// a fresh instance per test method) — so on GitHub's own documented public-repo
// ubuntu-latest spec (4 vCPU / 16GB), "default parallelism" meant up to 4 collections
// each spinning up their own multi-process Chromium at once, all fighting the same
// 4 cores.
//
// That is the resource starvation seven master-run failures traced back to: a
// different Gantt test timed out or mis-measured (e.g. GanttV3StickyHeaderTests'
// horizontal-scroll assertion reading a stale position because the page's own JS
// auto-center callback lost a race against the test's synchronous reset once the
// runner's CPU was oversubscribed) on almost every run, never the same one twice —
// the signature of scheduling contention, not a shared logic bug. The Gantt
// collection's own internal DisableParallelization=true only serializes Gantt specs
// against EACH OTHER; it does nothing to stop the ~20 OTHER collections (Smokes/,
// Scheduler/, Visual/) from running alongside it and starving its CPU budget. Log
// timestamps from a captured failure confirm this directly: Smokes/ and Scheduler/
// tests were interleaving with Gantt output in the exact window the Gantt assertion
// failed.
//
// Fix: fully serialize the assembly (one test collection executing at a time)
// instead of leaving it at ProcessorCount. This was validated against two weaker
// options first, both of which turned out insufficient:
//
//   - MaxParallelThreads=2 (halving the default): in a CPU-capped Docker repro of
//     the actual CI runner (4 vCPU / 16GB, matching GitHub's public-repo hosted-runner
//     spec), it took the flake rate from 2-of-5 repeated runs (unpatched default) to
//     0-of-5 — but under ADDITIONAL induced background CPU load (a 2-core busy-loop
//     container running alongside the same 4-vCPU test container, simulating a
//     noisier host than an idle repro), it still flaked 1-of-3, on the exact same
//     test with the exact same error signature: "expected the header to shift left
//     by ~300px in lockstep with the row canvas, got a delta of -5633" — a
//     byte-for-byte match to a real master CI failure.
//   - Full serialization (this fix, MaxParallelThreads=1): under the identical
//     induced-load repro, 3-of-3 clean.
//
// Cost: wall time for the filtered (non-Visual) suite rose from a ~150-175s baseline
// (default parallelism) to ~250-255s fully serialized — roughly +65-70%, not the
// mild ~10-15% MaxParallelThreads=2 cost. That is a real trade-off, not a rounding
// error, but the workflow's own timeout budget (25 minutes for the whole job,
// including two full solution builds and browser install) comfortably absorbs it,
// and "the gate is trustworthy" was worth more here than "the gate is a little
// faster."
[assembly: CollectionBehavior(MaxParallelThreads = 1)]
