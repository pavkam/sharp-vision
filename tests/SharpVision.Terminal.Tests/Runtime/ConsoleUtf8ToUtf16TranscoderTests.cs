// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Verifies the UTF-8-to-UTF-16 transcoder <see cref="WindowsConsoleOutputStream"/> uses to make
/// the renderer's raw UTF-8 bytes safe to hand to <c>WriteConsoleW</c>, entirely on the CPU with
/// no console handle - so this runs on every platform, including the one this repository is
/// developed on.
/// </summary>
public sealed class ConsoleUtf8ToUtf16TranscoderTests
{
    /// <summary>Verifies a 4-byte UTF-8 sequence split across two writes still decodes correctly.</summary>
    /// <remarks>
    /// Nothing above <see cref="WindowsConsoleOutputStream"/> guarantees a multi-byte UTF-8
    /// sequence lands entirely within one <c>Write</c> call, so the stateful decoder must carry
    /// the leading bytes of a torn character over to the write supplying its remainder.
    /// </remarks>
    [Fact]
    public void Transcode_WhenFourByteSequenceSplitsAcrossWrites_CombinesAcrossCalls()
    {
        var transcoder = new ConsoleUtf8ToUtf16Transcoder();

        // U+1F600, split after its first two bytes.
        var first = transcoder.Transcode([0xF0, 0x9F], flush: false).ToArray();
        var second = transcoder.Transcode([0x98, 0x80], flush: false).ToArray();

        first.ShouldBeEmpty();
        second.ShouldBe(['\uD83D', '\uDE00']);
    }

    /// <summary>
    /// Verifies an incomplete multi-byte sequence that never receives its remaining bytes is
    /// replaced with U+FFFD once the caller flushes at the end of the output's lifetime.
    /// </summary>
    [Fact]
    public void Transcode_WhenFlushingAnIncompleteSequence_EmitsReplacementCharacter()
    {
        var transcoder = new ConsoleUtf8ToUtf16Transcoder();

        _ = transcoder.Transcode([0xF0, 0x9F], flush: false).ToArray();
        var flushed = transcoder.Transcode([], flush: true).ToArray();

        flushed.ShouldBe(['\uFFFD']);
    }

    /// <summary>Verifies transcoding no bytes produces no code units.</summary>
    [Fact]
    public void Transcode_WhenInputIsEmpty_ProducesNoChars()
    {
        var transcoder = new ConsoleUtf8ToUtf16Transcoder();

        var written = transcoder.Transcode([], flush: false);

        written.IsEmpty.ShouldBeTrue();
    }
}
