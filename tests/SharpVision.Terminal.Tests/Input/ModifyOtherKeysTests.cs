// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

/// <summary>Verifies xterm enhanced-key commands and input decoding.</summary>
public sealed class ModifyOtherKeysTests
{
    /// <summary>Verifies query, set, and initial-value restore use official bytes.</summary>
    [Fact]
    public void Commands_WhenCalled_WriteExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new Writer(destination);

        XtermModifyOtherKeys.Query(writer);
        XtermModifyOtherKeys.Set(writer, 2);
        XtermModifyOtherKeys.Restore(writer);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[?4m\u001b[>4;2m\u001b[>4m"u8.ToArray());
    }

    /// <summary>Verifies legacy and CSI-u compatible forms preserve scalar and modifiers.</summary>
    [Theory]
    [InlineData("\u001b[27;3;120~")]
    [InlineData("\u001b[120;3u")]
    public void Decode_WhenEnhancedCharacterArrives_EmitsTypedStroke(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var decoder = new InputDecoder(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));

            var stroke = sink.Strokes.ShouldHaveSingleItem($"split {split}");
            stroke.Code.ShouldBe(Code.Character, $"split {split}");
            stroke.Character.ShouldBe(new Rune('x'), $"split {split}");
            stroke.Modifiers.ShouldBe(Modifiers.Alt, $"split {split}");
        }
    }

    /// <summary>Verifies malformed enhanced input recovers a following ordinary key.</summary>
    [Fact]
    public void Decode_WhenEnhancedKeyIsMalformed_ReportsAndRecovers()
    {
        var bytes = "\u001b[27;99;120~z"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var decoder = new InputDecoder(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));

            sink.Diagnostics.ShouldHaveSingleItem($"split {split}").Code.ShouldBe(
                DiagnosticCode.Malformed,
                $"split {split}");
            sink.Text[^1].Value.ShouldBe(new Rune('z'), $"split {split}");
        }
    }

    /// <summary>Verifies xterm's query reply is routed as protocol state rather than input.</summary>
    [Fact]
    public void Decode_WhenQueryReplyArrives_EmitsTypedResponse()
    {
        var sink = new RecordingProtocolSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("\u001b[>4;2m"u8);

        var response = sink.Responses.ShouldHaveSingleItem();
        response.Kind.ShouldBe(ResponseKind.ModifyOtherKeys);
        response.Values.ToArray().ShouldBe([4, 2]);
        sink.Strokes.ShouldBeEmpty();
    }

    /// <summary>Verifies Kitty event subparameters retain precedence over compatible CSI-u.</summary>
    [Fact]
    public void Decode_WhenKittyAndCompatibleCsiUOverlap_PreservesKittyActionAtEverySplit()
    {
        var bytes = "\u001b[120;3:2u"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var decoder = new InputDecoder(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));

            var stroke = sink.Strokes.ShouldHaveSingleItem($"split {split}");
            stroke.Character.ShouldBe(new Rune('x'), $"split {split}");
            stroke.Modifiers.ShouldBe(Modifiers.Alt, $"split {split}");
            stroke.Action.ShouldBe(KeyAction.Repeat, $"split {split}");
        }
    }
}
