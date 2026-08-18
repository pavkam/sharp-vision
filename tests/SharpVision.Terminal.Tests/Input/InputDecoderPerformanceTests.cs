// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

/// <summary>
/// Verifies <see cref="InputDecoder"/> allocation behavior on the warmed ASCII/UTF-8 text path.
/// </summary>
[Collection(PerformanceGroup.Name)]
public sealed class InputDecoderPerformanceTests
{
    /// <summary>
    /// Verifies warmed ASCII decoding performs no managed allocation per event.
    /// </summary>
    [Fact]
    public void Decode_WhenAsciiPathIsWarm_AllocatesZeroBytes()
    {
        var sink = new CountingInputSink();
        using InputDecoder decoder = new(sink);

        for (var index = 0; index < 10_000; index++)
        {
            decoder.Decode("a"u8);
            decoder.Decode("é"u8);
        }

        // Cross any tiered-PGO transition before the asserted allocation window.
        for (var index = 0; index < 10_000; index++)
        {
            decoder.Decode("a"u8);
            decoder.Decode("é"u8);
        }

        var minimum = long.MaxValue;

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
            {
                decoder.Decode("a"u8);
                decoder.Decode("é"u8);
            }

            minimum = Math.Min(
                minimum,
                GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
        sink.Count.ShouldBe(280_000);
    }

    /// <summary>
    /// Verifies a run of ordinary ground-state ASCII text reaches the protocol parser and the
    /// UTF-8 accumulator through one batched call each, instead of one call per byte. Without the
    /// pre-scan, this 64-byte fragment would drive 64 separate calls into each.
    /// </summary>
    [Fact]
    public void Decode_WhenGroundTextRunIsBatched_CallsParserAndUtf8AccumulatorOnce()
    {
        var sink = new CountingInputSink();
        using InputDecoder decoder = new(sink);
        var fragment = new byte[64];

        for (var index = 0; index < fragment.Length; index++)
        {
            fragment[index] = (byte) ('a' + (index % 26));
        }

        decoder.Decode(fragment);

        decoder.ParseCallCount.ShouldBe(1);
        decoder.TextAccumulationCallCount.ShouldBe(1);
        sink.Count.ShouldBe(128);
    }
}
