// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

using InputAction = PointerAction;

/// <summary>
/// Verifies <see cref="InputDecoder"/> behavior: focus, pointer, Kitty and legacy keyboard
/// decoding, bracketed paste, terminal-description key precedence, CSI dispatch precedence,
/// enhanced (modifyOtherKeys) input, and streaming text/allocation behavior.
/// </summary>
public sealed class InputDecoderTests
{
    #region Focus

    /// <summary>
    /// Verifies gained/lost reports preserve order and do not consume text.
    /// </summary>
    [Fact]
    public void Decode_WhenFocusAndTextAreAdjacent_EmitsOrderedValues()
    {
        var bytes = "\u001b[Ix\u001b[O"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Focus.ShouldBe([new TerminalFocus(true), new TerminalFocus(false)], $"split {split}");
            sink.Text.Single().Value.ShouldBe(new Rune('x'), $"split {split}");
        }
    }

    #endregion

    #region Pointer (mouse)

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
    [InlineData("\u001b[<131;10;5M", Buttons.None, InputAction.Release, 0, 0, Modifiers.None, false)]
    public void Decode_WhenSgrMouseArrives_MapsSemanticPointer(
        string input,
        Buttons buttons,
        InputAction action,
        int wheelX,
        int wheelY,
        Modifiers modifiers,
        bool motion)
    {
        var pointer = DecodePointer(Encoding.UTF8.GetBytes(input));

        pointer.Cells.ShouldBe(new Point(9, 4));
        pointer.Pixels.ShouldBeNull();
        pointer.Buttons.ShouldBe(buttons);
        pointer.Action.ShouldBe(action);
        pointer.WheelX.ShouldBe(wheelX);
        pointer.WheelY.ShouldBe(wheelY);
        pointer.Modifiers.ShouldBe(modifiers);
        pointer.MotionReported.ShouldBe(motion);
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
        pointer.CellPositionInferred.ShouldBeTrue();
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
        pointer.CellPositionInferred.ShouldBeTrue();
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
        pointer.CellPositionInferred.ShouldBeFalse();
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
        pointer.CellPositionInferred.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies the mouse-leave sentinel remains distinct from invalid zero coordinates.
    /// </summary>
    [Fact]
    public void Decode_WhenMouseLeaves_EmitsLeaveWithoutCoordinates()
    {
        var pointer = DecodePointer("\u001b[<35;0;0M"u8.ToArray());

        pointer.Action.ShouldBe(InputAction.Leave);
        pointer.Cells.ShouldBe(default);
        pointer.MotionReported.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies maximum representable wire coordinates convert without overflow.
    /// </summary>
    [Fact]
    public void Decode_WhenCoordinatesAreMaximum_PreservesBoundedCells()
    {
        var pointer = DecodePointer(Encoding.UTF8.GetBytes(
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
        var pointer = DecodePointer(Encoding.UTF8.GetBytes(input));

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
    /// into the keystroke stream.
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
    /// when no X10 report is pending.
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
    /// discarded past 0x7F.
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
    /// encoding.
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
    /// for the new <see cref="MouseCoordinates.Default"/> reader.
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

    private static Pointer DecodePointer(byte[] bytes)
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

    #endregion

    #region Kitty CSI-u keyboard

    /// <summary>
    /// Verifies alternate keys, repeat, and associated text follow official grammar.
    /// </summary>
    [Fact]
    public void Decode_WhenFullKittyEventArrives_PreservesEveryField()
    {
        var sink = Decode("\u001b[97:65:99;6:2;65:98u"u8.ToArray());

        sink.Strokes.ShouldBe(
        [
            new Stroke(
                Code.Character,
                new Rune('a'),
                97,
                Modifiers.Shift | Modifiers.Control,
                KeyAction.Repeat,
                new Rune('A'),
                new Rune('c'))
        ]);
        sink.Text.Select(static value => value.Value)
            .ShouldBe([new Rune('A'), new Rune('b')]);
    }

    /// <summary>
    /// Verifies press/repeat/release and every modifier bit.
    /// </summary>
    [Theory]
    [InlineData("\u001b[97;256:1u", KeyAction.Press)]
    [InlineData("\u001b[97;256:2u", KeyAction.Repeat)]
    [InlineData("\u001b[97;256:3u", KeyAction.Release)]
    public void Decode_WhenKittyActionVaries_MapsModifiersAndAction(
        string input,
        KeyAction action)
    {
        var stroke = Decode(Encoding.UTF8.GetBytes(input)).Strokes.Single();

        stroke.Action.ShouldBe(action);
        stroke.Modifiers.ShouldBe(
            Modifiers.Shift |
            Modifiers.Alt |
            Modifiers.Control |
            Modifiers.Super |
            Modifiers.Hyper |
            Modifiers.Meta |
            Modifiers.CapsLock |
            Modifiers.NumLock);
    }

    /// <summary>
    /// Verifies canonical control and known functional key codes map logically.
    /// </summary>
    [Theory]
    [InlineData(27, Code.Escape)]
    [InlineData(13, Code.Enter)]
    [InlineData(9, Code.Tab)]
    [InlineData(127, Code.Backspace)]
    [InlineData(57348, Code.Insert)]
    [InlineData(57349, Code.Delete)]
    [InlineData(57350, Code.Left)]
    [InlineData(57351, Code.Right)]
    [InlineData(57352, Code.Up)]
    [InlineData(57353, Code.Down)]
    [InlineData(57354, Code.PageUp)]
    [InlineData(57355, Code.PageDown)]
    [InlineData(57356, Code.Home)]
    [InlineData(57357, Code.End)]
    [InlineData(57358, Code.CapsLock)]
    [InlineData(57364, Code.F1)]
    [InlineData(57365, Code.F2)]
    [InlineData(57366, Code.F3)]
    [InlineData(57367, Code.F4)]
    [InlineData(57368, Code.F5)]
    [InlineData(57369, Code.F6)]
    [InlineData(57370, Code.F7)]
    [InlineData(57371, Code.F8)]
    [InlineData(57372, Code.F9)]
    [InlineData(57373, Code.F10)]
    [InlineData(57374, Code.F11)]
    [InlineData(57375, Code.F12)]
    [InlineData(57376, Code.F13)]
    public void Decode_WhenKittyCodeIsKnown_MapsLogicalCode(int native, Code code)
    {
        var sink = Decode(Encoding.ASCII.GetBytes($"\u001b[{native}u"));

        sink.Strokes.Single().ShouldBe(
            new Stroke(code, null, native, Modifiers.None, KeyAction.Press));
    }

    /// <summary>
    /// Verifies unknown functional codes remain typed with their native number.
    /// </summary>
    [Fact]
    public void Decode_WhenKittyCodeIsUnknown_PreservesNativeCode()
    {
        var stroke = Decode("\u001b[63743u"u8.ToArray()).Strokes.Single();

        stroke.Code.ShouldBe(Code.Unknown);
        stroke.NativeCode.ShouldBe(63743);
    }

    /// <summary>
    /// Verifies pure-text events emit one unknown stroke then ordered text values.
    /// </summary>
    [Fact]
    public void Decode_WhenKittyEventIsPureText_EmitsStrokeThenText()
    {
        var sink = Decode("\u001b[0;;229:946u"u8.ToArray());

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Unknown, null, 0, Modifiers.None, KeyAction.Press)
        ]);
        sink.Text.Select(static value => value.Value)
            .ShouldBe([new Rune('å'), new Rune('β')]);
    }

    /// <summary>
    /// Verifies representative Kitty events decode identically at every split.
    /// </summary>
    [Fact]
    public void Decode_WhenKittyEventIsFragmented_MapsAtEverySplit()
    {
        var bytes = "\u001b[97:65:99;6:2;65:98u"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.Count.ShouldBe(1, $"split {split}");
            sink.Text.Count.ShouldBe(2, $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies malformed scalars, fields, and controls report once then recover.
    /// </summary>
    [Theory]
    [InlineData("\u001b[97;0u")]
    [InlineData("\u001b[97;1:4u")]
    [InlineData("\u001b[1114112u")]
    [InlineData("\u001b[97;;127u")]
    [InlineData("\u001b[97;1;65;66u")]
    [InlineData("\u001b[<97u")]
    public void Decode_WhenKittyEventIsMalformed_ReportsAndRecovers(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input + "x");
        var sink = Decode(bytes);

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }


    // ── Printable characters with modifier combinations ──────────────────

    /// <summary>
    /// Verifies a bare printable character with no modifier parameter.
    /// </summary>
    [Fact]
    public void Decode_WhenBareCharacterCode_MapsCharacterWithNoModifiers()
    {
        var stroke = Decode("[97u"u8.ToArray()).Strokes.Single();

        stroke.ShouldBe(
            new Stroke(Code.Character, new Rune('a'), 97, Modifiers.None, KeyAction.Press));
    }

    /// <summary>
    /// Verifies each single-modifier and common multi-modifier combination.
    /// </summary>
    [Theory]
    [InlineData("[97;2u", Modifiers.Shift)]
    [InlineData("[97;3u", Modifiers.Alt)]
    [InlineData("[97;5u", Modifiers.Control)]
    [InlineData("[97;7u", Modifiers.Control | Modifiers.Alt)]
    [InlineData("[97;4u", Modifiers.Shift | Modifiers.Alt)]
    [InlineData("[97;6u", Modifiers.Shift | Modifiers.Control)]
    [InlineData("[97;8u", Modifiers.Shift | Modifiers.Control | Modifiers.Alt)]
    [InlineData("[97;9u", Modifiers.Super)]
    [InlineData("[97;17u", Modifiers.Hyper)]
    [InlineData("[97;33u", Modifiers.Meta)]
    [InlineData("[97;65u", Modifiers.CapsLock)]
    [InlineData("[97;129u", Modifiers.NumLock)]
    public void Decode_WhenModifierCombinationVaries_MapsCorrectFlags(
        string input,
        Modifiers expected)
    {
        var stroke = Decode(Encoding.UTF8.GetBytes(input)).Strokes.Single();

        stroke.Code.ShouldBe(Code.Character);
        stroke.Character.ShouldBe(new Rune('a'));
        stroke.NativeCode.ShouldBe(97);
        stroke.Modifiers.ShouldBe(expected);
        stroke.Action.ShouldBe(KeyAction.Press);
    }

    // ── Event types ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies press, repeat, and release event types with no modifiers.
    /// </summary>
    [Theory]
    [InlineData("[97;1:1u", KeyAction.Press)]
    [InlineData("[97;1:2u", KeyAction.Repeat)]
    [InlineData("[97;1:3u", KeyAction.Release)]
    public void Decode_WhenEventTypeVariesWithoutModifiers_MapsAction(
        string input,
        KeyAction action)
    {
        var stroke = Decode(Encoding.UTF8.GetBytes(input)).Strokes.Single();

        stroke.Code.ShouldBe(Code.Character);
        stroke.Character.ShouldBe(new Rune('a'));
        stroke.NativeCode.ShouldBe(97);
        stroke.Modifiers.ShouldBe(Modifiers.None);
        stroke.Action.ShouldBe(action);
    }

    /// <summary>
    /// Verifies event types combined with a modifier.
    /// </summary>
    [Theory]
    [InlineData("[97;2:1u", Modifiers.Shift, KeyAction.Press)]
    [InlineData("[97;5:2u", Modifiers.Control, KeyAction.Repeat)]
    [InlineData("[97;3:3u", Modifiers.Alt, KeyAction.Release)]
    public void Decode_WhenEventTypeAndModifierBothPresent_MapsBoth(
        string input,
        Modifiers modifiers,
        KeyAction action)
    {
        var stroke = Decode(Encoding.UTF8.GetBytes(input)).Strokes.Single();

        stroke.Code.ShouldBe(Code.Character);
        stroke.Modifiers.ShouldBe(modifiers);
        stroke.Action.ShouldBe(action);
    }

    // ── Named keys via CSI u ──────────────────────────────────────────────

    /// <summary>
    /// Verifies all named functional keys from the Kitty protocol map correctly.
    /// </summary>
    [Theory]
    [InlineData(27, Code.Escape)]
    [InlineData(13, Code.Enter)]
    [InlineData(9, Code.Tab)]
    [InlineData(127, Code.Backspace)]
    [InlineData(57358, Code.CapsLock)]
    [InlineData(57359, Code.ScrollLock)]
    [InlineData(57360, Code.NumLock)]
    [InlineData(57361, Code.PrintScreen)]
    [InlineData(57362, Code.Pause)]
    [InlineData(57363, Code.Menu)]
    public void Decode_WhenNamedKeyCode_MapsLogicalCode(int native, Code code)
    {
        var sink = Decode(Encoding.ASCII.GetBytes($"[{native}u"));

        sink.Strokes.Single().ShouldBe(
            new Stroke(code, null, native, Modifiers.None, KeyAction.Press));
    }

    /// <summary>
    /// Verifies named keys with modifiers preserve both code and modifier.
    /// </summary>
    [Theory]
    [InlineData(27, Code.Escape, 2, Modifiers.Shift)]
    [InlineData(13, Code.Enter, 5, Modifiers.Control)]
    [InlineData(9, Code.Tab, 3, Modifiers.Alt)]
    [InlineData(127, Code.Backspace, 6, Modifiers.Shift | Modifiers.Control)]
    public void Decode_WhenNamedKeyWithModifier_MapsBoth(
        int native,
        Code code,
        int modParam,
        Modifiers modifiers)
    {
        var sink = Decode(Encoding.ASCII.GetBytes($"[{native};{modParam}u"));

        var stroke = sink.Strokes.Single();
        stroke.Code.ShouldBe(code);
        stroke.NativeCode.ShouldBe(native);
        stroke.Modifiers.ShouldBe(modifiers);
        stroke.Action.ShouldBe(KeyAction.Press);
    }

    // ── Function keys via Kitty range ─────────────────────────────────────

    /// <summary>
    /// Verifies extended function keys F13..F35 map from the Kitty native range.
    /// </summary>
    [Theory]
    [InlineData(57376, Code.F13)]
    [InlineData(57377, Code.F14)]
    [InlineData(57378, Code.F15)]
    [InlineData(57379, Code.F16)]
    [InlineData(57380, Code.F17)]
    [InlineData(57381, Code.F18)]
    [InlineData(57382, Code.F19)]
    [InlineData(57383, Code.F20)]
    [InlineData(57384, Code.F21)]
    [InlineData(57385, Code.F22)]
    [InlineData(57386, Code.F23)]
    [InlineData(57387, Code.F24)]
    [InlineData(57388, Code.F25)]
    [InlineData(57389, Code.F26)]
    [InlineData(57390, Code.F27)]
    [InlineData(57391, Code.F28)]
    [InlineData(57392, Code.F29)]
    [InlineData(57393, Code.F30)]
    [InlineData(57394, Code.F31)]
    [InlineData(57395, Code.F32)]
    [InlineData(57396, Code.F33)]
    [InlineData(57397, Code.F34)]
    [InlineData(57398, Code.F35)]
    public void Decode_WhenFunctionKeyInKittyRange_MapsCorrectFKey(int native, Code code)
    {
        var sink = Decode(Encoding.ASCII.GetBytes($"[{native}u"));

        sink.Strokes.Single().ShouldBe(
            new Stroke(code, null, native, Modifiers.None, KeyAction.Press));
    }

    // ── Shifted and base layout keys ──────────────────────────────────────

    /// <summary>
    /// Verifies shifted alternate key identity is reported correctly.
    /// </summary>
    [Fact]
    public void Decode_WhenShiftedKeyPresent_MapsShiftedRune()
    {
        var stroke = Decode("[97:65u"u8.ToArray()).Strokes.Single();

        stroke.Code.ShouldBe(Code.Character);
        stroke.Character.ShouldBe(new Rune('a'));
        stroke.NativeCode.ShouldBe(97);
        stroke.Shifted.ShouldBe(new Rune('A'));
        stroke.BaseLayout.ShouldBeNull();
    }

    /// <summary>
    /// Verifies both shifted and base layout keys are reported.
    /// </summary>
    [Fact]
    public void Decode_WhenShiftedAndBaseLayoutPresent_MapsBothAlternates()
    {
        var stroke = Decode("[97:65:99u"u8.ToArray()).Strokes.Single();

        stroke.Code.ShouldBe(Code.Character);
        stroke.Character.ShouldBe(new Rune('a'));
        stroke.NativeCode.ShouldBe(97);
        stroke.Shifted.ShouldBe(new Rune('A'));
        stroke.BaseLayout.ShouldBe(new Rune('c'));
    }

    /// <summary>
    /// Verifies shifted key with a modifier and event type.
    /// </summary>
    [Fact]
    public void Decode_WhenShiftedKeyWithModifier_MapsBothShiftedAndModifier()
    {
        var stroke = Decode("[97:65;2:1u"u8.ToArray()).Strokes.Single();

        stroke.Code.ShouldBe(Code.Character);
        stroke.Character.ShouldBe(new Rune('a'));
        stroke.Shifted.ShouldBe(new Rune('A'));
        stroke.Modifiers.ShouldBe(Modifiers.Shift);
        stroke.Action.ShouldBe(KeyAction.Press);
    }

    // ── Associated text ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies a single associated text codepoint is emitted.
    /// </summary>
    [Fact]
    public void Decode_WhenSingleAssociatedText_EmitsStrokeAndText()
    {
        var sink = Decode("[97;1;97u"u8.ToArray());

        sink.Strokes.Single().ShouldBe(
            new Stroke(Code.Character, new Rune('a'), 97, Modifiers.None, KeyAction.Press));
        sink.Text.Single().Value.ShouldBe(new Rune('a'));
    }

    /// <summary>
    /// Verifies multiple associated text codepoints are emitted in order.
    /// </summary>
    [Fact]
    public void Decode_WhenMultipleAssociatedText_EmitsAllTextInOrder()
    {
        var sink = Decode("[97;1;97:98u"u8.ToArray());

        sink.Strokes.Single().ShouldBe(
            new Stroke(Code.Character, new Rune('a'), 97, Modifiers.None, KeyAction.Press));
        sink.Text.Select(static value => value.Value)
            .ShouldBe([new Rune('a'), new Rune('b')]);
    }

    /// <summary>
    /// Verifies associated text with high Unicode codepoints.
    /// </summary>
    [Fact]
    public void Decode_WhenAssociatedTextHasHighCodepoints_EmitsCorrectRunes()
    {
        var sink = Decode("[0;;229:946u"u8.ToArray());

        sink.Strokes.Single().Code.ShouldBe(Code.Unknown);
        sink.Text.Select(static value => value.Value)
            .ShouldBe([new Rune('å'), new Rune('β')]);
    }

    /// <summary>
    /// Verifies associated text with modifier present.
    /// </summary>
    [Fact]
    public void Decode_WhenAssociatedTextWithModifier_EmitsBoth()
    {
        var sink = Decode("[97;2;65u"u8.ToArray());

        var stroke = sink.Strokes.Single();
        stroke.Code.ShouldBe(Code.Character);
        stroke.Character.ShouldBe(new Rune('a'));
        stroke.Modifiers.ShouldBe(Modifiers.Shift);
        sink.Text.Single().Value.ShouldBe(new Rune('A'));
    }

    /// <summary>
    /// Verifies associated text carrying a control codepoint (e.g. Enter or Tab reported as
    /// text alongside their key event) is accepted, not rejected as malformed.
    /// </summary>
    [Theory]
    [InlineData("\u001b[97;1;13u", 13)]
    [InlineData("\u001b[97;1;9u", 9)]
    public void Decode_WhenAssociatedTextIsAControlCodepoint_EmitsText(string input, int codepoint)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Diagnostics.ShouldBeEmpty();
        sink.Text.Single().Value.ShouldBe(new Rune(codepoint));
    }

    // ── Edge cases and malformed ──────────────────────────────────────────

    /// <summary>
    /// Verifies modifier 0 is rejected as malformed (must be at least 1).
    /// </summary>
    [Fact]
    public void Decode_WhenModifierIsZero_ReportsMalformed()
    {
        var sink = Decode(Encoding.UTF8.GetBytes("[97;0u" + "x"));

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies event type 4 (out of range) is rejected.
    /// </summary>
    [Fact]
    public void Decode_WhenEventTypeOutOfRange_ReportsMalformed()
    {
        var sink = Decode(Encoding.UTF8.GetBytes("[97;1:4u" + "x"));

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies a codepoint exceeding the Unicode max scalar is rejected.
    /// </summary>
    [Fact]
    public void Decode_WhenCodepointExceedsMaxScalar_ReportsMalformed()
    {
        var sink = Decode(Encoding.UTF8.GetBytes("[1114112u" + "x"));

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies extra semicolons produce a diagnostic.
    /// </summary>
    [Fact]
    public void Decode_WhenExtraSemicolons_ReportsMalformed()
    {
        var sink = Decode(Encoding.UTF8.GetBytes("[97;;127u" + "x"));

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies too many semicolon-delimited groups produce a diagnostic.
    /// </summary>
    [Fact]
    public void Decode_WhenTooManyGroups_ReportsMalformed()
    {
        var sink = Decode(Encoding.UTF8.GetBytes("[97;1;65;66u" + "x"));

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies an empty key group (semicolon before modifier) is handled.
    /// </summary>
    [Fact]
    public void Decode_WhenKeyGroupIsEmpty_HandlesGracefully()
    {
        var sink = Decode("[;1u"u8.ToArray());

        // The decoder should either produce a stroke with code 0 or report a diagnostic;
        // verify that no exception is thrown and the decoder remains operational.
        (sink.Strokes.Count + sink.Diagnostics.Count).ShouldBeGreaterThan(0);
    }

    // ── Fragmentation ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifies a Kitty character event decodes identically at every byte split.
    /// </summary>
    [Fact]
    public void Decode_WhenBareCharacterIsFragmented_MapsAtEverySplit()
    {
        var bytes = "[97u"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.Count.ShouldBe(1, $"split {split}");
            sink.Strokes[0].Code.ShouldBe(Code.Character, $"split {split}");
            sink.Strokes[0].Character.ShouldBe(new Rune('a'), $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies a named-key Kitty event decodes identically at every byte split.
    /// </summary>
    [Fact]
    public void Decode_WhenNamedKeyIsFragmented_MapsAtEverySplit()
    {
        var bytes = "[57358u"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.Count.ShouldBe(1, $"split {split}");
            sink.Strokes[0].Code.ShouldBe(Code.CapsLock, $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies a modifier+event Kitty event decodes at every byte split.
    /// </summary>
    [Fact]
    public void Decode_WhenModifierEventIsFragmented_MapsAtEverySplit()
    {
        var bytes = "[97;5:2u"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.Count.ShouldBe(1, $"split {split}");
            sink.Strokes[0].Modifiers.ShouldBe(Modifiers.Control, $"split {split}");
            sink.Strokes[0].Action.ShouldBe(KeyAction.Repeat, $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies an associated-text Kitty event decodes at every byte split.
    /// </summary>
    [Fact]
    public void Decode_WhenAssociatedTextIsFragmented_MapsAtEverySplit()
    {
        var bytes = "[97;1;97:98u"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.Count.ShouldBe(1, $"split {split}");
            sink.Text.Count.ShouldBe(2, $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    // ── Recovery after malformed ──────────────────────────────────────────

    /// <summary>
    /// Verifies each malformed case emits exactly one diagnostic and preserves
    /// subsequent input.
    /// </summary>
    [Theory]
    [InlineData("[97;0u")]
    [InlineData("[97;1:4u")]
    [InlineData("[1114112u")]
    [InlineData("[97;;127u")]
    [InlineData("[97;1;65;66u")]
    public void Decode_WhenMalformedFollowedByPlainText_RecoversFully(string malformed)
    {
        var bytes = Encoding.UTF8.GetBytes(malformed + "xyz");
        var sink = Decode(bytes);

        sink.Diagnostics.Count.ShouldBe(1);

        // Recovery must produce at least the three plain characters.
        var textRunes = sink.Text.Select(static t => t.Value).ToList();
        textRunes.ShouldContain(new Rune('x'));
        textRunes.ShouldContain(new Rune('y'));
        textRunes.ShouldContain(new Rune('z'));
    }

    /// <summary>
    /// Verifies recovery from malformed Kitty followed by a valid Kitty event.
    /// </summary>
    [Fact]
    public void Decode_WhenMalformedFollowedByValidKitty_RecoversBoth()
    {
        var bytes = "[97;0u[98u"u8.ToArray();
        var sink = Decode(bytes);

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Strokes.ShouldContain(
            new Stroke(Code.Character, new Rune('b'), 98, Modifiers.None, KeyAction.Press));
    }

    /// <summary>
    /// Verifies back-to-back malformed events each produce a diagnostic.
    /// </summary>
    [Fact]
    public void Decode_WhenConsecutiveMalformedEvents_ReportsEachSeparately()
    {
        var bytes = Encoding.UTF8.GetBytes("[97;0u[1114112u" + "x");
        var sink = Decode(bytes);

        sink.Diagnostics.Count.ShouldBe(2);
        sink.Text.Select(static t => t.Value).ShouldContain(new Rune('x'));
    }

    private static RecordingInputSink Decode(byte[] bytes)
    {
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink))
        {
            decoder.Decode(bytes);
            decoder.Complete();
        }

        return sink;
    }

    #endregion

    #region Legacy VT keyboard

    /// <summary>
    /// Verifies C0 and DEL keyboard bytes map to named keys.
    /// </summary>
    [Theory]
    [InlineData("\r", Code.Enter)]
    [InlineData("\n", Code.Enter)]
    [InlineData("\t", Code.Tab)]
    [InlineData("\b", Code.Backspace)]
    [InlineData("\u007f", Code.Backspace)]
    public void Decode_WhenByteIsNamedControl_EmitsNamedStroke(string input, Code code)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Strokes.ShouldBe(
        [
            new Stroke(code, null, 0, Modifiers.None, KeyAction.Press)
        ]);
    }

    /// <summary>
    /// Verifies representative CSI, tilde, Shift-Tab, and SS3 keys at every split.
    /// </summary>
    [Theory]
    [InlineData("\u001b[A", Code.Up, Modifiers.None, 0)]
    [InlineData("\u001b[1;2A", Code.Up, Modifiers.Shift, 0)]
    [InlineData("\u001b[1;3D", Code.Left, Modifiers.Alt, 0)]
    [InlineData("\u001b[1;5C", Code.Right, Modifiers.Control, 0)]
    [InlineData("\u001b[1;6H", Code.Home, Modifiers.Shift | Modifiers.Control, 0)]
    [InlineData("\u001b[F", Code.End, Modifiers.None, 0)]
    [InlineData("\u001b[2~", Code.Insert, Modifiers.None, 2)]
    [InlineData("\u001b[3~", Code.Delete, Modifiers.None, 3)]
    [InlineData("\u001b[5~", Code.PageUp, Modifiers.None, 5)]
    [InlineData("\u001b[6~", Code.PageDown, Modifiers.None, 6)]
    [InlineData("\u001b[15;2~", Code.F5, Modifiers.Shift, 15)]
    [InlineData("\u001b[Z", Code.Tab, Modifiers.Shift, 0)]
    [InlineData("\u001bOP", Code.F1, Modifiers.None, 0)]
    [InlineData("\u001bOQ", Code.F2, Modifiers.None, 0)]
    [InlineData("\u001bOR", Code.F3, Modifiers.None, 0)]
    [InlineData("\u001bOS", Code.F4, Modifiers.None, 0)]
    public void Decode_WhenLegacyKeyIsFragmented_MapsAtEverySplit(
        string input,
        Code code,
        Modifiers modifiers,
        int nativeCode)
    {
        var bytes = Encoding.UTF8.GetBytes(input);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.ShouldBe(
                [
                    new Stroke(code, null, nativeCode, modifiers, KeyAction.Press)
                ], $"split {split}");
            sink.Text.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies the complete VT function-key range maps to stable logical codes.
    /// </summary>
    [Theory]
    [InlineData(11, Code.F1)]
    [InlineData(12, Code.F2)]
    [InlineData(13, Code.F3)]
    [InlineData(14, Code.F4)]
    [InlineData(15, Code.F5)]
    [InlineData(17, Code.F6)]
    [InlineData(18, Code.F7)]
    [InlineData(19, Code.F8)]
    [InlineData(20, Code.F9)]
    [InlineData(21, Code.F10)]
    [InlineData(23, Code.F11)]
    [InlineData(24, Code.F12)]
    public void Decode_WhenTildeFunctionKeyIsKnown_MapsLogicalCode(int native, Code code)
    {
        var sink = Decode(Encoding.UTF8.GetBytes($"\u001b[{native}~"));

        sink.Strokes.ShouldBe(
        [
            new Stroke(code, null, native, Modifiers.None, KeyAction.Press)
        ]);
    }

    /// <summary>
    /// Verifies unknown valid CSI keys remain typed and adjacent input survives.
    /// </summary>
    [Fact]
    public void Decode_WhenCsiKeyIsUnknown_EmitsUnknownAndRecovers()
    {
        var sink = Decode("\u001b[99~x"u8.ToArray());

        sink.Strokes[0].ShouldBe(
            new Stroke(Code.Unknown, null, 99, Modifiers.None, KeyAction.Press));
        sink.Strokes[1].Code.ShouldBe(Code.Character);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies malformed key parameters report once and preserve the next key.
    /// </summary>
    [Fact]
    public void Decode_WhenCsiParametersAreMalformed_ReportsAndRecovers()
    {
        var sink = Decode("\u001b[1:x\u001b[B"u8.ToArray());

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Strokes.ShouldContain(static item => item.Code == Code.Down);
    }

    /// <summary>
    /// Verifies a colon or an extra semicolon-delimited field inside the legacy CSI arrow-key
    /// parameters reports Malformed instead of silently parsing a truncated prefix - the trailing
    /// content would otherwise be dropped unread, turning a corrupted or private-use sequence into
    /// a plausible synthetic stroke.
    /// </summary>
    [Theory]
    [InlineData("\u001b[1:2A")]
    [InlineData("\u001b[:5A")]
    [InlineData("\u001b[1;2;9A")]
    [InlineData("\u001b[1;2:2;9A")]
    public void Decode_WhenCsiArrowKeyHasExtraField_ReportsMalformedAndRecovers(string malformed)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(malformed + "\u001b[B"));

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Strokes.ShouldNotContain(static item => item.Code == Code.Up);
        sink.Strokes.ShouldContain(static item => item.Code == Code.Down);
    }

    /// <summary>
    /// Verifies adjacent escape sequences produce distinct ordered strokes.
    /// </summary>
    [Fact]
    public void Decode_WhenKeysAreAdjacent_PreservesOrder()
    {
        var sink = Decode("\u001b[A\u001b[B\u001b[C\u001b[D"u8.ToArray());

        sink.Strokes.Select(static item => item.Code)
            .ShouldBe([Code.Up, Code.Down, Code.Right, Code.Left]);
    }

    /// <summary>
    /// Verifies an interrupted SS3 prefix reports once and cannot consume later text.
    /// </summary>
    [Fact]
    public void Decode_WhenSs3IsInterrupted_ReportsAndRecovers()
    {
        var sink = Decode("\u001bO\u001b[Ax"u8.ToArray());

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Strokes.Select(static item => item.Code)
            .ShouldBe([Code.Up, Code.Character]);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    // ──────────────────────────────────────────────────────────────────
    //  Kitty event-type extension: colon-separated modifier:event_type
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies CSI arrow/nav keys with Kitty event types (modifier:event_type) decode correctly.
    /// These are sent when the terminal enables <c>KittyEnhancement.EventTypes</c>.
    /// </summary>
    [Theory]
    [InlineData("[1;1:1A", Code.Up, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:2A", Code.Up, Modifiers.None, KeyAction.Repeat)]
    [InlineData("[1;1:3A", Code.Up, Modifiers.None, KeyAction.Release)]
    [InlineData("[1;1:1B", Code.Down, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:1C", Code.Right, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:1D", Code.Left, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:1H", Code.Home, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:1F", Code.End, Modifiers.None, KeyAction.Press)]
    public void Decode_WhenCsiKeyHasKittyEventType_MapsCodeAndAction(
        string input,
        Code code,
        Modifiers modifiers,
        KeyAction action)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Strokes.ShouldBe(
        [
            new Stroke(code, null, 0, modifiers, action)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies CSI keys with both modifiers and Kitty event types decode correctly.
    /// Format: <c>CSI 1;modifier:event_type final</c>.
    /// </summary>
    [Theory]
    [InlineData("[1;2:1A", Code.Up, Modifiers.Shift, KeyAction.Press)]
    [InlineData("[1;3:1D", Code.Left, Modifiers.Alt, KeyAction.Press)]
    [InlineData("[1;5:1C", Code.Right, Modifiers.Control, KeyAction.Press)]
    [InlineData("[1;5:3C", Code.Right, Modifiers.Control, KeyAction.Release)]
    [InlineData("[1;2:2B", Code.Down, Modifiers.Shift, KeyAction.Repeat)]
    [InlineData("[1;3:3A", Code.Up, Modifiers.Alt, KeyAction.Release)]
    [InlineData("[1;6:1H", Code.Home, Modifiers.Shift | Modifiers.Control, KeyAction.Press)]
    [InlineData("[1;7:1F", Code.End, Modifiers.Alt | Modifiers.Control, KeyAction.Press)]
    public void Decode_WhenCsiKeyHasModifierAndEventType_MapsBoth(
        string input,
        Code code,
        Modifiers modifiers,
        KeyAction action)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Strokes.ShouldBe(
        [
            new Stroke(code, null, 0, modifiers, action)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies CSI keys with modifiers but no event type still default to Press.
    /// </summary>
    [Theory]
    [InlineData("[1;2A", Code.Up, Modifiers.Shift)]
    [InlineData("[1;3D", Code.Left, Modifiers.Alt)]
    [InlineData("[1;5C", Code.Right, Modifiers.Control)]
    [InlineData("[1;6F", Code.End, Modifiers.Shift | Modifiers.Control)]
    public void Decode_WhenCsiKeyHasModifierWithoutEventType_DefaultsToPress(
        string input,
        Code code,
        Modifiers modifiers)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Strokes.ShouldBe(
        [
            new Stroke(code, null, 0, modifiers, KeyAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies plain CSI arrow keys with no parameters default to no modifiers and Press.
    /// </summary>
    [Theory]
    [InlineData("[A", Code.Up)]
    [InlineData("[B", Code.Down)]
    [InlineData("[C", Code.Right)]
    [InlineData("[D", Code.Left)]
    [InlineData("[H", Code.Home)]
    [InlineData("[F", Code.End)]
    public void Decode_WhenCsiKeyHasNoParameters_DefaultsToNonePress(
        string input,
        Code code)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Strokes.ShouldBe(
        [
            new Stroke(code, null, 0, Modifiers.None, KeyAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies SS3-encoded arrow and function keys (application mode) still decode correctly.
    /// </summary>
    [Theory]
    [InlineData("OA", Code.Up)]
    [InlineData("OB", Code.Down)]
    [InlineData("OC", Code.Right)]
    [InlineData("OD", Code.Left)]
    [InlineData("OP", Code.F1)]
    [InlineData("OQ", Code.F2)]
    [InlineData("OR", Code.F3)]
    [InlineData("OS", Code.F4)]
    public void Decode_WhenSs3KeyIsReceived_MapsCodeWithPress(
        string input,
        Code code)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Strokes.ShouldBe(
        [
            new Stroke(code, null, 0, Modifiers.None, KeyAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies an unrecognized CSI final byte reports Unsupported without emitting a stroke,
    /// since CSI participates in an extensible dispatch chain that must let later handlers
    /// (terminfo KeyMap, the ANSI grammar fallback) claim what this table does not.
    /// </summary>
    [Fact]
    public void Decode_WhenCsiFinalByteIsUnrecognized_ReportsUnsupportedWithoutAStroke()
    {
        var sink = Decode(Encoding.UTF8.GetBytes("[X"));

        sink.Strokes.ShouldBeEmpty();
        sink.Diagnostics.ShouldContain(static item => item.Code == DiagnosticCode.Unsupported);
    }

    /// <summary>
    /// Verifies an unrecognized SS3 final byte still emits a Code.Unknown stroke carrying the
    /// native byte, since SS3 has no further fallback handler once its table is exhausted,
    /// unlike CSI.
    /// </summary>
    [Fact]
    public void Decode_WhenSs3FinalByteIsUnrecognized_EmitsUnknownStrokeWithNativeByte()
    {
        var sink = Decode(Encoding.UTF8.GetBytes("OX"));

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Unknown, null, (byte) 'X', Modifiers.None, KeyAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies CSI keys with Kitty event types decode identically at every byte split.
    /// </summary>
    [Theory]
    [InlineData("[1;1:1A", Code.Up, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:2A", Code.Up, Modifiers.None, KeyAction.Repeat)]
    [InlineData("[1;1:3A", Code.Up, Modifiers.None, KeyAction.Release)]
    [InlineData("[1;5:1C", Code.Right, Modifiers.Control, KeyAction.Press)]
    [InlineData("[1;2:3D", Code.Left, Modifiers.Shift, KeyAction.Release)]
    public void Decode_WhenCsiEventTypeIsFragmented_MapsAtEverySplit(
        string input,
        Code code,
        Modifiers modifiers,
        KeyAction action)
    {
        var bytes = Encoding.UTF8.GetBytes(input);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.ShouldBe(
                [
                    new Stroke(code, null, 0, modifiers, action)
                ], $"split {split}");
            sink.Text.ShouldBeEmpty($"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies tilde keys with colon-separated event types are reported as malformed
    /// because the tilde path uses <c>TryReadParameters</c> which rejects colons.
    /// </summary>
    [Theory]
    [InlineData("[15;1:1~")]
    [InlineData("[3;2:1~")]
    public void Decode_WhenTildeKeyHasEventTypeColon_ReportsMalformed(string input)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Diagnostics.Count.ShouldBe(1);
    }

    /// <summary>
    /// Verifies that out-of-range Kitty event types are rejected.
    /// Event type 0 is invalid and event type 4+ is out of range.
    /// </summary>
    [Theory]
    [InlineData("[1;1:0A")]
    [InlineData("[1;1:4A")]
    [InlineData("[1;1:99A")]
    public void Decode_WhenKittyEventTypeIsOutOfRange_ReportsMalformed(string input)
    {
        var sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Strokes.ShouldNotContain(static item => item.Code == Code.Up);
    }

    /// <summary>
    /// Verifies adjacent CSI sequences with Kitty event types produce distinct ordered strokes.
    /// </summary>
    [Fact]
    public void Decode_WhenKittyEventTypeKeysAreAdjacent_PreservesOrder()
    {
        var sink = Decode("[1;1:1A[1;1:1B"u8.ToArray());

        sink.Strokes.Select(static item => item.Code)
            .ShouldBe([Code.Up, Code.Down]);
        sink.Strokes.ShouldAllBe(static item => item.Action == KeyAction.Press);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a press followed by a release of the same key produces the correct action sequence.
    /// </summary>
    [Fact]
    public void Decode_WhenKittyPressAndRelease_ProducesCorrectActions()
    {
        var sink = Decode("[1;1:1A[1;1:3A"u8.ToArray());

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Up, null, 0, Modifiers.None, KeyAction.Press),
            new Stroke(Code.Up, null, 0, Modifiers.None, KeyAction.Release)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a full press-repeat-release lifecycle for a single key.
    /// </summary>
    [Fact]
    public void Decode_WhenKittyPressRepeatRelease_ProducesFullLifecycle()
    {
        var sink = Decode(
            "[1;1:1A[1;1:2A[1;1:2A[1;1:3A"u8.ToArray());

        sink.Strokes.Select(static item => item.Action)
            .ShouldBe([
                KeyAction.Press,
                KeyAction.Repeat,
                KeyAction.Repeat,
                KeyAction.Release
            ]);
        sink.Strokes.ShouldAllBe(static item => item.Code == Code.Up);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a malformed Kitty event type followed by a valid key recovers correctly.
    /// </summary>
    [Fact]
    public void Decode_WhenMalformedEventTypeFollowedByValidKey_RecoversAfterDiagnostic()
    {
        var sink = Decode("[1;1:0A[B"u8.ToArray());

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Strokes.ShouldContain(static item => item.Code == Code.Down);
    }

    /// <summary>
    /// Verifies mixing plain, modifier-only, and event-type CSI keys in a single stream.
    /// </summary>
    [Fact]
    public void Decode_WhenMixingPlainAndEventTypeKeys_DecodesAll()
    {
        var sink = Decode(
            "[A[1;2A[1;1:1C"u8.ToArray());

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Up, null, 0, Modifiers.None, KeyAction.Press),
            new Stroke(Code.Up, null, 0, Modifiers.Shift, KeyAction.Press),
            new Stroke(Code.Right, null, 0, Modifiers.None, KeyAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies CSI arrow keys with event types decode when UseAnsiKeyGrammar is false,
    /// simulating a real terminal profile (like Kitty) where the ANSI grammar is disabled
    /// and only the key map and standard CSI handlers are active.
    /// </summary>
    [Theory]
    [InlineData("[1;1:1A", Code.Up, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:3A", Code.Up, Modifiers.None, KeyAction.Release)]
    [InlineData("[1;1:1B", Code.Down, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:1C", Code.Right, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:1D", Code.Left, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:1H", Code.Home, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;1:1F", Code.End, Modifiers.None, KeyAction.Press)]
    [InlineData("[1;5:1C", Code.Right, Modifiers.Control, KeyAction.Press)]
    [InlineData("[A", Code.Up, Modifiers.None, KeyAction.Press)]
    [InlineData("[B", Code.Down, Modifiers.None, KeyAction.Press)]
    public void Decode_WhenAnsiGrammarIsDisabled_StillDecodesCsiKeys(
        string input,
        Code code,
        Modifiers modifiers,
        KeyAction action)
    {
        var sink = new RecordingInputSink();
        var options = InputOptions.Default with { UseAnsiKeyGrammar = false };

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode(Encoding.UTF8.GetBytes(input));
            decoder.Complete();
        }

        sink.Strokes.ShouldContain(
            item => item.Code == code && item.Modifiers == modifiers && item.Action == action);
        sink.Diagnostics.ShouldBeEmpty();
    }

    #endregion

    #region Bracketed paste

    /// <summary>Verifies the small immutable payload wrapper avoids reference allocation.</summary>
    [Fact]
    public void Type_WhenPasteIsInspected_IsValueType() =>
        typeof(Paste).IsValueType.ShouldBeTrue();

    /// <summary>Verifies the valid default wrapper represents an empty paste.</summary>
    [Fact]
    public void Utf8_WhenPasteIsDefault_IsEmpty() =>
        default(Paste).Utf8.IsEmpty.ShouldBeTrue();

    /// <summary>Verifies the wrapper isolates its payload from caller mutation.</summary>
    [Fact]
    public void Constructor_WhenSourceChanges_PreservesCopiedPayload()
    {
        var source = "abc"u8.ToArray();
        var paste = new Paste(source);

        source[0] = (byte) 'z';

        paste.Utf8.ToArray().ShouldBe("abc"u8.ToArray());
    }

    /// <summary>
    /// Verifies empty, multiline, Unicode, and marker-like payload at every split.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("one\ntwo")]
    [InlineData("café 👩")]
    [InlineData("a\u001b[20xb")]
    public void Decode_WhenPasteIsFragmented_PreservesExactPayload(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes($"\u001b[200~{payload}\u001b[201~x");

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            Encoding.UTF8.GetString(sink.Pastes.Single().Utf8.Span)
                .ShouldBe(payload, $"split {split}");
            sink.Text.Single().Value.ShouldBe(new Rune('x'), $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies malformed UTF-8 is normalized to replacement scalars.
    /// </summary>
    [Fact]
    public void Decode_WhenPasteUtf8IsInvalid_EmitsValidOwnedUtf8()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);
        decoder.Decode("\u001b[200~"u8);
        decoder.Decode([0xF0, 0x28, 0x8C, 0x28]);
        decoder.Decode("\u001b[201~"u8);

        Encoding.UTF8.GetString(sink.Pastes.Single().Utf8.Span)
            .ShouldBe("�(�(");
    }

    /// <summary>
    /// Verifies overflow discards through the terminator and recovers once.
    /// </summary>
    [Fact]
    public void Decode_WhenPasteExceedsLimit_DiscardsAndRecovers()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink, new InputOptions { MaxPasteBytes = 3 });

        decoder.Decode("\u001b[200~abcdef\u001b[201~x"u8);
        decoder.Complete();

        sink.Pastes.ShouldBeEmpty();
        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies truncation reports once without publishing partial data.
    /// </summary>
    [Fact]
    public void Complete_WhenPasteIsTruncated_ReportsAndDropsPayload()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode("\u001b[200~payload\u001b[20"u8);
        decoder.Complete();

        sink.Pastes.ShouldBeEmpty();
        sink.Diagnostics.Count.ShouldBe(1);
    }

    /// <summary>
    /// Verifies every proper terminator prefix remains pending at end-of-stream.
    /// </summary>
    [Fact]
    public void Complete_WhenPasteEndsWithTerminatorPrefix_ReportsEveryPrefix()
    {
        var end = "\u001b[201~"u8.ToArray();

        for (var length = 1; length < end.Length; length++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode("\u001b[200~payload"u8);
            decoder.Decode(end.AsSpan(0, length));
            decoder.Complete();

            sink.Pastes.ShouldBeEmpty($"prefix {length}");
            sink.Diagnostics.Count.ShouldBe(1, $"prefix {length}");
        }
    }

    /// <summary>
    /// Verifies a large overflowing paste remains bounded and recovers at its terminator.
    /// </summary>
    [Fact]
    public void Decode_WhenUnterminatedPasteIsLarge_RetainsOnlyConfiguredLimit()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink, new InputOptions { MaxPasteBytes = 16 });
        decoder.Decode("\u001b[200~"u8);
        var chunk = new byte[1024 * 1024];
        chunk.AsSpan().Fill((byte) 'x');

        decoder.Decode(chunk);
        decoder.Decode("\u001b[201~y"u8);
        decoder.Complete();

        sink.Pastes.ShouldBeEmpty();
        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('y'));
    }

    /// <summary>
    /// Verifies a delivered paste remains stable while later paste storage is reused.
    /// </summary>
    [Fact]
    public void Decode_WhenMultiplePastesArrive_PreservesPriorOwnership()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode("\u001b[200~one\u001b[201~\u001b[200~two\u001b[201~"u8);

        Encoding.UTF8.GetString(sink.Pastes[0].Utf8.Span).ShouldBe("one");
        Encoding.UTF8.GetString(sink.Pastes[1].Utf8.Span).ShouldBe("two");
    }

    #endregion

    #region Kitty clipboard routing (via InputDecoder)

    /// <summary>Verifies a complete OSC 5522 reply is parsed and delivered, at every byte split, so
    /// the routing holds for a reply that arrives fragmented across reads rather than only for one
    /// that lands whole.</summary>
    [Fact]
    public void Decode_WhenKittyClipboardReplyArrives_DeliversTypedPacketAtEverySplit()
    {
        var sequence = "]5522;type=read:status=OK:id=req-1\\"u8.ToArray();

        for (var split = 0; split <= sequence.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using InputDecoder decoder = new(sink, InputOptions.Default);

            decoder.Decode(sequence.AsSpan(0, split));
            decoder.Decode(sequence.AsSpan(split));
            decoder.Complete();

            sink.KittyClipboardPackets.Count.ShouldBe(1, $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies a payload-carrying reply routes with its data intact, so the hop is not
    /// only recognizing the prefix but carrying the packet body through.</summary>
    [Fact]
    public void Decode_WhenKittyClipboardReplyCarriesData_DeliversThePayload()
    {
        var sink = new RecordingProtocolSink();
        using InputDecoder decoder = new(sink, InputOptions.Default);

        decoder.Decode("]5522;type=read:status=DATA:id=req-1;aGVsbG8=\\"u8);
        decoder.Complete();

        sink.KittyClipboardPackets.Count.ShouldBe(1);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>The counter-case that keeps the assertions above honest: a neighbouring OSC family
    /// must not be routed to the clipboard sink. Matching on a prefix rather than the exact
    /// parameter would swallow OSC 52 and every other numeric family beginning with these digits.
    /// </summary>
    [Theory]
    [InlineData("]52;c;aGVsbG8=\\")]
    [InlineData("]5;1\\")]
    [InlineData("]55220;type=read\\")]
    public void Decode_WhenAnotherOscFamilyArrives_DoesNotReachTheClipboardSink(string sequence)
    {
        var sink = new RecordingProtocolSink();
        using InputDecoder decoder = new(sink, InputOptions.Default);

        decoder.Decode(Encoding.UTF8.GetBytes(sequence));
        decoder.Complete();

        sink.KittyClipboardPackets.ShouldBeEmpty();
    }

    /// <summary>Verifies an OSC 52 reply with no protocol sink reports Unsupported instead of
    /// falling through to another handler.</summary>
    [Fact]
    public void Route_WhenOsc52ReplyArrivesWithoutProtocolSink_ReportsUnsupported()
    {
        var sink = new RecordingInputSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("]52;c;aGVsbG8=\\"u8);

        sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCode.Unsupported);
    }

    /// <summary>Verifies a Kitty OSC 5522 packet with no protocol sink reports Unsupported instead
    /// of falling through to another handler.</summary>
    [Fact]
    public void Route_WhenKittyClipboardPacketArrivesWithoutProtocolSink_ReportsUnsupported()
    {
        var sink = new RecordingInputSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("]5522;type=read:status=OK\\"u8);

        sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCode.Unsupported);
    }

    #endregion

    #region Terminal-description key precedence

    /// <summary>Verifies a described CSI spelling overrides the ANSI legacy meaning at every split.</summary>
    [Fact]
    public void Decode_WhenDescriptionChangesCsiMeaning_UsesDescriptionAtEverySplit()
    {
        var sequence = "\u001b[99~"u8.ToArray();
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding(sequence, Code.F63)]),
            useAnsiKeyGrammar: false);

        for (var split = 0; split <= sequence.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(sequence.AsSpan(0, split));
            decoder.Decode(sequence.AsSpan(split));
            decoder.Complete();

            sink.Strokes.ShouldBe(
            [
                new Stroke(Code.F63, null, 0, Modifiers.None, KeyAction.Press)
            ], $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies typed protocol and enhanced-input grammars retain precedence over a described key.</summary>
    [Theory]
    [InlineData("\u001b[?1;2c")]
    [InlineData("\u001b[?2026;1$y")]
    [InlineData("\u001b[97u")]
    [InlineData("\u001b[I")]
    [InlineData("\u001b[<0;1;1M")]
    public void Decode_WhenDescriptionConflictsWithRegisteredGrammar_RegisteredGrammarWins(string sequence)
    {
        var bytes = Encoding.ASCII.GetBytes(sequence);
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding(bytes, Code.F63)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingProtocolSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode(bytes);
            decoder.Complete();
        }

        sink.Strokes.ShouldNotContain(static value => value.Code == Code.F63);
        (sink.Responses.Count + sink.Focus.Count + sink.Pointers.Count + sink.Strokes.Count)
            .ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies an active paste consumes its terminator before described-key matching.</summary>
    [Fact]
    public void Decode_WhenPasteTerminatorIsAlsoDescribed_PasteTerminatorWins()
    {
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding("\u001b[201~"u8, Code.F63)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode("\u001b[200~payload\u001b[201~"u8);
            decoder.Complete();
        }

        sink.Pastes.ShouldHaveSingleItem().Utf8.Span.SequenceEqual("payload"u8).ShouldBeTrue();
        sink.Strokes.ShouldNotContain(static value => value.Code == Code.F63);
    }

    /// <summary>Verifies a described non-signature prefix uses longest match and replays mismatch bytes once.</summary>
    [Fact]
    public void Decode_WhenDescriptionPrefixesOverlap_UsesLongestMatchAndReplaysMismatchOnce()
    {
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F62),
                new KeyBinding([0xff, 0xfe], Code.F63)
            ]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0xff, (byte) 'x']);
            decoder.Complete();
        }

        sink.Strokes.Select(static value => value.Code).ShouldBe([Code.F62, Code.Character]);
        sink.Text.Select(static value => value.Value).ShouldBe([new Rune('x')]);
    }

    /// <summary>Verifies equivalent parser signatures cannot publish conflicting meanings.</summary>
    [Fact]
    public void Constructor_WhenEquivalentSignaturesConflict_Throws()
    {
        KeyBinding[] bindings =
        [
            new("\u001b[A"u8, Code.Up),
            new([0x9b, (byte) 'A'], Code.Down)
        ];

        _ = Should.Throw<ArgumentException>(() => new KeyMap(bindings));
    }

    /// <summary>Verifies a non-ANSI profile does not inherit an undescribed xterm key.</summary>
    [Fact]
    public void Decode_WhenProfileDoesNotDescribeLegacySequence_DoesNotApplyAnsiGrammar()
    {
        var options = InputOptions.Default.WithKeyMap(KeyMap.Empty, useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode("\u001b[99~"u8);
            decoder.Complete();
        }

        sink.Strokes.ShouldNotContain(static value => value.NativeCode == 99);
    }

    /// <summary>Verifies a lone Escape retains the configured finite deadline with described keys active.</summary>
    [Fact]
    public void ExpireEscape_WhenDescriptionIsActive_StillUsesFiniteDeadline()
    {
        var time = new ManualTimeProvider();
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding("\u001b[A"u8, Code.Up)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink, options, time);

        decoder.Decode("\u001b"u8);
        decoder.ExpireEscape().ShouldBeFalse();
        time.Advance(options.EscapeTimeout);

        decoder.ExpireEscape().ShouldBeTrue();
        sink.Strokes.Single().Code.ShouldBe(Code.Escape);
    }

    /// <summary>Verifies an Escape key with intermediates maps at every transport split.</summary>
    [Fact]
    public void Decode_WhenDescriptionUsesEscapeIntermediates_MapsAtEverySplit()
    {
        var sequence = "\u001b(B"u8.ToArray();
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding(sequence, Code.F63)]),
            useAnsiKeyGrammar: false);

        AssertDescribedAtEverySplit(sequence, Code.F63, options);
    }

    /// <summary>Verifies an unmatched Escape-intermediate signature reports once and recovers text.</summary>
    [Fact]
    public void Decode_WhenEscapeIntermediateIsUndescribed_ReportsAndRecovers()
    {
        var options = InputOptions.Default.WithKeyMap(KeyMap.Empty, useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode("\u001b(Bx"u8);
            decoder.Complete();
        }

        sink.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(SequenceKind.Escape);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies seven-bit and eight-bit SS3 spellings share one structural identity.</summary>
    [Fact]
    public void Constructor_WhenSevenAndEightBitSs3MeaningsConflict_Throws()
    {
        KeyBinding[] bindings =
        [
            new("\u001bOA"u8, Code.Up),
            new([0x8f, (byte) 'A'], Code.Down)
        ];

        _ = Should.Throw<ArgumentException>(() => new KeyMap(bindings));
    }

    /// <summary>Verifies described eight-bit SS3 input maps at every split.</summary>
    [Fact]
    public void Decode_WhenDescriptionUsesEightBitSs3_MapsAtEverySplit()
    {
        var sequence = new byte[] { 0x8f, (byte) 'A' };
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding(sequence, Code.Up)]),
            useAnsiKeyGrammar: false);

        options.KeyMap.FallbackBindings.ShouldBeEmpty();
        AssertDescribedAtEverySplit(sequence, Code.Up, options);
    }

    /// <summary>Verifies an unmatched eight-bit SS3 final reports once and recovers following text.</summary>
    [Fact]
    public void Decode_WhenEightBitSs3FinalIsUndescribed_ReportsAndRecovers()
    {
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding([0x8f, (byte) 'A'], Code.Up)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0x8f, (byte) 'Z', (byte) 'x']);
            decoder.Complete();
        }

        sink.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(SequenceKind.Escape);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies representative structural families decode identically at every boundary.</summary>
    [Fact]
    public void Decode_WhenStructuralDescriptionFamiliesAreFragmented_MapAtEverySplit()
    {
        (byte[] Sequence, Code Code)[] cases =
        [
            ([0x08], Code.Backspace),
            ("\u001b(B"u8.ToArray(), Code.F60),
            ("\u001b[91~"u8.ToArray(), Code.F61),
            ([0x9b, (byte) '9', (byte) '2', (byte) '~'], Code.F62),
            ("\u001bOA"u8.ToArray(), Code.Up),
            ([0x8f, (byte) 'B'], Code.Down)
        ];

        foreach (var item in cases)
        {
            var options = InputOptions.Default.WithKeyMap(
                new KeyMap([new KeyBinding(item.Sequence, item.Code)]),
                useAnsiKeyGrammar: false);

            AssertDescribedAtEverySplit(item.Sequence, item.Code, options);
        }
    }

    /// <summary>Verifies valid UTF-8 continuation bytes never become described C1 introducers.</summary>
    [Fact]
    public void Decode_WhenUtf8ContainsC1ContinuationBytes_PreservesUnicodeAtEverySplit()
    {
        var input = new byte[] { 0xc2, 0x8f, 0xc2, 0x9b };
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0x8f, (byte) 'A'], Code.Up),
                new KeyBinding([0x9b, (byte) '9', (byte) '2', (byte) '~'], Code.F63)
            ]),
            useAnsiKeyGrammar: false);

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(input.AsSpan(0, split));
            decoder.Decode(input.AsSpan(split));
            decoder.Complete();

            sink.Text.Select(static value => value.Value)
                .ShouldBe([new Rune(0x8f), new Rune(0x9b)], $"split {split}");
            sink.Strokes.Select(static value => value.Code)
                .ShouldBe([Code.Character, Code.Character], $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies an unmatched described eight-bit CSI family reports and recovers text.</summary>
    [Fact]
    public void Decode_WhenEightBitCsiSignatureIsUndescribed_ReportsAndRecovers()
    {
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding([0x9b, (byte) '9', (byte) '2', (byte) '~'], Code.F63)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0x9b, (byte) '9', (byte) '3', (byte) '~', (byte) 'x']);
            decoder.Complete();
        }

        sink.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(SequenceKind.Csi);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies explicit parser-wide C1 policy remains independent of described keys.</summary>
    [Fact]
    public void Decode_WhenCallerExplicitlyEnablesC1WithoutMap_PreservesParserControlSemantics()
    {
        var configured = InputOptions.Default with
        {
            ParserLimits = ParserLimits.Default with { AcceptEightBitControls = true }
        };
        var options = configured.WithKeyMap(KeyMap.Empty, useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0x8f]);
            decoder.Complete();
        }

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Unknown, null, 0x8f, Modifiers.None, KeyAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Verifies a fallback binding cannot steal a pending UTF-8 continuation.</summary>
    [Fact]
    public void Decode_WhenFallbackStartsWithUtf8Continuation_PendingUnicodeWinsAtEverySplit()
    {
        var input = "\u00a0"u8.ToArray();
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding([0xa0], Code.F63)]),
            useAnsiKeyGrammar: false);

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(input.AsSpan(0, split));
            decoder.Decode(input.AsSpan(split));
            decoder.Complete();

            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune(0xa0), $"split {split}");
            sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.Character, $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies an established matcher prefix continues to own its remaining bytes.</summary>
    [Fact]
    public void Decode_WhenMatcherPrefixPrecedesUtf8Continuation_ExistingMatchWins()
    {
        var sequence = "\u00a0"u8.ToArray();
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap([new KeyBinding(sequence, Code.F63)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode(sequence.AsSpan(0, 1));
            decoder.Decode(sequence.AsSpan(1));
            decoder.Complete();
        }

        sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.F63);
        sink.Text.ShouldBeEmpty();
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Verifies a suffix after a shorter match is rematched as an adjacent described key.</summary>
    [Fact]
    public void Decode_WhenShorterMatchLeavesDescribedSuffix_RematchesSuffixAtEverySplit()
    {
        var input = new byte[] { 0xff, 0xfe, 0x78 };
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F61),
                new KeyBinding([0xff, 0xfe, 0xff], Code.F62),
                new KeyBinding([0xfe, 0x78], Code.F63)
            ]),
            useAnsiKeyGrammar: false);

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(input.AsSpan(0, split));
            decoder.Decode(input.AsSpan(split));
            decoder.Complete();

            sink.Strokes.Select(static value => value.Code)
                .ShouldBe([Code.F61, Code.F63], $"split {split}");
            sink.Text.ShouldBeEmpty($"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies every fallback match contributes its exact bytes to later diagnostic offsets.</summary>
    [Fact]
    public void Decode_WhenFallbackKeysPrecedeMalformedProtocol_PreservesAbsoluteOffset()
    {
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F62),
                new KeyBinding([0xfe], Code.F63)
            ]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0xff, 0xfe]);
            decoder.Decode("\u001b[1:x"u8);
            decoder.Complete();
        }

        sink.Strokes.Select(static value => value.Code).ShouldBe([Code.F62, Code.F63]);
        sink.Diagnostics.ShouldHaveSingleItem().Offset.ShouldBe(7);
    }

    /// <summary>Verifies CSI parameter signatures accept the exact active limit and reject one more.</summary>
    [Fact]
    public void Constructor_WhenCsiParametersMeetOrExceedActiveLimit_EnforcesLimit()
    {
        var limits = ParserLimits.Default with { MaxParameterBytes = 3 };

        var exact = new KeyBinding("\u001b[123A"u8, Code.Up, Modifiers.None, limits);

        _ = exact.Signature.ShouldNotBeNull();
        _ = Should.Throw<ArgumentException>(() =>
            new KeyBinding("\u001b[1234A"u8, Code.Up, Modifiers.None, limits));
    }

    /// <summary>Verifies Escape intermediates accept the exact active limit and reject one more.</summary>
    [Fact]
    public void Constructor_WhenEscapeIntermediatesMeetOrExceedActiveLimit_EnforcesLimit()
    {
        var limits = ParserLimits.Default with { MaxIntermediateBytes = 2 };

        var exact = new KeyBinding("\u001b()B"u8, Code.F62, Modifiers.None, limits);

        _ = exact.Signature.ShouldNotBeNull();
        _ = Should.Throw<ArgumentException>(() =>
            new KeyBinding("\u001b()#B"u8, Code.F63, Modifiers.None, limits));
    }

    /// <summary>Verifies the ordinary constructor compiles against default parser limits.</summary>
    [Fact]
    public void Constructor_WhenCsiParametersExceedDefaultLimit_RejectsSignature()
    {
        var exact = CsiWithParameters(ParserLimits.Default.MaxParameterBytes);
        var over = CsiWithParameters(ParserLimits.Default.MaxParameterBytes + 1);

        _ = new KeyBinding(exact, Code.Up);
        _ = Should.Throw<ArgumentException>(() => new KeyBinding(over, Code.Up));
    }

    /// <summary>Verifies matcher disposal clears a retained prefix and releases every owned array.</summary>
    [Fact]
    public void Dispose_WhenMatcherRetainsPrefix_ReleasesOwnedStorageIdempotently()
    {
        var matcher = new KeySequenceMatcher(
        [
            new KeyBinding([0xff], Code.F62),
            new KeyBinding([0xff, 0xfe], Code.F63)
        ]);

        var status = matcher.Add(0xff, out _, out _, out _);

        status.ShouldBe(KeySequenceMatchStatus.Pending);
        matcher.Pending.ShouldBeTrue();
        matcher.RetainsStorage.ShouldBeTrue();

        matcher.Dispose();
        matcher.Dispose();

        matcher.Pending.ShouldBeFalse();
        matcher.RetainsStorage.ShouldBeFalse();
    }

    /// <summary>Verifies decoder disposal releases matcher and rematch workspace ownership.</summary>
    [Fact]
    public void Dispose_WhenDecoderUsedRematchWorkspace_ReleasesKeyStorageAndRejectsUse()
    {
        var options = InputOptions.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F61),
                new KeyBinding([0xff, 0xfe, 0xff], Code.F62),
                new KeyBinding([0xfe, (byte) 'x'], Code.F63)
            ]),
            useAnsiKeyGrammar: false);
        var ownership = DecoderOwnershipProbe.CreateAfterRematch(options);
        var decoder = ownership.InputDecoder;

        DecoderOwnershipProbe.Dispose(decoder);
        decoder.Dispose();

        DecoderOwnershipProbe.WaitForRelease(ownership.Matcher, ownership.Replay).ShouldBeTrue();
        _ = Should.Throw<ObjectDisposedException>(() => decoder.Decode([]));
        _ = Should.Throw<ObjectDisposedException>(decoder.Complete);
        _ = Should.Throw<ObjectDisposedException>(() => decoder.ExpireEscape());
        GC.KeepAlive(decoder);
    }

    private static byte[] CsiWithParameters(int count)
    {
        var sequence = new byte[count + 3];
        sequence[0] = 0x1b;
        sequence[1] = (byte) '[';
        sequence.AsSpan(2, count).Fill((byte) '1');
        sequence[^1] = (byte) 'A';
        return sequence;
    }

    private static void AssertDescribedAtEverySplit(byte[] sequence, Code code, InputOptions options)
    {
        for (var split = 0; split <= sequence.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(sequence.AsSpan(0, split));
            decoder.Decode(sequence.AsSpan(split));
            decoder.Complete();

            sink.Strokes.ShouldBe(
            [
                new Stroke(code, null, 0, Modifiers.None, KeyAction.Press)
            ], $"{Convert.ToHexString(sequence)} split {split}");
            sink.Diagnostics.ShouldBeEmpty($"{Convert.ToHexString(sequence)} split {split}");
        }
    }

    #endregion

    #region CSI dispatch precedence

    /// <summary>Verifies a Kitty enhancement-flags query reply (<c>CSI ? &lt;flags&gt; u</c>) is
    /// claimed by the xterm response handler ahead of the Kitty keyboard handler, when a protocol
    /// sink is present to receive it.</summary>
    [Fact]
    public void Decode_WhenQueryReplyArrivesWithProtocolSink_IsClaimedByXtermResponseNotKitty()
    {
        var sink = new RecordingProtocolSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("[?5u"u8.ToArray());

        sink.MetricsResponses.ShouldBeEmpty();
        var response = sink.Responses.ShouldHaveSingleItem();
        response.Kind.ShouldBe(ResponseKind.Keyboard);
        sink.Strokes.ShouldBeEmpty();
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Verifies the same query reply, with no protocol sink to receive it, is still
    /// claimed by the xterm response handler (reporting Unsupported) rather than falling through
    /// to the Kitty keyboard handler and being misdecoded as a key event.</summary>
    [Fact]
    public void Decode_WhenQueryReplyArrivesWithoutProtocolSink_ReportsUnsupportedNotAKittyStroke()
    {
        var sink = new RecordingInputSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("[?5u"u8.ToArray());

        sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCode.Unsupported);
        sink.Strokes.ShouldBeEmpty();
    }

    /// <summary>Verifies a Kitty keyboard event report (<c>CSI &lt;code&gt;u</c>, no private
    /// marker) is decoded as a stroke rather than claimed by the xterm response handler, since
    /// TryCsi's own 'u' case requires the '?' marker the xterm response only matches on.</summary>
    [Fact]
    public void Decode_WhenKittyEventArrivesWithNoMarker_FallsThroughToKittyHandler()
    {
        var sink = new RecordingProtocolSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("[97u"u8.ToArray());

        sink.Responses.ShouldBeEmpty();
        var stroke = sink.Strokes.ShouldHaveSingleItem();
        stroke.Code.ShouldBe(Code.Character);
        stroke.Character.ShouldBe(new Rune('a'));
    }

    #endregion

    #region xterm modifyOtherKeys

    /// <summary>Verifies query, set, and initial-value restore use official bytes.</summary>
    [Fact]
    public void Commands_WhenCalled_WriteExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        XtermModifyOtherKeys.Query(writer);
        XtermModifyOtherKeys.Set(writer, 2);
        XtermModifyOtherKeys.Restore(writer);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[?4m\u001b[>4;2m\u001b[>4m"u8.ToArray());
    }

    /// <summary>Verifies legacy and CSI-u compatible forms preserve scalar and modifiers.</summary>
    [Theory]
    [InlineData("\u001b[27;3;120~")]
    [InlineData("\u001b[120;3u")]
    public void Decode_WhenEnhancedCharacterArrives_EmitsTypedStroke(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var decoder = new InputDecoder(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));

            var stroke = sink.Strokes.ShouldHaveSingleItem($"split {split}");
            stroke.Code.ShouldBe(Code.Character, $"split {split}");
            stroke.Character.ShouldBe(new Rune('x'), $"split {split}");
            stroke.Modifiers.ShouldBe(Modifiers.Alt, $"split {split}");
        }
    }

    /// <summary>Verifies malformed enhanced input recovers a following ordinary key.</summary>
    [Fact]
    public void Decode_WhenEnhancedKeyIsMalformed_ReportsAndRecovers()
    {
        var bytes = "\u001b[27;99;120~z"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var decoder = new InputDecoder(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));

            sink.Diagnostics.ShouldHaveSingleItem($"split {split}").Code.ShouldBe(
                DiagnosticCode.Malformed,
                $"split {split}");
            sink.Text[^1].Value.ShouldBe(new Rune('z'), $"split {split}");
        }
    }

    /// <summary>Verifies xterm's query reply is routed as protocol state rather than input.</summary>
    [Fact]
    public void Decode_WhenQueryReplyArrives_EmitsTypedResponse()
    {
        var sink = new RecordingProtocolSink();
        using var decoder = new InputDecoder(sink);

        decoder.Decode("\u001b[>4;2m"u8);

        var response = sink.Responses.ShouldHaveSingleItem();
        response.Kind.ShouldBe(ResponseKind.ModifyOtherKeys);
        response.Values.ToArray().ShouldBe([4, 2]);
        sink.Strokes.ShouldBeEmpty();
    }

    /// <summary>Verifies Kitty event subparameters retain precedence over compatible CSI-u.</summary>
    [Fact]
    public void Decode_WhenKittyAndCompatibleCsiUOverlap_PreservesKittyActionAtEverySplit()
    {
        var bytes = "\u001b[120;3:2u"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var decoder = new InputDecoder(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));

            var stroke = sink.Strokes.ShouldHaveSingleItem($"split {split}");
            stroke.Character.ShouldBe(new Rune('x'), $"split {split}");
            stroke.Modifiers.ShouldBe(Modifiers.Alt, $"split {split}");
            stroke.Action.ShouldBe(KeyAction.Repeat, $"split {split}");
        }
    }

    #endregion

    #region Streaming text and allocation

    /// <summary>
    /// Verifies every input split preserves complete Unicode scalar values.
    /// </summary>
    [Fact]
    public void Decode_WhenUtf8IsFragmented_EmitsCompleteRunes()
    {
        var bytes = "Aé👩"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
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

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
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
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode("x\u001by"u8);
        decoder.Complete();

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Character, new Rune('x'), 0, Modifiers.None, KeyAction.Press),
            new Stroke(Code.Character, new Rune('y'), 0, Modifiers.Alt, KeyAction.Press)
        ]);
        sink.Text.ShouldBe([new TerminalText(new Rune('x')), new TerminalText(new Rune('y'))]);
    }

    /// <summary>
    /// Verifies Escape-prefixed UTF-8 preserves Alt across every byte split.
    /// </summary>
    [Fact]
    public void Decode_WhenAltUtf8IsFragmented_PreservesOneScalar()
    {
        var bytes = "\u001bé"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
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
                        KeyAction.Press)
                ], $"split {split}");
            sink.Text.ShouldBe([new TerminalText(new Rune('é'))], $"split {split}");
        }
    }

    /// <summary>
    /// Verifies a lone Escape is held until its deadline and then emitted once.
    /// </summary>
    [Fact]
    public void ExpireEscape_WhenDeadlineIsReached_EmitsEscape()
    {
        var sink = new RecordingInputSink();
        var clock = new ManualTimeProvider();
        using InputDecoder decoder = new(
            sink,
            new InputOptions { EscapeTimeout = TimeSpan.FromMilliseconds(25) },
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
            new Stroke(Code.Escape, null, 0, Modifiers.None, KeyAction.Press)
        ]);
    }

    /// <summary>
    /// Verifies an expired raw Escape remains included in later diagnostic offsets.
    /// </summary>
    [Fact]
    public void Decode_WhenEscapeExpired_PreservesAbsoluteDiagnosticOffset()
    {
        var sink = new RecordingInputSink();
        var clock = new ManualTimeProvider();
        using InputDecoder decoder = new(
            sink,
            new InputOptions { EscapeTimeout = TimeSpan.FromMilliseconds(1) },
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
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode([0xF0, 0x9F]);
        decoder.Decode("\u001b"u8);
        decoder.Complete();

        sink.Text.ShouldBe([new TerminalText(Rune.ReplacementChar)]);
        sink.Strokes[^1].Code.ShouldBe(Code.Escape);
    }

    /// <summary>
    /// Verifies completion reports an unfinished CSI without inventing a key.
    /// </summary>
    [Fact]
    public void Complete_WhenCsiIsTruncated_ReportsDiagnostic()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode("\u001b[1;"u8);
        decoder.Complete();

        sink.Strokes.ShouldBeEmpty();
        sink.Diagnostics.Single().Code.ShouldBe(DiagnosticCode.Truncated);
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
            InputAction.Move,
            0,
            0,
            Modifiers.None,
            false,
            false));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Pointer(
            default,
            null,
            Buttons.None,
            (InputAction) int.MaxValue,
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
        var cell = new Pointer(
            new Point(2, 3),
            null,
            Buttons.None,
            InputAction.Move,
            0,
            0,
            Modifiers.None,
            true,
            false);
        var pixel = new Pointer(
            null,
            new Point(20, 30),
            Buttons.None,
            InputAction.Move,
            0,
            0,
            Modifiers.None,
            true,
            false);
        var leave = new Pointer(
            null,
            null,
            Buttons.None,
            InputAction.Leave,
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
            InputAction.Move,
            0,
            0,
            Modifiers.None,
            true,
            false));
    }

    /// <summary>
    /// Verifies a pending SS3 continuation that consumes a would-be Alt-marked byte does not arm
    /// a decoder-wide modifier that then attaches to a later, unrelated keystroke.
    /// </summary>
    [Fact]
    public void Decode_WhenSs3PendingConsumesAltMarkedByte_DoesNotArmLaterText()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        // ESC O arms a pending SS3 continuation; the next ESC then a UTF-8 lead byte would,
        // without the fix, be misread as Alt-prefixed text even though the SS3 decoder consumes
        // the lead byte itself and never reaches EmitText.
        decoder.Decode([0x1b, (byte) 'O', 0x1b, 0xc3, (byte) 's']);
        decoder.Complete();

        var stroke = sink.Strokes.ShouldHaveSingleItem();
        stroke.Character.ShouldBe(new Rune('s'));
        stroke.Modifiers.ShouldBe(Modifiers.None);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('s'));
    }

    /// <summary>
    /// Verifies a pending X10 mouse continuation that consumes a would-be Alt-marked byte does
    /// not arm a decoder-wide modifier that then attaches to a later, unrelated keystroke.
    /// </summary>
    [Fact]
    public void Decode_WhenX10PendingConsumesAltMarkedByte_DoesNotArmLaterText()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        // ESC [ M arms a pending X10 mouse report; the next ESC then a UTF-8 lead byte would,
        // without the fix, be misread as Alt-prefixed text even though the mouse decoder consumes
        // the lead byte itself and never reaches EmitText.
        decoder.Decode([0x1b, (byte) '[', (byte) 'M', 0x1b, 0xc3, (byte) 's']);
        decoder.Complete();

        var stroke = sink.Strokes.ShouldHaveSingleItem();
        stroke.Character.ShouldBe(new Rune('s'));
        stroke.Modifiers.ShouldBe(Modifiers.None);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('s'));
    }

    /// <summary>
    /// Verifies a UTF-8 continuation byte (0x80..0xBF) following Escape cannot begin a scalar
    /// and so does not arm Alt for text that can never be produced: the malformed Escape sequence
    /// is reported and recovers, and a following ordinary keystroke carries no leaked Alt.
    /// </summary>
    [Fact]
    public void Decode_WhenEscapeIsFollowedByUtf8ContinuationByte_DoesNotArmLaterText()
    {
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        decoder.Decode([0x1b, 0x80, (byte) 'x']);
        decoder.Complete();

        sink.Diagnostics.ShouldNotBeEmpty();
        var stroke = sink.Strokes.ShouldHaveSingleItem();
        stroke.Character.ShouldBe(new Rune('x'));
        stroke.Modifiers.ShouldBe(Modifiers.None);
    }

    /// <summary>Verifies a legacy input-only sink observes an unsupported reply.</summary>
    [Fact]
    public void Decode_WhenSinkHandlesOnlyInput_ReportsUnsupportedReply()
    {
        // Arrange
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink);

        // Act
        decoder.Decode("\u001b[?1;2c"u8);

        // Assert
        var diagnostic = sink.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe(DiagnosticCode.Unsupported);
        diagnostic.Kind.ShouldBe(SequenceKind.Csi);
    }

    #endregion
}
