// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

using InputAction = PointerAction;

/// <summary>
/// Verifies cell, UTF-8, SGR, pixel, and urxvt pointer decoding.
/// </summary>
public sealed class MouseTests
{
    /// <summary>
    /// Verifies SGR press, release, motion, wheel, modifiers, and extra buttons.
    /// </summary>
    [Theory]
    [InlineData("\u001b[<0;10;5M", Buttons.Primary, InputAction.Press, 0, 0, Modifiers.None, false)]
    [InlineData("\u001b[<0;10;5m", Buttons.Primary, InputAction.Release, 0, 0, Modifiers.None, false)]
    [InlineData("\u001b[<32;10;5M", Buttons.Primary, InputAction.Move, 0, 0, Modifiers.None, true)]
    [InlineData("\u001b[<64;10;5M", Buttons.None, InputAction.Wheel, 0, 1, Modifiers.None, false)]
    [InlineData("\u001b[<65;10;5M", Buttons.None, InputAction.Wheel, 0, -1, Modifiers.None, false)]
    [InlineData("\u001b[<66;10;5M", Buttons.None, InputAction.Wheel, -1, 0, Modifiers.None, false)]
    [InlineData("\u001b[<67;10;5M", Buttons.None, InputAction.Wheel, 1, 0, Modifiers.None, false)]
    [InlineData("\u001b[<28;10;5M", Buttons.Primary, InputAction.Press, 0, 0,
        Modifiers.Shift | Modifiers.Alt | Modifiers.Control, false)]
    [InlineData("\u001b[<128;10;5M", Buttons.Back, InputAction.Press, 0, 0, Modifiers.None, false)]
    [InlineData("\u001b[<129;10;5M", Buttons.Forward, InputAction.Press, 0, 0, Modifiers.None, false)]
    public void Decode_WhenSgrMouseArrives_MapsSemanticPointer(
        string input,
        Buttons buttons,
        InputAction action,
        int wheelX,
        int wheelY,
        Modifiers modifiers,
        bool motion)
    {
        var pointer = Decode(Encoding.UTF8.GetBytes(input));

        pointer.Cells.ShouldBe(new Point(9, 4));
        pointer.Pixels.ShouldBeNull();
        pointer.Buttons.ShouldBe(buttons);
        pointer.Action.ShouldBe(action);
        pointer.WheelX.ShouldBe(wheelX);
        pointer.WheelY.ShouldBe(wheelY);
        pointer.Modifiers.ShouldBe(modifiers);
        pointer.IsMotion.ShouldBe(motion);
    }

    /// <summary>
    /// Verifies SGR pixel coordinates preserve pixels and infer cells once.
    /// </summary>
    [Fact]
    public void Decode_WhenPixelMouseArrives_PreservesPixelsAndInfersCells()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(
            sink,
            new InputOptions { PixelMouse = true, CellMetrics = new CellMetrics(8, 16) });

        decoder.Decode("\u001b[<0;17;33M"u8);

