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

    /// <summary>
    /// Verifies a response the negotiator classifies as a duplicate reaches the destination as a
    /// <see cref="Diagnostic"/> through <see cref="IInputSink.Input(in Diagnostic)"/>. Before this
    /// forwarding existed, every <c>Response</c> override discarded
    /// <see cref="Negotiator.Accept(in XtermCapabilitiesResponse)"/>'s result and never consulted
    /// <see cref="Negotiator.LastDiagnostic"/>, so the entire duplicate/late classification family
    /// never reached any destination.
    /// </summary>
    [Fact]
    public void Response_WhenNegotiatorClassifiesDuplicateReply_ForwardsClassificationDiagnostic()
    {
        var destination = new ClipboardCapableSink();
        var negotiator = new Negotiator(new NegotiationOptions(new Dictionary<string, string?>()));
        negotiator.Start(new ArrayBufferWriter<byte>());
        IProtocolSink sink = new NegotiationSink(destination, negotiator);
        var synchronizedOutput = Response("?2026;1"u8, "$"u8, (byte) 'y');

        sink.Response(in synchronizedOutput);
        sink.Response(in synchronizedOutput);

        destination.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCode.DuplicateResponse);
    }

    private static XtermCapabilitiesResponse Response(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final)
    {
        XtermResponses.TryCsi(parameters, intermediates, final, out var response).ShouldBeTrue();
        return response;
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
