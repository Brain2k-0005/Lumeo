using Xunit;
using Lumeo.Services;

namespace Lumeo.Tests.Services;

/// <summary>
/// Round-3 P2 — additive-evolution guard for <see cref="IKeyboardShortcutService"/>.
///
/// Round-2 added <c>allowInEditable</c> directly onto the existing abstract members,
/// changing their CLR signature and breaking every external implementor / test double
/// that had implemented the original 3-parameter shape. The fix restores the 3-parameter
/// members as the abstract contract and exposes the <c>allowInEditable</c> variant as
/// additive default interface members (mirroring IComponentInteropService).
///
/// This test pins that contract: a "legacy" implementor that only knows the original
/// 3-parameter members must (a) still satisfy the interface — if the 4-parameter shape
/// were abstract this file would not compile — and (b) transparently answer the
/// 4-parameter overload via the default interface member, which delegates to the
/// 3-parameter member (i.e. behaves as <c>allowInEditable: false</c>).
/// </summary>
public class KeyboardShortcutServiceInterfaceCompatTests
{
    // Implements ONLY the original 3-parameter members — no knowledge of allowInEditable.
    private sealed class LegacyThreeParamImpl : IKeyboardShortcutService
    {
        public int Registrations;

        public ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Func<Task> handler, bool preventDefault = true)
        {
            Registrations++;
            return ValueTask.FromResult<IAsyncDisposable>(new Noop());
        }

        public ValueTask<IAsyncDisposable> RegisterAsync(string keyCombo, Action handler, bool preventDefault = true)
        {
            Registrations++;
            return ValueTask.FromResult<IAsyncDisposable>(new Noop());
        }

        public ValueTask UnregisterAsync(string id) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class Noop : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Legacy_ThreeParam_Implementor_Satisfies_Interface_And_Routes_FourParam_Through_The_DIM()
    {
        IKeyboardShortcutService svc = new LegacyThreeParamImpl();
        var legacy = (LegacyThreeParamImpl)svc;

        // Original 3-parameter calls (unchanged public surface).
        await svc.RegisterAsync("ctrl+k", () => Task.CompletedTask);
        await svc.RegisterAsync("escape", () => { });

        // The additive 4-parameter overloads are DEFAULT interface members. A legacy
        // implementor that never implemented them still answers the call — the default
        // body delegates to its 3-parameter member (the allowInEditable flag is dropped).
        await svc.RegisterAsync("ctrl+b", () => Task.CompletedTask, preventDefault: true, allowInEditable: false);
        await svc.RegisterAsync("ctrl+p", () => { }, preventDefault: true, allowInEditable: true);

        // All four calls landed on the two 3-parameter members via the DIM delegation.
        Assert.Equal(4, legacy.Registrations);
    }
}
