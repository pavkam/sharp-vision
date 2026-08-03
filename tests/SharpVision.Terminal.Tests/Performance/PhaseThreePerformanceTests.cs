// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Performance;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Input;

/// <summary>
/// Gates deterministic Phase 3 allocations and reports non-gating local timings.
/// </summary>
[Collection(PerformanceGroup.Name)]
public sealed class PhaseThreePerformanceTests
{
    private static TerminalCapabilities TrueColorCapabilities { get; } =
        TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };

    /// <summary>Verifies warmed ASCII, mixed, and emoji segmentation allocate nothing.</summary>
    [Fact]
    public void Enumerate_WhenRepresentativeTextIsWarm_AllocatesZeroBytes()
    {
        string[] values = ["plain ASCII", "e\u0301 · 界", "👩🏽‍💻 🇵🇹"];

        for (var index = 0; index < 10_000; index++)
        {
            Count(values);
        }

        var (allocated, elapsed) = Measure(static state => Count(state), values);

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
        var row = new string('x', 80);

        for (var y = 0; y < dense.Size.Height; y++)
        {
            _ = dense.Canvas.Draw(row.AsSpan(), new Point(0, y));
        }

        var destination = new ArrayBufferWriter<byte>(8192);

        for (var index = 0; index < 10_000; index++)
        {
            Encode(front, sparse, dense, destination);
        }

        var minimum = long.MaxValue;
        var watch = Stopwatch.StartNew();

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
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

    /// <summary>Verifies warmed compiled cursor, style, and color expansion allocates no managed memory.</summary>
    [Fact]
    public void Encode_WhenDescriptionProgramsAreWarm_AllocatesZeroBytes()
    {
        using Frame front = new(new Size(2, 1));
        using Frame back = new(new Size(2, 1));
        _ = front.Canvas.Draw("ab", default);
        _ = back.Canvas.Draw(
            "ab",
            default,
            new CellStyle(ReferenceColors.Get(4), attributes: Attributes.Bold));
        back.SetCursor(new Point(1, 0), visible: true, CursorShape.Bar);
        var profile = new TerminalProfile(
            new Description("performance", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 },
            new Programs(new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%df"u8),
                ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
                ["clear"] = new DescriptionProgram("\u001b[2J"u8),
                ["bold"] = new DescriptionProgram("\u001b[1m"u8),
                ["setaf"] = new DescriptionProgram("\u001b[38;5;%p1%dm"u8),
                ["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8),
                ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
                ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8),
                ["Ss"] = new DescriptionProgram("\u001b[%p1%d q"u8),
                ["Se"] = new DescriptionProgram("\u001b[0 q"u8)
            }),
            KeyMap.Empty);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>(1024);

        for (var index = 0; index < 1_000; index++)
        {
            EncodeDescription(front, back, destination, profile, interpreter);
        }

        destination.WrittenSpan.IndexOf("\u001b[38;5;4m"u8).ShouldBeGreaterThanOrEqualTo(0);

        var minimum = long.MaxValue;

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
            {
                EncodeDescription(front, back, destination, profile, interpreter);
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
    }

    /// <summary>Verifies warmed text, mouse, and Kitty decoding allocate nothing.</summary>
    [Fact]
    public void Decode_WhenRepresentativeInputIsWarm_AllocatesZeroBytes()
    {
        var sink = new CountingSink();
        using InputDecoder decoder = new(sink);

        for (var index = 0; index < 20_000; index++)
        {
            Decode(decoder);
        }

        var minimum = long.MaxValue;
        var watch = Stopwatch.StartNew();

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
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

    /// <summary>Verifies warmed described-signature lookup allocates no managed memory.</summary>
    [Fact]
    public void Decode_WhenDescriptionKeyIsWarm_AllocatesZeroBytes()
    {
        var sink = new CountingSink();
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding("\u001b[99~"u8, Code.F63)]),
            useAnsiKeyGrammar: false);
        using InputDecoder decoder = new(sink, options);

        for (var index = 0; index < 10_000; index++)
        {
            decoder.Decode("\u001b[99~"u8);
        }

        var minimum = long.MaxValue;

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
            {
                decoder.Decode("\u001b[99~"u8);
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
        sink.Count.ShouldBe(60_000);
    }

    /// <summary>Verifies warmed fallback matching and suffix rematching allocate no managed memory.</summary>
    [Fact]
    public void Decode_WhenFallbackKeysAreWarm_AllocatesZeroBytes()
    {
        var sink = new CountingSink();
        var options = Options.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F61),
                new KeyBinding([0xff, 0xfe, 0xff], Code.F62),
                new KeyBinding([0xfe, 0x78], Code.F63)
            ]),
            useAnsiKeyGrammar: false);
        using InputDecoder decoder = new(sink, options);
        var input = new byte[] { 0xff, 0xfe, 0x78 };

        for (var index = 0; index < 10_000; index++)
        {
            decoder.Decode(input);
        }

        var minimum = long.MaxValue;

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
            {
                decoder.Decode(input);
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
        sink.Count.ShouldBe(120_000);
    }

    /// <summary>Verifies warmed xterm cursor and color programs allocate nothing.</summary>
    [Fact]
    public void Write_WhenTerminfoProgramsAreWarm_AllocatesZeroBytes()
    {
        // ncurses 6.6 terminfo.src xterm cursor-address and xterm-256color setaf forms.
        var cursor = "\u001b[%i%p1%d;%p2%dH"u8.Compile(ProgramLimits.Default);
        var color = "\u001b[%?%p1%{8}%<%t3%p1%d%e%p1%{16}%<%t9%p1%{8}%-%d%e38;5;%p1%d%;m"u8
            .Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>(64);
        object?[] cursorParameters = [4, 9];
        object?[] colorParameters = [200];

        for (var index = 0; index < 10_000; index++)
        {
            ExpandPrograms(interpreter, cursor, color, cursorParameters, colorParameters, destination);
        }

        var minimum = long.MaxValue;
        var expansionWatch = Stopwatch.StartNew();

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
            {
                ExpandPrograms(interpreter, cursor, color, cursorParameters, colorParameters, destination);
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        expansionWatch.Stop();

        minimum.ShouldBe(0);
        Report("terminfo cup/setaf expansion", expansionWatch.Elapsed, 100_000);
    }

    /// <summary>Verifies executable-contract metadata does not add eager per-profile probe cost.</summary>
    [Fact]
    public void Constructor_WhenManyProgramSetsAreCreated_RemainsWithinOpaqueBaseline()
    {
        var executable = new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8)
        };
        var opaque = new Dictionary<string, DescriptionProgram>
        {
            ["first"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["second"] = new DescriptionProgram("\u001b[0m"u8),
            ["third"] = new DescriptionProgram("\u001b[2J"u8)
        };
        _ = ConstructPrograms(executable, 100);
        _ = ConstructPrograms(opaque, 100);

        var opaqueBefore = GC.GetAllocatedBytesForCurrentThread();
        var opaqueWatch = Stopwatch.StartNew();
        var opaqueCount = ConstructPrograms(opaque, 10_000);
        opaqueWatch.Stop();
        var opaqueAllocated = GC.GetAllocatedBytesForCurrentThread() - opaqueBefore;
        var executableBefore = GC.GetAllocatedBytesForCurrentThread();
        var executableWatch = Stopwatch.StartNew();
        var executableCount = ConstructPrograms(executable, 10_000);
        executableWatch.Stop();
        var executableAllocated = GC.GetAllocatedBytesForCurrentThread() - executableBefore;

        executableCount.ShouldBe(opaqueCount);
        executableAllocated.ShouldBeLessThanOrEqualTo(opaqueAllocated + 1_024);
        Report("opaque vs. executable program-set construction", executableWatch.Elapsed, executableCount);
    }

    private static (long Allocated, TimeSpan Elapsed) Measure<TState>(
        Action<TState> action,
        TState state)
    {
        var minimum = long.MaxValue;
        var watch = Stopwatch.StartNew();

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
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

    private static int ConstructPrograms(IReadOnlyDictionary<string, DescriptionProgram> values, int iterations)
    {
        var count = 0;

        for (var index = 0; index < iterations; index++)
        {
            count += new Programs(values).Count;
        }

        return count;
    }

    private static void Count(string[] values)
    {
        foreach (var value in values)
        {
            foreach (var unused in Graphemes.Enumerate(value.AsSpan()))
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

    private static void EncodeDescription(
        Frame front,
        Frame back,
        ArrayBufferWriter<byte> destination,
        TerminalProfile profile,
        Interpreter interpreter)
    {
        destination.Clear();
        interpreter.BeginTransaction();

        try
        {
            _ = FrameEncoder.Encode(front, back, destination, profile, interpreter);
        }
        finally
        {
            interpreter.RollbackTransaction();
        }
    }

    private static void ExpandPrograms(
        Interpreter interpreter,
        DescriptionProgram cursor,
        DescriptionProgram color,
        object?[] cursorParameters,
        object?[] colorParameters,
        ArrayBufferWriter<byte> destination)
    {
        destination.Clear();
        interpreter.Write(cursor, cursorParameters, destination);
        interpreter.Write(color, colorParameters, destination);
    }

    private static void Report(string scenario, TimeSpan elapsed, int iterations)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{scenario}: {iterations} iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; " +
            $"{RuntimeInformation.ProcessArchitecture}; {RuntimeInformation.OSDescription}");
    }
}
