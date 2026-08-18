// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Performance;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Multiplexing;
using SharpVision.Terminal.Protocols;


/// <summary>Gates the allocation cost of an active multiplexer route that never receives a
/// wrapped reply.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class ProtocolRouterPerformanceTests
{
    /// <summary>Verifies routing ordinary input through an active multiplexer route never
    /// allocates the bounded candidate buffer when nothing ever starts with the reply prefix —
    /// the buffer is sized to the policy's MaxEnvelopeBytes (1 MiB by default, up to 16 MiB),
    /// so unconditionally allocating it for every active route wastes memory for the common
    /// no-reply case.</summary>
    [Fact]
    public void Route_WhenNoInputEverMatchesTheReplyPrefix_NeverAllocatesTheCandidateBuffer()
    {
        var input = "ordinary text with no escape sequences at all"u8.ToArray();

        var before = GC.GetAllocatedBytesForCurrentThread();

        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries,
            maxDepth: 4,
            maxEnvelopeBytes: 16 * 1024 * 1024);
        var route = new MultiplexerRoute(policy);
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink, route: route);
        router.Route(input);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The 16 MiB candidate buffer would dwarf this budget if allocated during either
        // construction (the reported bug) or this single non-matching MultiplexerRoute call.
        allocated.ShouldBeLessThan(1024L * 1024);
    }

    /// <summary>Verifies ordinary text passing through an active multiplexer route is handed to
    /// the decoder as one batched span instead of one single-byte Decode() call per byte — the
    /// per-byte dispatch used to make every multiplexer-routed keystroke and paste pay for a
    /// full separate decode call, with no accumulation of the pass-through run first.</summary>
    [Fact]
    public void Route_WhenOrdinaryTextPassesThroughAnActiveMultiplexerRoute_CallsDecodeOnce()
    {
        var input = "ordinary text with no escape sequences at all"u8.ToArray();

        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries,
            maxDepth: 4,
            maxEnvelopeBytes: 16 * 1024 * 1024);
        var route = new MultiplexerRoute(policy);
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink, route: route);

        router.Route(input);

        router.DecoderInvocationCount.ShouldBe(1);
        string.Concat(sink.Text.Select(value => value.Value.ToString())).ShouldBe(
            "ordinary text with no escape sequences at all");
    }
}
