// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

/// <summary>Verifies the UTF-8 rune accumulator extracted from InputDecoder.</summary>
public sealed class Utf8TextAccumulatorTests
{
    /// <summary>Verifies a multi-byte sequence split across two calls decodes to one rune.</summary>
    [Fact]
    public void Process_WhenAMultiByteSequenceSpansTwoCalls_EmitsOneRune()
    {
        var emitted = new List<Rune>();
        var accumulator = new Utf8TextAccumulator(emitted.Add);
        var bytes = "é"u8.ToArray();
        bytes.Length.ShouldBe(2);

        accumulator.Process(bytes.AsSpan(0, 1));
        accumulator.HasPending.ShouldBeTrue();
        emitted.ShouldBeEmpty();

        accumulator.Process(bytes.AsSpan(1, 1));

        accumulator.HasPending.ShouldBeFalse();
        emitted.ShouldBe([new Rune('é')]);
    }

    /// <summary>Verifies an invalid leading byte emits one replacement character and does not
    /// desynchronize decoding of the bytes that follow it.</summary>
    [Fact]
    public void Process_WhenAByteIsInvalidUtf8_EmitsReplacementAndContinuesDecoding()
    {
        var emitted = new List<Rune>();
        var accumulator = new Utf8TextAccumulator(emitted.Add);

        accumulator.Process([0xff, (byte) 'a']);

        emitted.ShouldBe([Rune.ReplacementChar, new Rune('a')]);
    }

    /// <summary>Verifies Flush emits a replacement character for a still-incomplete sequence and
    /// clears the pending state.</summary>
    [Fact]
    public void Flush_WhenASequenceIsIncomplete_EmitsReplacementAndClearsPending()
    {
        var emitted = new List<Rune>();
        var accumulator = new Utf8TextAccumulator(emitted.Add);
        accumulator.Process("é"u8.ToArray().AsSpan(0, 1));

        accumulator.Flush();

        emitted.ShouldBe([Rune.ReplacementChar]);
        accumulator.HasPending.ShouldBeFalse();
    }

    /// <summary>Verifies Flush is a no-op, emitting nothing, when nothing is pending.</summary>
    [Fact]
    public void Flush_WhenNothingIsPending_EmitsNothing()
    {
        var emitted = new List<Rune>();
        var accumulator = new Utf8TextAccumulator(emitted.Add);

        accumulator.Flush();

        emitted.ShouldBeEmpty();
    }

    /// <summary>Verifies Clear discards a pending sequence without emitting a replacement.</summary>
    [Fact]
    public void Clear_WhenASequenceIsPending_DiscardsItWithoutEmitting()
    {
        var emitted = new List<Rune>();
        var accumulator = new Utf8TextAccumulator(emitted.Add);
        accumulator.Process("é"u8.ToArray().AsSpan(0, 1));

        accumulator.Clear();

        accumulator.HasPending.ShouldBeFalse();
        emitted.ShouldBeEmpty();
    }
}
