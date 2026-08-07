// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Capabilities;

/// <summary>Verifies the reverse mode walk finishes even when the cleanup budget expires part way
/// through it.
///
/// <para>Cleanup unwinds leases in reverse enable order under one shared budget. An ordinary write
/// failure affects only its own lease and the walk continues - that was covered. Budget expiry was
/// not, and behaves differently: the shared token stays cancelled, so every remaining write throws
/// before emitting a byte. The loop still iterated, the catch still swallowed, and nothing
/// reached the terminal.</para>
///
/// <para>The ordering makes that maximally bad rather than incidental. Alternate screen and cursor
/// policy are leased first, so they unwind last - the two restores whose loss a user actually sees
/// are structurally guaranteed to be the first two abandoned. The user is returned to a shell
/// sitting on the alternate screen with a hidden cursor, and nothing downstream re-emits either:
/// this is the only code in the product that writes them.</para>
/// </summary>
public sealed class SessionCleanupBudgetTests
{
    /// <summary>The regression this file exists to pin. A stalled write consumes the whole budget,
    /// and the restores queued behind it must still reach the transport.</summary>
    [Fact]
    public async Task RunAsync_WhenCleanupBudgetExpiresMidWalk_StillRestoresTheAlternateScreenAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = new TerminalOptions
        {
            Capabilities = Supported(),
            CleanupTimeout = TimeSpan.FromMilliseconds(50)
        };
        await using Session session = new(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Armed here so the stall lands on the first cleanup disable, whatever index that is.
        transport.StallNextWrite = true;
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        // The two the user sees. Before the fix the stalled first disable burned the budget and
        // these never reached the transport at all.
        transport.JoinedWrites.EndsWith("[?25h[?1049l", StringComparison.Ordinal)
            .ShouldBeTrue(
                "cursor policy and the alternate screen must be restored even after budget " +
                $"expiry, but the transport saw '{transport.JoinedWrites}'");
    }

    /// <summary>Verifies the whole tail is recovered, not just the last pair - the walk resumes
    /// rather than skipping to the end.</summary>
    [Fact]
    public async Task RunAsync_WhenCleanupBudgetExpiresMidWalk_EmitsEveryRemainingDisableAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = new TerminalOptions
        {
            Capabilities = Supported(),
            CleanupTimeout = TimeSpan.FromMilliseconds(50)
        };
        await using Session session = new(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Armed here so the stall lands on the first cleanup disable, whatever index that is.
        transport.StallNextWrite = true;
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        var written = transport.JoinedWrites;

        // Compared against the same session's own enable sequence rather than a hardcoded list, so
        // this keeps meaning if the lease set changes.
        foreach (var disable in new[] { "[?1049l", "[?25h", "[?1006l", "[?1000l" })
        {
            written.Contains(disable, StringComparison.Ordinal).ShouldBeTrue(
                $"the walk must resume far enough to emit '{disable}', but saw '{written}'");
        }
    }

    /// <summary>The counter-case that keeps the renewal honest: with no stall, one budget covers
    /// the walk and nothing is renewed, so the ordinary path is unchanged.</summary>
    [Fact]
    public async Task RunAsync_WhenCleanupCompletesWithinBudget_ReportsNoCleanupFailureAsync()
    {
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sink = new RuntimeSink();
        var options = new TerminalOptions
        {
            Capabilities = Supported(),
            CleanupTimeout = TimeSpan.FromSeconds(30)
        };
        await using Session session = new(transport, resize, sink, options);
        using var cancellation = new CancellationTokenSource();
        var running = session.RunAsync(cancellation.Token).AsTask();
        await transport.FirstRead.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(running);

        session.LastCleanupException.ShouldBeNull();
        transport.JoinedWrites.ShouldEndWith("[?25h[?1049l");
    }

    private static TerminalCapabilities Supported()
    {
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        return TerminalCapabilities.Conservative with
        {
            FocusReporting = supported,
            BracketedPaste = supported,
            CellMouse = supported,
            KittyKeyboard = supported
        };
    }
}
