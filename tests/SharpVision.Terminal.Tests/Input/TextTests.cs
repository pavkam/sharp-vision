// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;


using SharpVision.Terminal.Input;


using InputAction = Terminal.Input.Action;
using InputDecoder = Terminal.Input.Decoder;
using InputText = Terminal.Input.Text;

/// <summary>
/// Verifies streaming UTF-8, Alt text, Escape ambiguity, and allocation behavior.
/// </summary>
public sealed class TextTests
{
    /// <summary>
    /// Verifies every input split preserves complete Unicode scalar values.
    /// </summary>
    [Fact]
    public void Decode_WhenUtf8IsFragmented_EmitsCompleteRunes()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("Aé👩");

        for (int split = 0; split <= bytes.Length; split++)
        {
            RecordingInputSink sink = new();
            using InputDecoder decoder = new(sink);

            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Text.Select(static item => item.Value.Value)
                .ShouldBe([0x41, 0xE9, 0x1F469], $"split {split}");
            sink.Strokes.Select(static item => item.Code)
                .ShouldAllBe(static item => item == Code.Character);
        }
    }

    /// <summary>
    /// Verifies malformed UTF-8 replaces minimally and preserves following input.
    /// </summary>
    [Fact]
    public void Decode_WhenUtf8IsInvalid_EmitsReplacementAndRecovers()
    {
        byte[] bytes = [0xF0, 0x28, 0x8C, 0x28, (byte) 'x'];

        for (int split = 0; split <= bytes.Length; split++)
        {
            RecordingInputSink sink = new();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Text.Select(static item => item.Value.Value)
                .ShouldBe(
                    [Rune.ReplacementChar.Value, '(', Rune.ReplacementChar.Value, '(', 'x'],
                    $"split {split}");
        }
    }

    /// <summary>
    /// Verifies plain and Escape-prefixed printable input emit stroke/text pairs.
    /// </summary>
    [Fact]
    public void Decode_WhenTextIsPlainOrAltModified_EmitsTypedPairs()
    {
        RecordingInputSink sink = new();
        using InputDecoder decoder = new(sink);

        decoder.Decode("x\u001by"u8);
        decoder.Complete();

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Character, new Rune('x'), 0, Modifiers.None, InputAction.Press),
            new Stroke(Code.Character, new Rune('y'), 0, Modifiers.Alt, InputAction.Press),
        ]);
        sink.Text.ShouldBe([new InputText(new Rune('x')), new InputText(new Rune('y'))]);
    }

    /// <summary>
    /// Verifies Escape-prefixed UTF-8 preserves Alt across every byte split.
    /// </summary>
    [Fact]
    public void Decode_WhenAltUtf8IsFragmented_PreservesOneScalar()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\u001bé");

        for (int split = 0; split <= bytes.Length; split++)
        {
            RecordingInputSink sink = new();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.ShouldBe(
            [
                new Stroke(
                    Code.Character,
                    new Rune('é'),
                    0,
                    Modifiers.Alt,
                    InputAction.Press),
            ], $"split {split}");
            sink.Text.ShouldBe([new InputText(new Rune('é'))], $"split {split}");
        }
    }

    /// <summary>
    /// Verifies a lone Escape is held until its deadline and then emitted once.
    /// </summary>
    [Fact]
    public void ExpireEscape_WhenDeadlineIsReached_EmitsEscape()
    {
        RecordingInputSink sink = new();
        ManualTimeProvider clock = new();
        using InputDecoder decoder = new(
            sink,
            new Options { EscapeTimeout = TimeSpan.FromMilliseconds(25) },
            clock);
        decoder.Decode("\u001b"u8);

        decoder.ExpireEscape().ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(24));
        decoder.ExpireEscape().ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        decoder.ExpireEscape().ShouldBeTrue();
        decoder.ExpireEscape().ShouldBeFalse();

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Escape, null, 0, Modifiers.None, InputAction.Press),
        ]);
    }

    /// <summary>
    /// Verifies an expired raw Escape remains included in later diagnostic offsets.
    /// </summary>
    [Fact]
    public void Decode_WhenEscapeExpired_PreservesAbsoluteDiagnosticOffset()
    {
        RecordingInputSink sink = new();
        ManualTimeProvider clock = new();
        using InputDecoder decoder = new(
            sink,
            new Options { EscapeTimeout = TimeSpan.FromMilliseconds(1) },
            clock);
        decoder.Decode("\u001b"u8);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        decoder.ExpireEscape().ShouldBeTrue();

        decoder.Decode("\u001b[1:x"u8);
        decoder.Complete();

        sink.Diagnostics.Single().Offset.ShouldBe(6);
    }

    /// <summary>
    /// Verifies completion resolves both raw Escape and incomplete UTF-8 input.
    /// </summary>
    [Fact]
    public void Complete_WhenInputIsPending_ResolvesWithoutDroppingData()
    {
        RecordingInputSink sink = new();
        using InputDecoder decoder = new(sink);

        decoder.Decode([0xF0, 0x9F]);
        decoder.Decode("\u001b"u8);
        decoder.Complete();

        sink.Text.ShouldBe([new InputText(Rune.ReplacementChar)]);
        sink.Strokes[^1].Code.ShouldBe(Code.Escape);
    }

    /// <summary>
    /// Verifies completion reports an unfinished CSI without inventing a key.
    /// </summary>
    [Fact]
    public void Complete_WhenCsiIsTruncated_ReportsDiagnostic()
    {
        RecordingInputSink sink = new();
        using InputDecoder decoder = new(sink);

        decoder.Decode("\u001b[1;"u8);
        decoder.Complete();

        sink.Strokes.ShouldBeEmpty();
        sink.Diagnostics.Single().Code.ShouldBe(DiagnosticCode.Truncated);
    }

    /// <summary>
    /// Verifies warmed ASCII decoding performs no managed allocation per event.
    /// </summary>
    [Fact]
    public void Decode_WhenAsciiPathIsWarm_AllocatesZeroBytes()
    {
        CountingInputSink sink = new();
        using InputDecoder decoder = new(sink);

        for (int index = 0; index < 10_000; index++)
        {
            decoder.Decode("a"u8);
            decoder.Decode("é"u8);
        }

        // Cross any tiered-PGO transition before the asserted allocation window.
        for (int index = 0; index < 10_000; index++)
        {
            decoder.Decode("a"u8);
            decoder.Decode("é"u8);
        }

        long minimum = long.MaxValue;

        for (int sample = 0; sample < 5; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int index = 0; index < 10_000; index++)
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
    /// Verifies pointer values reject invalid public coordinates and enum values.
    /// </summary>
    [Fact]
    public void Constructor_WhenPointerValueIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Pointer(
            new Point(-1, 0),
            null,
            Buttons.None,
            PointerAction.Move,
            0,
            0,
            Modifiers.None,
            false,
            false));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Pointer(
            default,
            null,
            Buttons.None,
            (PointerAction) int.MaxValue,
            0,
            0,
            Modifiers.None,
            false,
            false));
    }

    /// <summary>Verifies pointer coordinates distinguish cell, pixel-only, and leave values.</summary>
    [Fact]
    public void Constructor_WhenPointerCoordinateFamiliesVary_PreservesAvailability()
    {
        // Arrange / Act
        Pointer cell = new(
            new Point(2, 3),
            null,
            Buttons.None,
            PointerAction.Move,
            0,
            0,
            Modifiers.None,
            true,
            false);
        Pointer pixel = new(
            null,
            new Point(20, 30),
            Buttons.None,
            PointerAction.Move,
            0,
            0,
            Modifiers.None,
            true,
            false);
        Pointer leave = new(
            null,
            null,
            Buttons.None,
            PointerAction.Leave,
            0,
            0,
            Modifiers.None,
            true,
            false);

        // Assert
        cell.Cells.ShouldBe(new Point(2, 3));
        cell.Pixels.ShouldBeNull();
        pixel.Cells.ShouldBeNull();
        pixel.Pixels.ShouldBe(new Point(20, 30));
        leave.Cells.ShouldBeNull();
        leave.Pixels.ShouldBeNull();
    }

    /// <summary>Verifies absent coordinates are reserved for pointer leave.</summary>
    [Fact]
    public void Constructor_WhenCoordinatesAreMissingForOrdinaryAction_Throws()
    {
        _ = Should.Throw<ArgumentException>(() => new Pointer(
            null,
            null,
            Buttons.None,
            PointerAction.Move,
            0,
            0,
            Modifiers.None,
            true,
            false));
    }

}
