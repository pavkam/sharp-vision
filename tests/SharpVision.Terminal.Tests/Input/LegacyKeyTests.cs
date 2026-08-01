// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

using InputAction = Terminal.Input.Action;

/// <summary>
/// Verifies legacy VT key mappings, modifiers, fragmentation, and recovery.
/// </summary>
public sealed class LegacyKeyTests
{
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
            new Stroke(code, null, 0, Modifiers.None, InputAction.Press)
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
                    new Stroke(code, null, nativeCode, modifiers, InputAction.Press)
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
            new Stroke(code, null, native, Modifiers.None, InputAction.Press)
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
            new Stroke(Code.Unknown, null, 99, Modifiers.None, InputAction.Press));
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
    /// a plausible synthetic stroke (see #97).
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
    [InlineData("[1;1:1A", Code.Up, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:2A", Code.Up, Modifiers.None, InputAction.Repeat)]
    [InlineData("[1;1:3A", Code.Up, Modifiers.None, InputAction.Release)]
    [InlineData("[1;1:1B", Code.Down, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:1C", Code.Right, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:1D", Code.Left, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:1H", Code.Home, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:1F", Code.End, Modifiers.None, InputAction.Press)]
    public void Decode_WhenCsiKeyHasKittyEventType_MapsCodeAndAction(
        string input,
        Code code,
        Modifiers modifiers,
        InputAction action)
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
    [InlineData("[1;2:1A", Code.Up, Modifiers.Shift, InputAction.Press)]
    [InlineData("[1;3:1D", Code.Left, Modifiers.Alt, InputAction.Press)]
    [InlineData("[1;5:1C", Code.Right, Modifiers.Control, InputAction.Press)]
    [InlineData("[1;5:3C", Code.Right, Modifiers.Control, InputAction.Release)]
    [InlineData("[1;2:2B", Code.Down, Modifiers.Shift, InputAction.Repeat)]
    [InlineData("[1;3:3A", Code.Up, Modifiers.Alt, InputAction.Release)]
    [InlineData("[1;6:1H", Code.Home, Modifiers.Shift | Modifiers.Control, InputAction.Press)]
    [InlineData("[1;7:1F", Code.End, Modifiers.Alt | Modifiers.Control, InputAction.Press)]
    public void Decode_WhenCsiKeyHasModifierAndEventType_MapsBoth(
        string input,
        Code code,
        Modifiers modifiers,
        InputAction action)
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
            new Stroke(code, null, 0, modifiers, InputAction.Press)
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
            new Stroke(code, null, 0, Modifiers.None, InputAction.Press)
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
            new Stroke(code, null, 0, Modifiers.None, InputAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies an unrecognized CSI final byte reports Unsupported without emitting a stroke,
    /// since CSI participates in an extensible dispatch chain that must let later handlers
    /// (terminfo KeyMap, the ANSI grammar fallback) claim what this table does not (see #97).
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
    /// unlike CSI (see #97).
    /// </summary>
    [Fact]
    public void Decode_WhenSs3FinalByteIsUnrecognized_EmitsUnknownStrokeWithNativeByte()
    {
        var sink = Decode(Encoding.UTF8.GetBytes("OX"));

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Unknown, null, (byte) 'X', Modifiers.None, InputAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies CSI keys with Kitty event types decode identically at every byte split.
    /// </summary>
    [Theory]
    [InlineData("[1;1:1A", Code.Up, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:2A", Code.Up, Modifiers.None, InputAction.Repeat)]
    [InlineData("[1;1:3A", Code.Up, Modifiers.None, InputAction.Release)]
    [InlineData("[1;5:1C", Code.Right, Modifiers.Control, InputAction.Press)]
    [InlineData("[1;2:3D", Code.Left, Modifiers.Shift, InputAction.Release)]
    public void Decode_WhenCsiEventTypeIsFragmented_MapsAtEverySplit(
        string input,
        Code code,
        Modifiers modifiers,
        InputAction action)
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
        sink.Strokes.ShouldAllBe(static item => item.Action == InputAction.Press);
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
            new Stroke(Code.Up, null, 0, Modifiers.None, InputAction.Press),
            new Stroke(Code.Up, null, 0, Modifiers.None, InputAction.Release)
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
                InputAction.Press,
                InputAction.Repeat,
                InputAction.Repeat,
                InputAction.Release
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
            new Stroke(Code.Up, null, 0, Modifiers.None, InputAction.Press),
            new Stroke(Code.Up, null, 0, Modifiers.Shift, InputAction.Press),
            new Stroke(Code.Right, null, 0, Modifiers.None, InputAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies CSI arrow keys with event types decode when UseAnsiKeyGrammar is false,
    /// simulating a real terminal profile (like Kitty) where the ANSI grammar is disabled
    /// and only the key map and standard CSI handlers are active.
    /// </summary>
    [Theory]
    [InlineData("[1;1:1A", Code.Up, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:3A", Code.Up, Modifiers.None, InputAction.Release)]
    [InlineData("[1;1:1B", Code.Down, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:1C", Code.Right, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:1D", Code.Left, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:1H", Code.Home, Modifiers.None, InputAction.Press)]
    [InlineData("[1;1:1F", Code.End, Modifiers.None, InputAction.Press)]
    [InlineData("[1;5:1C", Code.Right, Modifiers.Control, InputAction.Press)]
    [InlineData("[A", Code.Up, Modifiers.None, InputAction.Press)]
    [InlineData("[B", Code.Down, Modifiers.None, InputAction.Press)]
    public void Decode_WhenAnsiGrammarIsDisabled_StillDecodesCsiKeys(
        string input,
        Code code,
        Modifiers modifiers,
        InputAction action)
    {
        var sink = new RecordingInputSink();
        var options = Options.Default with { UseAnsiKeyGrammar = false };

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode(Encoding.UTF8.GetBytes(input));
            decoder.Complete();
        }

        sink.Strokes.ShouldContain(
            item => item.Code == code && item.Modifiers == modifiers && item.Action == action);
        sink.Diagnostics.ShouldBeEmpty();
    }

    private static RecordingInputSink Decode(byte[] input)
    {
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink))
        {
            decoder.Decode(input);
            decoder.Complete();
        }

        return sink;
    }
}
