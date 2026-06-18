using Xunit;

namespace Lumeo.Tests;

/// <summary>
/// Tests that subscribe to the PROCESS-WIDE <see cref="System.Threading.Tasks.TaskScheduler.UnobservedTaskException"/>
/// event must not run alongside other tests (or each other): a parallel test's
/// faulted fire-and-forget task raises that global event inside their assertion
/// window and flakes them (Assert.NotSame "values are the same instance"). Members
/// of this collection run serially and, via <c>DisableParallelization</c>, never
/// concurrently with any other collection — so no foreign unobserved exception can
/// pollute their observation window. They also call <c>GC.Collect()</c>, which is
/// likewise best kept out of the parallel pool.
/// </summary>
[CollectionDefinition("UnobservedTaskException", DisableParallelization = true)]
public sealed class UnobservedTaskExceptionCollection { }
