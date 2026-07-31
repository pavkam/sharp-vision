// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

/// <summary>Verifies the bracketed-paste byte accumulator extracted from Decoder (see #97).</summary>
public sealed class PasteAccumulatorTests
{
    /// <summary>Verifies plain bytes accumulate until the end marker completes the paste.</summary>
    [Fact]
    public void Process_WhenEndMarkerArrives_CompletesAndBuffersPrecedingBytes()
    {
        using var accumulator = new PasteAccumulator(64);
        accumulator.Begin();

        foreach (var value in "hi"u8)
        {
            accumulator.Process(value).ShouldBeFalse();
        }

        var completed = false;

        foreach (var value in "[201~"u8)
        {
            completed = accumulator.Process(value);
        }

        completed.ShouldBeTrue();
        accumulator.Buffered.ToArray().ShouldBe("hi"u8.ToArray());
        accumulator.Overflowed.ShouldBeFalse();
    }

    /// <summary>Verifies a byte sequence that starts matching the end marker but does not
    /// complete it is appended as ordinary data instead of being silently dropped.</summary>
    [Fact]
    public void Process_WhenMarkerPrefixDoesNotComplete_AppendsThePrefixAsData()
    {
        using var accumulator = new PasteAccumulator(64);
        accumulator.Begin();

        // Escape followed by '[' matches the first two marker bytes, then 'x' breaks the match —
        // both prefix bytes must be recovered into the buffer as data, not discarded.
        foreach (var value in "[x"u8)
        {
            accumulator.Process(value).ShouldBeFalse();
        }

        accumulator.Buffered.ToArray().ShouldBe("[x"u8.ToArray());
    }

    /// <summary>Verifies bytes past the configured limit are discarded and counted rather than
    /// silently truncating the buffer mid-cluster.</summary>
    [Fact]
    public void Process_WhenByteLimitIsReached_OverflowsAndCountsDiscardedBytes()
    {
        using var accumulator = new PasteAccumulator(2);
        accumulator.Begin();

        foreach (var value in "abcd"u8)
        {
            _ = accumulator.Process(value);
        }

        accumulator.Overflowed.ShouldBeTrue();
        accumulator.DiscardedBytes.ShouldBeGreaterThan(0);
        accumulator.Buffered.Length.ShouldBe(0);
    }

    /// <summary>Verifies Reset clears buffered content and returns to the inactive state without
    /// releasing the pooled rental, so a following paste can reuse it.</summary>
    [Fact]
    public void Reset_WhenPasteWasBuffered_ClearsStateAndKeepsAcceptingFurtherPastes()
    {
        using var accumulator = new PasteAccumulator(64);
        accumulator.Begin();
        _ = accumulator.Process((byte) 'a');

        accumulator.Reset();

        accumulator.IsActive.ShouldBeFalse();
        accumulator.Overflowed.ShouldBeFalse();
        accumulator.DiscardedBytes.ShouldBe(0);
        accumulator.Buffered.Length.ShouldBe(0);

        accumulator.Begin();
        accumulator.IsActive.ShouldBeTrue();
        _ = accumulator.Process((byte) 'b');
        accumulator.Buffered.ToArray().ShouldBe("b"u8.ToArray());
    }

    /// <summary>Verifies Dispose clears state and leaves the accumulator inactive.</summary>
    [Fact]
    public void Dispose_WhenPasteWasBuffered_LeavesAccumulatorInactive()
    {
        var accumulator = new PasteAccumulator(64);
        accumulator.Begin();
        _ = accumulator.Process((byte) 'a');

        accumulator.Dispose();

        accumulator.IsActive.ShouldBeFalse();
    }
}
