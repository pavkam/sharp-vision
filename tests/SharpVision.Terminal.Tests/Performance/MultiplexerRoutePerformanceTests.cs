// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Performance;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Multiplexing;
using SharpVision.Terminal.Protocols;


/// <summary>Gates the warmed allocation cost of nested-tmux graphics passthrough.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class MultiplexerRoutePerformanceTests
{
    /// <summary>Verifies warmed nested-tmux graphics passthrough stays allocation-flat instead of
    /// scaling with nesting depth and frame size — TryWritePassthrough used to copy the frame into
    /// a fresh heap array once per layer plus a starting and ending copy; it now ping-pongs two
    /// pooled buffers sized to the route's own MaxEnvelopeBytes bound, so warmed steady-state cost
    /// is dominated by the caller's own destination writer rather than layer count.
    /// </summary>
    [Fact]
    public void TryWriteGraphics_WhenNestedFourDeepAndWarmed_StaysAllocationFlatAcrossFrames()
    {
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux, MultiplexerKind.Tmux, MultiplexerKind.Tmux, MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics,
            maxDepth: 4,
            maxEnvelopeBytes: 1024 * 1024);
        var route = new MultiplexerRoute(policy);

        var commands = new byte[2 + 4096 + 2];
        commands[0] = ControlBytes.Escape;
        commands[1] = (byte) '_';
        commands.AsSpan(2, 4096).Fill((byte) 'A');
        commands[^2] = ControlBytes.Escape;
        commands[^1] = (byte) '\\';

        // Warm the ArrayPool buckets and the JIT before measuring.
        for (var index = 0; index < 8; index++)
        {
            var warmup = new ArrayBufferWriter<byte>();
            route.TryWriteGraphics(warmup, commands).ShouldBeTrue();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 100; index++)
        {
            var destination = new ArrayBufferWriter<byte>();
            route.TryWriteGraphics(destination, commands).ShouldBeTrue();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Unfixed, this frame at this depth cost roughly 37,600 B per call (see the issue's own
        // measurements at a comparable size and depth) — 100 calls would be well over 3 MB. The
        // pooled ping-pong path keeps 100 warmed calls far below that.
        allocated.ShouldBeLessThan(700_000);
    }
}