        var pointer = sink.Pointers.Single();
        pointer.Pixels.ShouldBe(new Point(16, 32));
        pointer.Cells.ShouldBe(new Point(2, 2));
        pointer.IsCellPositionInferred.ShouldBeTrue();
    }

    /// <summary>Verifies uneven total dimensions preserve the final cell.</summary>
    [Fact]
    public void Decode_WhenPixelGridIsUneven_UsesExactRationalMapping()
    {
        // Arrange
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(
            sink,
            new InputOptions
            {
                PixelMouse = true,
                CellMetrics = new CellMetrics(
                    new Size(10, 3),
                    new Size(101, 31))
            });

        // Act
        decoder.Decode("\u001b[<0;101;31M"u8);

        // Assert
        var pointer = sink.Pointers.Single();
        pointer.Pixels.ShouldBe(new Point(100, 30));
        pointer.Cells.ShouldBe(new Point(9, 2));
        pointer.IsCellPositionInferred.ShouldBeTrue();
    }

    /// <summary>Verifies pixel input without geometry does not fabricate top-left cells.</summary>
    [Fact]
    public void Decode_WhenPixelMetricsAreMissing_PreservesOnlyPixels()
    {
        // Arrange
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(
            sink,
            new InputOptions { PixelMouse = true });

        // Act
        decoder.Decode("\u001b[<0;17;33M"u8);

        // Assert
        var pointer = sink.Pointers.Single();
        pointer.Pixels.ShouldBe(new Point(16, 32));
        pointer.Cells.ShouldBeNull();
        pointer.IsCellPositionInferred.ShouldBeFalse();
    }

    /// <summary>Verifies pixels outside exact totals remain unmapped.</summary>
    [Fact]
    public void Decode_WhenPixelIsOutsideExactGrid_PreservesOnlyPixels()
    {
        // Arrange
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(
            sink,
            new InputOptions
            {
                PixelMouse = true,
                CellMetrics = new CellMetrics(
                    new Size(10, 3),
                    new Size(101, 31))
            });

        // Act
        decoder.Decode("\u001b[<0;102;31M"u8);

        // Assert
        var pointer = sink.Pointers.Single();
        pointer.Pixels.ShouldBe(new Point(101, 30));
        pointer.Cells.ShouldBeNull();
        pointer.IsCellPositionInferred.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies the mouse-leave sentinel remains distinct from invalid zero coordinates.
    /// </summary>
    [Fact]
    public void Decode_WhenMouseLeaves_EmitsLeaveWithoutCoordinates()
    {
        var pointer = Decode("\u001b[<35;0;0M"u8.ToArray());

        pointer.Action.ShouldBe(InputAction.Leave);
        pointer.Cells.ShouldBe(default);
        pointer.IsMotion.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies maximum representable wire coordinates convert without overflow.
    /// </summary>
    [Fact]
    public void Decode_WhenCoordinatesAreMaximum_PreservesBoundedCells()
    {
        var pointer = Decode(Encoding.UTF8.GetBytes(
            $"\u001b[<0;{int.MaxValue};{int.MaxValue}M"));

        pointer.Cells.ShouldBe(new Point(int.MaxValue - 1, int.MaxValue - 1));
    }

    /// <summary>
    /// Verifies X10 and urxvt compatibility forms map the same cell position.
    /// </summary>
    [Theory]
    [InlineData("\u001b[M *%")]
    [InlineData("\u001b[32;10;5M")]
    public void Decode_WhenLegacyMouseArrives_MapsCellPress(string input)
    {
        var pointer = Decode(Encoding.UTF8.GetBytes(input));

        pointer.ShouldBe(new Pointer(
            new Point(9, 4),
            null,
            Buttons.Primary,
            InputAction.Press,
            0,
            0,
            Modifiers.None,
            false,
            false));
    }

    /// <summary>
    /// Verifies a 0x7f (DEL) X10 coordinate field byte decodes as coordinate 95 rather than
    /// destroying the pending report and leaking a phantom Backspace plus a literal character
    /// into the keystroke stream (#263).
    /// </summary>
    [Theory]
    [InlineData("\u001b[M \u007f%", 94, 4)]
    [InlineData("\u001b[M *\u007f", 9, 94)]
    public void Decode_WhenX10FieldContainsDel_ResolvesCoordinateNinetyFive(
        string input,
        int expectedX,
        int expectedY)
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);
        decoder.Decode(Encoding.UTF8.GetBytes(input));
        decoder.Complete();

        var pointer = sink.Pointers.ShouldHaveSingleItem();
        pointer.Cells.ShouldBe(new Point(expectedX, expectedY));
        pointer.Buttons.ShouldBe(Buttons.Primary);
        pointer.Action.ShouldBe(InputAction.Press);
        sink.Strokes.ShouldBeEmpty();
        sink.Text.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a plain, unrelated DEL byte still decodes as an ordinary Backspace keystroke
    /// when no X10 report is pending (#263).
    /// </summary>
    [Fact]
    public void Decode_WhenDelArrivesWithoutPendingReport_EmitsBackspaceStroke()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);
        decoder.Decode([0x7f]);
        decoder.Complete();

        sink.Pointers.ShouldBeEmpty();
        sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.Backspace);
    }

    /// <summary>
    /// Verifies every spec-legal raw X10 coordinate byte (0x20..0xFF, excluding the reserved
    /// zero coordinate at 0x20) decodes as <c>value - 32</c> under the negotiated
    /// <see cref="MouseCoordinates.Default"/> encoding, rather than being misread as UTF-8 and
    /// discarded past 0x7F (#270).
    /// </summary>
    [Fact]
    public void Decode_WhenCoordinatesAreDefault_DecodesEveryRawByteField()
    {
        var options = new InputOptions { MouseCoordinates = MouseCoordinates.Default };

        for (var fieldByte = 0x21; fieldByte <= 0xff; fieldByte++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);
            byte[] bytes = [0x1b, (byte) '[', (byte) 'M', 0x20, (byte) fieldByte, 0x21];

            decoder.Decode(bytes);
            decoder.Complete();

            var pointer = sink.Pointers.ShouldHaveSingleItem();
            pointer.Cells!.Value.X.ShouldBe(fieldByte - 32 - 1, $"field byte 0x{fieldByte:x2}");
            sink.Diagnostics.ShouldBeEmpty($"field byte 0x{fieldByte:x2}");
        }
    }

    /// <summary>
    /// Verifies raw X10 field bytes 0x20 (the reserved zero coordinate) and any byte outside
    /// 0x20..0xFF do not desynchronize the decoder under <see cref="MouseCoordinates.Default"/>.
    /// </summary>
    [Fact]
    public void Decode_WhenCoordinatesAreDefaultAndFieldIsZero_ReportsMalformedAndRecovers()
    {
        var options = new InputOptions { MouseCoordinates = MouseCoordinates.Default };
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink, options);

        decoder.Decode([0x1b, (byte) '[', (byte) 'M', 0x20, 0x20, 0x20, (byte) 'x']);
        decoder.Complete();

        sink.Pointers.ShouldBeEmpty();
        _ = sink.Diagnostics.ShouldHaveSingleItem();
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies UTF-8 X10 coordinate scalars, including values past the single-byte range,
    /// decode as <c>scalar - 32</c> under the negotiated <see cref="MouseCoordinates.Utf8"/>
    /// encoding (#270).
    /// </summary>
    [Theory]
    [InlineData(0x21)]
    [InlineData(0x7e)]
    [InlineData(0x80)]
    [InlineData(200)]
    [InlineData(2015)]
    public void Decode_WhenCoordinatesAreUtf8_DecodesScalarField(int scalar)
    {
        var options = new InputOptions { MouseCoordinates = MouseCoordinates.Utf8 };
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink, options);
        var bytes = "\u001b[M "u8.ToArray()
            .Concat(Encoding.UTF8.GetBytes(new Rune(scalar).ToString()))
            .Concat("!"u8.ToArray())
            .ToArray();

        decoder.Decode(bytes);
        decoder.Complete();

        var pointer = sink.Pointers.ShouldHaveSingleItem();
        pointer.Cells!.Value.X.ShouldBe(scalar - 32 - 1);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies an invalid raw X10 field byte sequence still reports malformed and recovers,
    /// rather than desynchronizing the decoder, mirroring the existing UTF-8 negative coverage
    /// for the new <see cref="MouseCoordinates.Default"/> reader (#270).
    /// </summary>
    [Fact]
    public void Decode_WhenRawFieldSequenceIsTruncated_ReportsMalformedAndRecovers()
    {
        var options = new InputOptions { MouseCoordinates = MouseCoordinates.Default };
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink, options);

        // Only two of the three required raw X10 fields arrive before an unrelated escape
        // sequence begins, which must discard the pending report rather than misreading it.
        decoder.Decode([0x1b, (byte) '[', (byte) 'M', 0x20, 0x41]);
        decoder.Decode("\u001b[A"u8);
        decoder.Complete();

        sink.Pointers.ShouldBeEmpty();
        sink.Diagnostics.ShouldNotBeEmpty();
        sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.Up);
    }

    /// <summary>
    /// Verifies UTF-8 X10 coordinates and SGR input survive every byte split.
    /// </summary>
    [Fact]
    public void Decode_WhenMouseIsFragmented_MapsAtEverySplit()
    {
        var x10 = "\u001b[M "u8.ToArray()
            .Concat(Encoding.UTF8.GetBytes(new Rune(333).ToString()))
            .Concat(Encoding.UTF8.GetBytes(new Rune(233).ToString()))
            .ToArray();
        var sgr = "\u001b[<0;10;5M"u8.ToArray();

        foreach (var (bytes, options) in new[]
                 {
                     (x10, new InputOptions { MouseCoordinates = MouseCoordinates.Utf8 }),
                     (sgr, InputOptions.Default)
                 })
        {
            for (var split = 0; split <= bytes.Length; split++)
            {
                var sink = new RecordingInputSink();
                using InputDecoder decoder = new(sink, options);
                decoder.Decode(bytes.AsSpan(0, split));
                decoder.Decode(bytes.AsSpan(split));
                decoder.Complete();

                sink.Pointers.Count.ShouldBe(1, $"split {split}");
                sink.Diagnostics.ShouldBeEmpty($"split {split}");
            }
        }
    }

    /// <summary>
    /// Verifies malformed mouse input reports once and the next key survives.
    /// </summary>
    [Fact]
    public void Decode_WhenMouseIsMalformed_ReportsAndRecovers()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode("\u001b[<0;0;5Mx"u8);
        decoder.Complete();

        sink.Pointers.ShouldBeEmpty();
        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies undefined extended buttons and truncated X10 reports recover safely.
    /// </summary>
    [Theory]
    [InlineData("\u001b[<130;10;5M")]
    [InlineData("\u001b[M *")]
    public void Complete_WhenMouseEncodingIsInvalid_ReportsWithoutPointer(string input)
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode(Encoding.UTF8.GetBytes(input));
        decoder.Complete();

        sink.Pointers.ShouldBeEmpty();
        sink.Diagnostics.Count.ShouldBe(1);
    }

    private static Pointer Decode(byte[] bytes)
    {
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink))
        {
            decoder.Decode(bytes);
            decoder.Complete();
        }

        sink.Diagnostics.ShouldBeEmpty();
        return sink.Pointers.Single();
    }
}
