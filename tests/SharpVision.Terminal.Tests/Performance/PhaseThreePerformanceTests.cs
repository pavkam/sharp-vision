// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Performance;

using System.Diagnostics;
using System.Runtime.InteropServices;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Unicode;


using FrameEncoder = Terminal.Rendering.Encoder;
using InputDecoder = Terminal.Input.Decoder;
using TerminalCapabilities = Terminal.Capabilities.Capabilities;

/// <summary>
/// Gates deterministic Phase 3 allocations and reports non-gating local timings.
/// </summary>
public sealed class PhaseThreePerformanceTests
{
    private static TerminalCapabilities TrueColorCapabilities { get; } =
        TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };

    /// <summary>Verifies warmed ASCII, mixed, and emoji segmentation allocate nothing.</summary>
    [Fact]
    public void Enumerate_WhenRepresentativeTextIsWarm_AllocatesZeroBytes()
    {
        string[] values = ["plain ASCII", "e\u0301 · 界", "👩🏽‍💻 🇵🇹"];

        for (int index = 0; index < 10_000; index++)
        {
            Count(values);
        }

        (long allocated, TimeSpan elapsed) = Measure(static state => Count(state), values);

        allocated.ShouldBe(0);
        Report("segmentation", elapsed, 10_000);
    }

    /// <summary>Verifies warmed unchanged, sparse, and dense frame scans allocate nothing.</summary>
    [Fact]
    public void Encode_WhenRepresentativeFramesAreWarm_AllocatesZeroBytes()
    {
        using Frame front = new(new Size(80, 24));
        using Frame sparse = new(new Size(80, 24));
        using Frame dense = new(new Size(80, 24));
        _ = sparse.Canvas.Draw("x".AsSpan(), new Point(40, 12));
        string row = new('x', 80);

        for (int y = 0; y < dense.Size.Height; y++)
        {
            _ = dense.Canvas.Draw(row.AsSpan(), new Point(0, y));
        }

        ArrayBufferWriter<byte> destination = new(8192);

        for (int index = 0; index < 10_000; index++)
        {
            Encode(front, sparse, dense, destination);
        }

        long minimum = long.MaxValue;
        Stopwatch watch = Stopwatch.StartNew();

        for (int sample = 0; sample < 5; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int index = 0; index < 10_000; index++)
            {
                Encode(front, sparse, dense, destination);
            }

            minimum = Math.Min(
                minimum,
                GC.GetAllocatedBytesForCurrentThread() - before);
        }

        watch.Stop();
        minimum.ShouldBe(0);
        Report("frame no-op/sparse/dense encoding", watch.Elapsed, 50_000);
    }

    /// <summary>Verifies warmed text, mouse, and Kitty decoding allocate nothing.</summary>
    [Fact]
    public void Decode_WhenRepresentativeInputIsWarm_AllocatesZeroBytes()
    {
        CountingSink sink = new();
        using InputDecoder decoder = new(sink);

        for (int index = 0; index < 20_000; index++)
        {
            Decode(decoder);
        }

        long minimum = long.MaxValue;
        Stopwatch watch = Stopwatch.StartNew();

        for (int sample = 0; sample < 5; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int index = 0; index < 10_000; index++)
            {
                Decode(decoder);
            }

            minimum = Math.Min(
                minimum,
                GC.GetAllocatedBytesForCurrentThread() - before);
        }

        watch.Stop();
        minimum.ShouldBe(0);
        sink.Count.ShouldBe(280_000);
        Report("legacy text/mouse/Kitty decoding", watch.Elapsed, 50_000);
    }

    private static (long Allocated, TimeSpan Elapsed) Measure<TState>(
        Action<TState> action,
        TState state)
    {
        long minimum = long.MaxValue;
        Stopwatch watch = Stopwatch.StartNew();

        for (int sample = 0; sample < 5; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int index = 0; index < 10_000; index++)
            {
                action(state);
            }

            minimum = Math.Min(
                minimum,
                GC.GetAllocatedBytesForCurrentThread() - before);
        }

        watch.Stop();
        return (minimum, watch.Elapsed);
    }

    private static void Count(string[] values)
    {
        foreach (string value in values)
        {
            foreach (Grapheme unused in Graphemes.Enumerate(value.AsSpan()))
            {
                _ = unused;
            }
        }
    }

    private static void Encode(
        Frame front,
        Frame sparse,
        Frame dense,
        ArrayBufferWriter<byte> destination)
    {
        destination.Clear();
        _ = FrameEncoder.Encode(front, front, destination, TrueColorCapabilities);
        destination.Clear();
        _ = FrameEncoder.Encode(front, sparse, destination, TrueColorCapabilities);
        destination.Clear();
        _ = FrameEncoder.Encode(front, dense, destination, TrueColorCapabilities);
    }

    private static void Decode(InputDecoder decoder)
    {
        decoder.Decode("x"u8);
        decoder.Decode("\u001b[<0;10;5M"u8);
        decoder.Decode("\u001b[57376u"u8);
    }

    private static void Report(string scenario, TimeSpan elapsed, int iterations)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{scenario}: {iterations} iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; " +
            $"{RuntimeInformation.ProcessArchitecture}; {RuntimeInformation.OSDescription}");
    }

}
