// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;





/// <summary>
/// Verifies bounded bracketed-paste matching, ownership, and recovery.
/// </summary>
public sealed class PasteTests
{
    /// <summary>Verifies the small immutable payload wrapper avoids reference allocation.</summary>
    [Fact]
    public void Type_WhenPasteIsInspected_IsValueType() =>
        typeof(Paste).IsValueType.ShouldBeTrue();

    /// <summary>Verifies the valid default wrapper represents an empty paste.</summary>
    [Fact]
    public void Utf8_WhenPasteIsDefault_IsEmpty() =>
        default(Paste).Utf8.IsEmpty.ShouldBeTrue();

    /// <summary>Verifies the wrapper isolates its payload from caller mutation.</summary>
    [Fact]
    public void Constructor_WhenSourceChanges_PreservesCopiedPayload()
    {
        var source = "abc"u8.ToArray();
        var paste = new Paste(source);

        source[0] = (byte) 'z';

        paste.Utf8.ToArray().ShouldBe("abc"u8.ToArray());
    }

    /// <summary>
    /// Verifies empty, multiline, Unicode, and marker-like payload at every split.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("one\ntwo")]
    [InlineData("café 👩")]
    [InlineData("a\u001b[20xb")]
    public void Decode_WhenPasteIsFragmented_PreservesExactPayload(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes($"\u001b[200~{payload}\u001b[201~x");

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            Encoding.UTF8.GetString(sink.Pastes.Single().Utf8.Span)
                .ShouldBe(payload, $"split {split}");
            sink.Text.Single().Value.ShouldBe(new Rune('x'), $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies malformed UTF-8 is normalized to replacement scalars.
    /// </summary>
    [Fact]
    public void Decode_WhenPasteUtf8IsInvalid_EmitsValidOwnedUtf8()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);
        decoder.Decode("\u001b[200~"u8);
        decoder.Decode([0xF0, 0x28, 0x8C, 0x28]);
        decoder.Decode("\u001b[201~"u8);

        Encoding.UTF8.GetString(sink.Pastes.Single().Utf8.Span)
            .ShouldBe("�(�(");
    }

    /// <summary>
    /// Verifies overflow discards through the terminator and recovers once.
    /// </summary>
    [Fact]
    public void Decode_WhenPasteExceedsLimit_DiscardsAndRecovers()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink, new Options { MaxPasteBytes = 3 });

        decoder.Decode("\u001b[200~abcdef\u001b[201~x"u8);
        decoder.Complete();

        sink.Pastes.ShouldBeEmpty();
        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies truncation reports once without publishing partial data.
    /// </summary>
    [Fact]
    public void Complete_WhenPasteIsTruncated_ReportsAndDropsPayload()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode("\u001b[200~payload\u001b[20"u8);
        decoder.Complete();

        sink.Pastes.ShouldBeEmpty();
        sink.Diagnostics.Count.ShouldBe(1);
    }

    /// <summary>
    /// Verifies every proper terminator prefix remains pending at end-of-stream.
    /// </summary>
    [Fact]
    public void Complete_WhenPasteEndsWithTerminatorPrefix_ReportsEveryPrefix()
    {
        var end = "\u001b[201~"u8.ToArray();

        for (var length = 1; length < end.Length; length++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode("\u001b[200~payload"u8);
            decoder.Decode(end.AsSpan(0, length));
            decoder.Complete();

            sink.Pastes.ShouldBeEmpty($"prefix {length}");
            sink.Diagnostics.Count.ShouldBe(1, $"prefix {length}");
        }
    }

    /// <summary>
    /// Verifies a large overflowing paste remains bounded and recovers at its terminator.
    /// </summary>
    [Fact]
    public void Decode_WhenUnterminatedPasteIsLarge_RetainsOnlyConfiguredLimit()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink, new Options { MaxPasteBytes = 16 });
        decoder.Decode("\u001b[200~"u8);
        var chunk = new byte[1024 * 1024];
        chunk.AsSpan().Fill((byte) 'x');

        decoder.Decode(chunk);
        decoder.Decode("\u001b[201~y"u8);
        decoder.Complete();

        sink.Pastes.ShouldBeEmpty();
        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('y'));
    }

    /// <summary>
    /// Verifies a delivered paste remains stable while later paste storage is reused.
    /// </summary>
    [Fact]
    public void Decode_WhenMultiplePastesArrive_PreservesPriorOwnership()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode("\u001b[200~one\u001b[201~\u001b[200~two\u001b[201~"u8);

        Encoding.UTF8.GetString(sink.Pastes[0].Utf8.Span).ShouldBe("one");
        Encoding.UTF8.GetString(sink.Pastes[1].Utf8.Span).ShouldBe("two");
    }
}
