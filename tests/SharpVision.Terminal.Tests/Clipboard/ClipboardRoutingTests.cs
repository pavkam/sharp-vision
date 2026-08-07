// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

using Kitty.Clipboard;

/// <summary>
/// Verifies the decoder routes inbound OSC 52 and Kitty OSC 5522 replies to the two new
/// <see cref="IProtocolSink"/> overloads, mirroring the existing Kitty graphics
/// routing test shape.
/// </summary>
public sealed class ClipboardRoutingTests
{
    /// <summary>Verifies a successful OSC 52 reply is claimed and forwarded, not treated as an
    /// unrecognized xterm response or Kitty packet.</summary>
    [Fact]
    public void Route_WhenOsc52ReplyArrives_IsClaimedByClipboardHandler()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route("]52;c;aGVsbG8=\\"u8);

        var reply = sink.ClipboardReplies.ShouldHaveSingleItem();
        reply.Status.ShouldBe(ClipboardStatus.Success);
        reply.Selection.ShouldBe(Selection.Clipboard);
        Encoding.UTF8.GetString(reply.Data.Span).ShouldBe("hello");
        sink.Responses.ShouldBeEmpty();
        sink.KittyClipboardPackets.ShouldBeEmpty();
    }

    /// <summary>Verifies a query-form OSC 52 reply parses at every transport split.</summary>
    [Fact]
    public void Route_WhenOsc52QueryReplyIsFragmented_ProducesSameReplyAtEverySplit()
    {
        var wire = "]52;c;?\\"u8.ToArray();

        for (var split = 0; split <= wire.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(wire.AsSpan(0, split));
            router.Route(wire.AsSpan(split));

            var reply = sink.ClipboardReplies.ShouldHaveSingleItem($"split {split}");
            reply.Status.ShouldBe(ClipboardStatus.Query);
        }
    }

    /// <summary>Verifies an OSC 52 reply with no protocol sink reports Unsupported instead of
    /// falling through to another handler.</summary>
    [Fact]
    public void Route_WhenOsc52ReplyArrivesWithoutProtocolSink_ReportsUnsupported()
    {
        var sink = new RecordingInputSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("]52;c;aGVsbG8=\\"u8);

        sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCode.Unsupported);
    }

    /// <summary>Verifies a valid Kitty OSC 5522 packet is claimed and forwarded, not treated as an
    /// OSC 52 reply.</summary>
    [Fact]
    public void Route_WhenKittyClipboardPacketArrives_IsClaimedByKittyClipboardHandler()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route("]5522;type=read:status=OK:id=req-1\\"u8);

        var packet = sink.KittyClipboardPackets.ShouldHaveSingleItem();
        packet.Valid.ShouldBeTrue();
        packet.Operation.ShouldBe(KittyClipboardOperation.Read);
        packet.ReplyStatus.ShouldBe(ReplyStatus.Ok);
        packet.Id.ShouldBe("req-1");
        sink.ClipboardReplies.ShouldBeEmpty();
        sink.Responses.ShouldBeEmpty();
    }

    /// <summary>Verifies a malformed Kitty OSC 5522 packet still routes to the typed handler as an
    /// invalid packet, rather than being silently dropped.</summary>
    [Fact]
    public void Route_WhenKittyClipboardPacketIsMalformed_ProducesInvalidPacket()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route("]5522;***\\"u8);

        var packet = sink.KittyClipboardPackets.ShouldHaveSingleItem();
        packet.Valid.ShouldBeFalse();
        _ = packet.Diagnostic.ShouldNotBeNull();
    }

    /// <summary>Verifies a Kitty OSC 5522 packet with no protocol sink reports Unsupported instead
    /// of falling through to another handler.</summary>
    [Fact]
    public void Route_WhenKittyClipboardPacketArrivesWithoutProtocolSink_ReportsUnsupported()
    {
        var sink = new RecordingInputSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("]5522;type=read:status=OK\\"u8);

        sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCode.Unsupported);
    }

    /// <summary>
    /// Verifies a malformed reply carrying no matching correlation ID does not disrupt an
    /// unrelated in-flight transaction bound to a different ID.
    /// </summary>
    [Fact]
    public void Route_WhenUnrelatedMalformedPacketArrives_DoesNotDisruptBoundTransaction()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);
        using var transaction = Transaction.Read(id: "bound");

        // A malformed packet with a different, recoverable id is unrelated wire noise.
        router.Route("]5522;type=read:status=EIO:id=unrelated\\"u8);

        var unrelated = sink.KittyClipboardPackets.ShouldHaveSingleItem();
        transaction.Accept(unrelated).ShouldBe(AcceptResult.Ignored);
        transaction.State.ShouldBe(TransactionState.Created);

        router.Route("]5522;type=read:status=OK:id=bound\\"u8);
        var bound = sink.KittyClipboardPackets[1];
        transaction.Accept(bound).ShouldBe(AcceptResult.Accepted);
        transaction.State.ShouldBe(TransactionState.Accepted);
    }
}
