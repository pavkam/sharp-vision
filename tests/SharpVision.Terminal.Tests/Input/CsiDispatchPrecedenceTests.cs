// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

/// <summary>
/// Verifies CSI handler precedence is preserved by the data-driven dispatch list, for the one
/// final byte more than one handler inspects: <c>u</c> (see #97 step 6).
/// </summary>
public sealed class CsiDispatchPrecedenceTests
{
    /// <summary>Verifies a Kitty enhancement-flags query reply (<c>CSI ? &lt;flags&gt; u</c>) is
    /// claimed by the xterm response handler ahead of the Kitty keyboard handler, when a protocol
    /// sink is present to receive it.</summary>
    [Fact]
    public void Decode_WhenQueryReplyArrivesWithProtocolSink_IsClaimedByXtermResponseNotKitty()
    {
        var sink = new RecordingProtocolSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("[?5u"u8.ToArray());

        sink.MetricsResponses.ShouldBeEmpty();
        var response = sink.Responses.ShouldHaveSingleItem();
        response.Kind.ShouldBe(ResponseKind.Keyboard);
        sink.Strokes.ShouldBeEmpty();
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Verifies the same query reply, with no protocol sink to receive it, is still
    /// claimed by the xterm response handler (reporting Unsupported) rather than falling through
    /// to the Kitty keyboard handler and being misdecoded as a key event.</summary>
    [Fact]
    public void Decode_WhenQueryReplyArrivesWithoutProtocolSink_ReportsUnsupportedNotAKittyStroke()
    {
        var sink = new RecordingInputSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("[?5u"u8.ToArray());

        sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCode.Unsupported);
        sink.Strokes.ShouldBeEmpty();
    }

    /// <summary>Verifies a Kitty keyboard event report (<c>CSI &lt;code&gt;u</c>, no private
    /// marker) is decoded as a stroke rather than claimed by the xterm response handler, since
    /// TryCsi's own 'u' case requires the '?' marker the xterm response only matches on.</summary>
    [Fact]
    public void Decode_WhenKittyEventArrivesWithNoMarker_FallsThroughToKittyHandler()
    {
        var sink = new RecordingProtocolSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("[97u"u8.ToArray());

        sink.Responses.ShouldBeEmpty();
        var stroke = sink.Strokes.ShouldHaveSingleItem();
        stroke.Code.ShouldBe(Code.Character);
        stroke.Character.ShouldBe(new Rune('a'));
    }
}
