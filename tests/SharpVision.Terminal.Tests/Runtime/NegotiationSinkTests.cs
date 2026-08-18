// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using Kitty.Clipboard;

using SharpVision.Terminal.Clipboard;

/// <summary>
/// Verifies the negotiation interception sink stays transparent to every optional protocol-reply
/// extension a destination sink implements.
/// </summary>
public sealed class NegotiationSinkTests
{
    /// <summary>
    /// Verifies an OSC 52 clipboard reply reaches a destination implementing
    /// <see cref="IClipboardReplySink"/> instead of falling back to the generic unsupported
    /// diagnostic every other optional reply family already avoids through this sink.
    /// </summary>
    [Fact]
    public void Dispatch_WhenDestinationImplementsClipboardReplySink_ForwardsTypedReply()
    {
        var destination = new ClipboardCapableSink();
        var negotiator = new Negotiator(new NegotiationOptions(new Dictionary<string, string?>()));
        IProtocolSink sink = new NegotiationSink(destination, negotiator);
        var reply = new ClipboardReply(ClipboardStatus.Success, Selection.Clipboard, ReadOnlyMemory<byte>.Empty);

        sink.Dispatch(in reply);

        destination.ClipboardReplies.ShouldHaveSingleItem().Status.ShouldBe(ClipboardStatus.Success);
        destination.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a Kitty OSC 5522 clipboard packet reaches a destination implementing
    /// <see cref="IKittyClipboardPacketSink"/> instead of falling back to the generic unsupported
    /// diagnostic every other optional reply family already avoids through this sink.
    /// </summary>
    [Fact]
    public void Dispatch_WhenDestinationImplementsKittyClipboardPacketSink_ForwardsTypedPacket()
    {
        var destination = new ClipboardCapableSink();
        var negotiator = new Negotiator(new NegotiationOptions(new Dictionary<string, string?>()));
        IProtocolSink sink = new NegotiationSink(destination, negotiator);
        var packet = KittyClipboardPacket.Parse("5522;type=read:status=OK"u8);

        sink.Dispatch(packet);

        destination.KittyPackets.ShouldHaveSingleItem().ShouldBeSameAs(packet);
        destination.Diagnostics.ShouldBeEmpty();
    }

    private sealed class ClipboardCapableSink: ISink, IClipboardReplySink, IKittyClipboardPacketSink
    {
        internal List<ClipboardReply> ClipboardReplies { get; } = [];

        internal List<KittyClipboardPacket> KittyPackets { get; } = [];

        internal List<Diagnostic> Diagnostics { get; } = [];

        public void Response(in ClipboardReply value) => ClipboardReplies.Add(value);

        public void Response(KittyClipboardPacket value) => KittyPackets.Add(value);

        public void Input(in Stroke value)
        {
        }

        public void Input(in TerminalText value)
        {
        }

        public void Input(in Pointer value)
        {
        }

        public void Input(Paste value)
        {
        }

        public void Input(in TerminalFocus value)
        {
        }

        public void Input(in Diagnostic value) => Diagnostics.Add(value);

        public void Response(in XtermCapabilitiesResponse value)
        {
        }

        public void Sequence(ProtocolSequence value)
        {
        }

        public void Resize(in Dimensions value)
        {
        }

        public void Closed()
        {
        }

        public void Fault(Exception exception)
        {
        }
    }
}
