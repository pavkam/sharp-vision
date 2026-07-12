using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

using CellMetrics = SharpVision.Terminal.Geometry.Metrics;
using InputAction = SharpVision.Terminal.Input.PointerAction;
using InputDecoder = SharpVision.Terminal.Input.Decoder;

namespace SharpVision.Terminal.Tests.Input;

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
    [InlineData("\u001b[<28;10;5M", Buttons.Primary, InputAction.Press, 0, 0, Modifiers.Shift | Modifiers.Alt | Modifiers.Control, false)]
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
        using var decoder = new InputDecoder(
            sink,
            new Options
            {
                PixelMouse = true,
                CellMetrics = new CellMetrics(8, 16),
            });

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
        using var decoder = new InputDecoder(
            sink,
            new Options
            {
                PixelMouse = true,
                CellMetrics = new CellMetrics(
                    new Size(10, 3),
                    new Size(101, 31)),
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
        using var decoder = new InputDecoder(
            sink,
            new Options { PixelMouse = true });

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
        using var decoder = new InputDecoder(
            sink,
            new Options
            {
                PixelMouse = true,
                CellMetrics = new CellMetrics(
                    new Size(10, 3),
                    new Size(101, 31)),
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

        foreach (var bytes in new[] { x10, sgr })
        {
            for (var split = 0; split <= bytes.Length; split++)
            {
                var sink = new RecordingInputSink();
                using var decoder = new InputDecoder(sink);
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
        using var decoder = new InputDecoder(sink);

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
        using var decoder = new InputDecoder(sink);

        decoder.Decode(Encoding.UTF8.GetBytes(input));
        decoder.Complete();

        sink.Pointers.ShouldBeEmpty();
        sink.Diagnostics.Count.ShouldBe(1);
    }

    private static Pointer Decode(byte[] bytes)
    {
        var sink = new RecordingInputSink();

        using (var decoder = new InputDecoder(sink))
        {
            decoder.Decode(bytes);
            decoder.Complete();
        }

        sink.Diagnostics.ShouldBeEmpty();
        return sink.Pointers.Single();
    }
}
