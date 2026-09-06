// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Verifies the UTF-16-to-UTF-8 transcoder <see cref="WindowsConsoleInputStream"/> uses to make
/// <c>ReadConsoleW</c> output safe to hand back as UTF-8, entirely on the CPU with no console
/// handle - so this runs on every platform, including the one this repository is developed on.
/// </summary>
public sealed class ConsoleUtf16ToUtf8TranscoderTests
{
    /// <summary>Verifies a surrogate pair split across two native reads still decodes correctly.</summary>
    /// <remarks>
    /// A native <c>ReadConsoleW</c> call can return with a lone high surrogate as its last code
    /// unit when the matching low surrogate has not arrived yet; the stateful encoder must carry
    /// that surrogate over to the call supplying the rest of the pair instead of replacing it or
    /// throwing.
    /// </remarks>
    [Fact]
    public void Transcode_WhenSurrogatePairSplitsAcrossReads_CombinesAcrossCalls()
    {
        var transcoder = new ConsoleUtf16ToUtf8Transcoder();
        Span<byte> destination = stackalloc byte[16];

        // U+10000, split as a lone high surrogate ending the first native read.
        var firstWritten = transcoder.Transcode(['a', '\uD800'], flush: false, destination);
        var first = destination[..firstWritten].ToArray();

        var secondWritten = transcoder.Transcode(['\uDC00'], flush: false, destination);
        var second = destination[..secondWritten].ToArray();

        first.ShouldBe([(byte) 'a']);
        second.ShouldBe([0xF0, 0x90, 0x80, 0x80]);
    }

    /// <summary>
    /// Verifies a caller buffer smaller than one transcoded character drains the remainder across
    /// repeated calls instead of dropping or duplicating bytes.
    /// </summary>
    /// <remarks>
    /// Interactive console reads use buffers as small as one byte, far smaller than a 3-byte
    /// UTF-8 sequence such as U+20AC.
    /// </remarks>
    [Fact]
    public void Transcode_WhenDestinationSmallerThanEncodedChar_DrainsAcrossRepeatedCalls()
    {
        var transcoder = new ConsoleUtf16ToUtf8Transcoder();
        Span<byte> oneByte = stackalloc byte[1];
        List<byte> collected = [];

        var firstWritten = transcoder.Transcode(['€'], flush: false, oneByte);
        collected.Add(oneByte[0]);
        transcoder.HasPendingOutput.ShouldBeTrue();

        var secondWritten = transcoder.DrainPending(oneByte);
        collected.Add(oneByte[0]);
        transcoder.HasPendingOutput.ShouldBeTrue();

        var thirdWritten = transcoder.DrainPending(oneByte);
        collected.Add(oneByte[0]);

        firstWritten.ShouldBe(1);
        secondWritten.ShouldBe(1);
        thirdWritten.ShouldBe(1);
        transcoder.HasPendingOutput.ShouldBeFalse();
        collected.ShouldBe([0xE2, 0x82, 0xAC]);
    }

    /// <summary>
    /// Verifies an unpaired high surrogate that never receives its low surrogate is replaced with
    /// U+FFFD once the caller flushes at the end of the input's lifetime.
    /// </summary>
    [Fact]
    public void Transcode_WhenFlushingAnUnpairedSurrogate_EmitsReplacementCharacter()
    {
        var transcoder = new ConsoleUtf16ToUtf8Transcoder();
        Span<byte> destination = stackalloc byte[16];

        var written = transcoder.Transcode(['\uD800'], flush: true, destination);

        destination[..written].ToArray().ShouldBe([0xEF, 0xBF, 0xBD]);
    }

    /// <summary>Verifies transcoding no code units produces no bytes and no pending state.</summary>
    [Fact]
    public void Transcode_WhenInputIsEmpty_ProducesNoBytes()
    {
        var transcoder = new ConsoleUtf16ToUtf8Transcoder();
        Span<byte> destination = stackalloc byte[16];

        var written = transcoder.Transcode([], flush: false, destination);

        written.ShouldBe(0);
        transcoder.HasPendingOutput.ShouldBeFalse();
    }
}
