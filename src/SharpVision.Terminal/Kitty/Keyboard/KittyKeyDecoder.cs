// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Keyboard;

using Input;

/// <summary>
/// Decodes Kitty progressive-enhancement keyboard CSI-<c>u</c> reports, extracted from
/// <see cref="InputDecoder"/>.
/// </summary>
/// <remarks>
/// <see cref="TryDecimal(ReadOnlySpan{byte}, bool, out int)"/> and <see cref="TryReadModifiers"/> also back <c>InputDecoder</c>'s xterm
/// modifyOtherKeys CSI parsing, which reuses the same Kitty-originated modifier encoding.
/// </remarks>
internal sealed class KittyKeyDecoder
{
    private const int _maxAssociatedText = 32;

    private readonly IInputSink _sink;
    private readonly Action<DiagnosticCode, SequenceKind> _report;

    /// <summary>Initializes a Kitty key decoder sharing its host's sink and diagnostics.</summary>
    /// <param name="sink">The non-null event sink strokes and associated text are emitted to.</param>
    /// <param name="report">The non-null host diagnostic-reporting delegate.</param>
    public KittyKeyDecoder(IInputSink sink, Action<DiagnosticCode, SequenceKind> report)
    {
        _sink = sink;
        _report = report;
    }

    /// <summary>Handles one parsed Kitty CSI-<c>u</c> report's semicolon-delimited parameters.</summary>
    /// <param name="parameters">The borrowed CSI parameter bytes.</param>
    public void Handle(ReadOnlySpan<byte> parameters)
    {
        if (!TrySplitGroups(
                parameters,
                out var keyGroup,
                out var modifierGroup,
                out var textGroup,
                out var hasText) ||
            !TryReadKey(
                keyGroup,
                out var native,
                out var shifted,
                out var baseLayout) ||
            !TryReadModifiers(modifierGroup, out var modifiers, out var action) ||
            (hasText && !ValidateText(textGroup)))
        {
            _report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        var code = MapKittyCode(native, out var character);
        var stroke = new Stroke(
            code,
            character,
            native,
            modifiers,
            action,
            shifted,
            baseLayout);
        _sink.Input(in stroke);

        if (hasText)
        {
            EmitAssociatedText(textGroup);
        }
    }

    /// <summary>Reads the modifier-and-event-type field shared by Kitty and xterm CSI reports.</summary>
    /// <param name="input">The borrowed modifier field bytes.</param>
    /// <param name="modifiers">The decoded modifier flags.</param>
    /// <param name="action">The decoded key transition, defaulting to press.</param>
    /// <returns>True when the field is a valid modifier, with an optional event-type suffix.</returns>
    [Pure]
    internal static bool TryReadModifiers(
        ReadOnlySpan<byte> input,
        out Modifiers modifiers,
        out KeyAction action)
    {
        var separator = input.IndexOf((byte) ':');
        var modifierField = separator < 0 ? input : input[..separator];

        // The modifier field is the complete remaining input when no colon splits off an event
        // type, so a stray ';' inside it must fail the field instead of silently truncating to
        // whatever preceded the ';'.
        if (!TryDecimal(modifierField, allowEmpty: true, out var encoded, out var modifierEnd) ||
            modifierEnd != ParameterSeparator.None)
        {
            modifiers = default;
            action = default;
            return false;
        }

        encoded = modifierField.IsEmpty ? 1 : encoded;

        if (encoded is < 1 or > 256)
        {
            modifiers = default;
            action = default;
            return false;
        }

        modifiers = (Modifiers) (encoded - 1);
        action = KeyAction.Press;

        if (separator < 0)
        {
            return true;
        }

        var eventField = input[(separator + 1)..];

        if (eventField.IndexOf((byte) ':') >= 0 ||
            !TryDecimal(eventField, allowEmpty: false, out var eventType, out var eventEnd) ||
            eventEnd != ParameterSeparator.None ||
            eventType is < 1 or > 3)
        {
            return false;
        }

        action = (KeyAction) (eventType - 1);
        return true;
    }

    /// <summary>Reads an unsigned decimal field shared by Kitty and xterm CSI parsing.</summary>
    /// <param name="input">The borrowed decimal digit bytes.</param>
    /// <param name="allowEmpty">Whether an empty field is valid, yielding zero.</param>
    /// <param name="value">The decoded non-negative value, or zero when invalid.</param>
    /// <returns>True when the field is empty and allowed, or entirely bounded decimal digits.</returns>
    [Pure]
    internal static bool TryDecimal(ReadOnlySpan<byte> input, bool allowEmpty, out int value) =>
        TryDecimal(input, allowEmpty, out value, out _);

    /// <summary>Reads an unsigned decimal field shared by Kitty and xterm CSI parsing, also reporting
    /// the delimiter that ended it.</summary>
    /// <param name="input">The borrowed decimal digit bytes.</param>
    /// <param name="allowEmpty">Whether an empty field is valid, yielding zero.</param>
    /// <param name="value">The decoded non-negative value, or zero when invalid.</param>
    /// <param name="separator">The delimiter <paramref name="input"/> ended on, or None.</param>
    /// <returns>True when the field is empty and allowed, or entirely bounded decimal digits.</returns>
    /// <remarks>
    /// A caller that already owns the complete field - nothing legitimate should follow within
    /// <paramref name="input"/> - must additionally require <paramref name="separator"/> to be
    /// <see cref="ParameterSeparator.None"/>. Otherwise trailing content past an internal ';' or ':'
    /// this overload doesn't itself reject is silently dropped instead of failing the field.
    /// </remarks>
    [Pure]
    internal static bool TryDecimal(
        ReadOnlySpan<byte> input,
        bool allowEmpty,
        out int value,
        out ParameterSeparator separator)
    {
        // Parameters strips a leading DEC private-marker byte (0x3c-0x3f) before parsing; these
        // fields never carry one, so reject it explicitly rather than silently parsing whatever
        // follows it as the value.
        if (!input.IsEmpty && input[0] is >= 0x3c and <= 0x3f)
        {
            value = 0;
            separator = ParameterSeparator.None;
            return false;
        }

        var parameters = new Parameters(input, maxValue: int.MaxValue);
        var status = parameters.Read(out value, out separator);

        return status switch
        {
            ParameterStatus.Value => true,
            ParameterStatus.End => allowEmpty,
            ParameterStatus.Default => allowEmpty,
            ParameterStatus.Invalid => false,
            ParameterStatus.Overflow => false,
            ParameterStatus.Limit => false,
            _ => throw new UnreachableException("ParameterStatus has no members beyond this switch.")
        };
    }

    private void EmitAssociatedText(ReadOnlySpan<byte> input)
    {
        while (!input.IsEmpty)
        {
            var separator = input.IndexOf((byte) ':');
            var field = separator < 0 ? input : input[..separator];
            var parsed = TryDecimal(field, allowEmpty: false, out var value);
            var scalar = Rune.TryCreate(value, out var rune);
            Debug.Assert(
                parsed && scalar,
                "Associated text is fully validated before emission.");
            var text = new TerminalText(rune);
            _sink.Input(in text);
            input = separator < 0 ? [] : input[(separator + 1)..];
        }
    }

    [Pure]
    private static Code MapKittyCode(int native, out Rune? character)
    {
        character = null;

        switch (native)
        {
            case 0:
                return Code.Unknown;
            case 9:
                return Code.Tab;
            case 13:
                return Code.Enter;
            case 27:
                return Code.Escape;
            case 127:
            case 57347:
                return Code.Backspace;
            case 57344:
                return Code.Escape;
            case 57345:
                return Code.Enter;
            case 57346:
                return Code.Tab;
            case 57348:
                return Code.Insert;
            case 57349:
                return Code.Delete;
            case 57350:
                return Code.Left;
            case 57351:
                return Code.Right;
            case 57352:
                return Code.Up;
            case 57353:
                return Code.Down;
            case 57354:
                return Code.PageUp;
            case 57355:
                return Code.PageDown;
            case 57356:
                return Code.Home;
            case 57357:
                return Code.End;
            case 57358:
                return Code.CapsLock;
            case 57359:
                return Code.ScrollLock;
            case 57360:
                return Code.NumLock;
            case 57361:
                return Code.PrintScreen;
            case 57362:
                return Code.Pause;
            case 57363:
                return Code.Menu;
            case >= 57364 and <= 57375:
                var f1Through12 = native - 57364 + 1;
                var f1Mapped = f1Through12.TryGet(out var f1Code);
                Debug.Assert(f1Mapped, "Kitty's named function range maps to F1 through F12.");
                return f1Code;
            case >= 57376 and <= 57398:
                var function = native - 57376 + 13;
                var mapped = function.TryGet(out var functionCode);
                Debug.Assert(mapped, "Kitty's named function range maps to F13 through F35.");
                return functionCode;
            case 57399:
                return Code.Keypad0;
            case 57400:
                return Code.Keypad1;
            case 57401:
                return Code.Keypad2;
            case 57402:
                return Code.Keypad3;
            case 57403:
                return Code.Keypad4;
            case 57404:
                return Code.Keypad5;
            case 57405:
                return Code.Keypad6;
            case 57406:
                return Code.Keypad7;
            case 57407:
                return Code.Keypad8;
            case 57408:
                return Code.Keypad9;
            case 57409:
                return Code.KeypadDecimal;
            case 57410:
                return Code.KeypadDivide;
            case 57411:
                return Code.KeypadMultiply;
            case 57412:
                return Code.KeypadSubtract;
            case 57413:
                return Code.KeypadAdd;
            case 57414:
                return Code.KeypadEnter;
            case 57415:
                return Code.KeypadEqual;
            case 57416:
                return Code.KeypadSeparator;
            case 57417:
                return Code.KeypadLeft;
            case 57418:
                return Code.KeypadRight;
            case 57419:
                return Code.KeypadUp;
            case 57420:
                return Code.KeypadDown;
            case 57421:
                return Code.KeypadPageUp;
            case 57422:
                return Code.KeypadPageDown;
            case 57423:
                return Code.KeypadHome;
            case 57424:
                return Code.KeypadEnd;
            case 57425:
                return Code.KeypadInsert;
            case 57426:
                return Code.KeypadDelete;
            case 57427:
                // Kitty's keypad Begin/center key shares the same logical identity as the
                // legacy keypad-5-with-NumLock-off "Begin" key, so it reuses Code.Begin
                // rather than a separate keypad-specific member.
                return Code.Begin;
            case 57428:
                return Code.MediaPlay;
            case 57429:
                return Code.MediaPause;
            case 57430:
                return Code.MediaPlayPause;
            case 57431:
                return Code.MediaReverse;
            case 57432:
                return Code.MediaStop;
            case 57433:
                return Code.MediaFastForward;
            case 57434:
                return Code.MediaRewind;
            case 57435:
                return Code.MediaTrackNext;
            case 57436:
                return Code.MediaTrackPrevious;
            case 57437:
                return Code.MediaRecord;
            case 57438:
                return Code.LowerVolume;
            case 57439:
                return Code.RaiseVolume;
            case 57440:
                return Code.MuteVolume;
            case 57441:
                return Code.LeftShift;
            case 57442:
                return Code.LeftControl;
            case 57443:
                return Code.LeftAlt;
            case 57444:
                return Code.LeftSuper;
            case 57445:
                return Code.LeftHyper;
            case 57446:
                return Code.LeftMeta;
            case 57447:
                return Code.RightShift;
            case 57448:
                return Code.RightControl;
            case 57449:
                return Code.RightAlt;
            case 57450:
                return Code.RightSuper;
            case 57451:
                return Code.RightHyper;
            case 57452:
                return Code.RightMeta;
            case 57453:
                return Code.IsoLevel3Shift;
            case 57454:
                return Code.IsoLevel5Shift;
            default:
                break;
        }

        if (native is >= 57344 and <= 63743 ||
            !Rune.TryCreate(native, out var rune) ||
            Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control)
        {
            return Code.Unknown;
        }

        character = rune;
        return Code.Character;
    }

    [Pure]
    private static bool TrySplitGroups(
        ReadOnlySpan<byte> input,
        out ReadOnlySpan<byte> key,
        out ReadOnlySpan<byte> modifiers,
        out ReadOnlySpan<byte> text,
        out bool hasText)
    {
        var first = input.IndexOf((byte) ';');

        if (first < 0)
        {
            key = input;
            modifiers = [];
            text = [];
            hasText = false;
            return true;
        }

        key = input[..first];
        input = input[(first + 1)..];
        var second = input.IndexOf((byte) ';');

        if (second < 0)
        {
            modifiers = input;
            text = [];
            hasText = false;
            return true;
        }

        modifiers = input[..second];
        text = input[(second + 1)..];
        hasText = true;
        return text.IndexOf((byte) ';') < 0;
    }

    [Pure]
    private static bool TryReadKey(
        ReadOnlySpan<byte> input,
        out int native,
        out Rune? shifted,
        out Rune? baseLayout)
    {
        shifted = null;
        baseLayout = null;
        var first = input.IndexOf((byte) ':');
        var main = first < 0 ? input : input[..first];

        if (!TryDecimal(main, allowEmpty: false, out native) ||
            (native != 0 && !Rune.TryCreate(native, out _)))
        {
            return false;
        }

        if (first < 0)
        {
            return true;
        }

        input = input[(first + 1)..];
        var second = input.IndexOf((byte) ':');
        var shiftedField = second < 0 ? input : input[..second];

        if (!TryOptionalRune(shiftedField, out shifted))
        {
            return false;
        }

        if (second < 0)
        {
            return true;
        }

        var baseField = input[(second + 1)..];
        return baseField.IndexOf((byte) ':') < 0 &&
               TryOptionalRune(baseField, out baseLayout);
    }

    [Pure]
    private static bool ValidateText(ReadOnlySpan<byte> input)
    {
        var count = 0;

        while (!input.IsEmpty)
        {
            var separator = input.IndexOf((byte) ':');
            var field = separator < 0 ? input : input[..separator];

            if (++count > _maxAssociatedText ||
                !TryDecimal(field, allowEmpty: false, out var value) ||
                !Rune.TryCreate(value, out _) ||
                value < 0x20 ||
                value is >= 0x7f and <= 0x9f)
            {
                return false;
            }

            input = separator < 0 ? [] : input[(separator + 1)..];
        }

        return true;
    }

    [Pure]
    private static bool TryOptionalRune(ReadOnlySpan<byte> input, out Rune? rune)
    {
        if (input.IsEmpty)
        {
            rune = null;
            return true;
        }

        if (TryDecimal(input, allowEmpty: false, out var value) &&
            Rune.TryCreate(value, out var parsed))
        {
            rune = parsed;
            return true;
        }

        rune = null;
        return false;
    }
}
