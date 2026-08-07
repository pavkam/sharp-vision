// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using System.Text;

using SharpVision.Terminal.Input;

/// <summary>Verifies inbound Kitty OSC 5522 bytes reach the typed clipboard packet sink.
///
/// <para>The transaction, packet parsing, and startup probe each had their own coverage, but the
/// decoder hop between them did not: nothing drove raw OSC 5522 bytes through
/// <c>InputDecoder</c> and asserted a typed packet arrived. Four normative pages meanwhile
/// described this exact hop as unconnected, so the claim went unchallenged in both directions -
/// no test contradicted the docs, and no doc matched the code.</para>
/// </summary>
public sealed class KittyClipboardRoutingTests
{
    /// <summary>Verifies a complete OSC 5522 reply is parsed and delivered, at every byte split, so
    /// the routing holds for a reply that arrives fragmented across reads rather than only for one
    /// that lands whole.</summary>
    [Fact]
    public void Decode_WhenKittyClipboardReplyArrives_DeliversTypedPacketAtEverySplit()
    {
        var sequence = "]5522;type=read:status=OK:id=req-1\\"u8.ToArray();

        for (var split = 0; split <= sequence.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using InputDecoder decoder = new(sink, InputOptions.Default);

            decoder.Decode(sequence.AsSpan(0, split));
            decoder.Decode(sequence.AsSpan(split));
            decoder.Complete();

            sink.KittyClipboardPackets.Count.ShouldBe(1, $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies a payload-carrying reply routes with its data intact, so the hop is not
    /// only recognizing the prefix but carrying the packet body through.</summary>
    [Fact]
    public void Decode_WhenKittyClipboardReplyCarriesData_DeliversThePayload()
    {
        var sink = new RecordingProtocolSink();
        using InputDecoder decoder = new(sink, InputOptions.Default);

        decoder.Decode("]5522;type=read:status=DATA:id=req-1;aGVsbG8=\\"u8);
        decoder.Complete();

        sink.KittyClipboardPackets.Count.ShouldBe(1);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>The counter-case that keeps the assertions above honest: a neighbouring OSC family
    /// must not be routed to the clipboard sink. Matching on a prefix rather than the exact
    /// parameter would swallow OSC 52 and every other numeric family beginning with these digits.
    /// </summary>
    [Theory]
    [InlineData("]52;c;aGVsbG8=\\")]
    [InlineData("]5;1\\")]
    [InlineData("]55220;type=read\\")]
    public void Decode_WhenAnotherOscFamilyArrives_DoesNotReachTheClipboardSink(string sequence)
    {
        var sink = new RecordingProtocolSink();
        using InputDecoder decoder = new(sink, InputOptions.Default);

        decoder.Decode(Encoding.UTF8.GetBytes(sequence));
        decoder.Complete();

        sink.KittyClipboardPackets.ShouldBeEmpty();
    }
}
