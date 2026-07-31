// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

using InputAction = Terminal.Input.Action;

/// <summary>
/// Verifies Kitty CSI-u key identity, modifiers, events, text, and recovery.
/// </summary>
public sealed class KittyKeyboardTests
{
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
                InputAction.Repeat,
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
    [InlineData("\u001b[97;256:1u", InputAction.Press)]
    [InlineData("\u001b[97;256:2u", InputAction.Repeat)]
    [InlineData("\u001b[97;256:3u", InputAction.Release)]
    public void Decode_WhenKittyActionVaries_MapsModifiersAndAction(
        string input,
        InputAction action)
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
            new Stroke(code, null, native, Modifiers.None, InputAction.Press));
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
            new Stroke(Code.Unknown, null, 0, Modifiers.None, InputAction.Press)
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
    [InlineData("\u001b[97;;31u")]
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
            new Stroke(Code.Character, new Rune('a'), 97, Modifiers.None, InputAction.Press));
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
        stroke.Action.ShouldBe(InputAction.Press);
    }

    // ── Event types ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies press, repeat, and release event types with no modifiers.
    /// </summary>
    [Theory]
    [InlineData("[97;1:1u", InputAction.Press)]
    [InlineData("[97;1:2u", InputAction.Repeat)]
    [InlineData("[97;1:3u", InputAction.Release)]
    public void Decode_WhenEventTypeVariesWithoutModifiers_MapsAction(
        string input,
        InputAction action)
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
    [InlineData("[97;2:1u", Modifiers.Shift, InputAction.Press)]
    [InlineData("[97;5:2u", Modifiers.Control, InputAction.Repeat)]
    [InlineData("[97;3:3u", Modifiers.Alt, InputAction.Release)]
    public void Decode_WhenEventTypeAndModifierBothPresent_MapsBoth(
        string input,
        Modifiers modifiers,
        InputAction action)
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
            new Stroke(code, null, native, Modifiers.None, InputAction.Press));
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
        stroke.Action.ShouldBe(InputAction.Press);
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
            new Stroke(code, null, native, Modifiers.None, InputAction.Press));
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
        stroke.Action.ShouldBe(InputAction.Press);
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
            new Stroke(Code.Character, new Rune('a'), 97, Modifiers.None, InputAction.Press));
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
            new Stroke(Code.Character, new Rune('a'), 97, Modifiers.None, InputAction.Press));
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
        var sink = Decode(Encoding.UTF8.GetBytes("[97;;31u" + "x"));

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
            sink.Strokes[0].Action.ShouldBe(InputAction.Repeat, $"split {split}");
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
    [InlineData("[97;;31u")]
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
            new Stroke(Code.Character, new Rune('b'), 98, Modifiers.None, InputAction.Press));
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
}
