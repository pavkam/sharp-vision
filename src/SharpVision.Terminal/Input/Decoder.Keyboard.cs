using System.Text;

using SharpVision.Terminal.Protocols;

using InputAction = SharpVision.Terminal.Input.Action;

namespace SharpVision.Terminal.Input;

public sealed partial class Decoder
{
    private const int _maxAssociatedText = 32;

    private void HandleKitty(ReadOnlySpan<byte> parameters)
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
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
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

    private void EmitAssociatedText(ReadOnlySpan<byte> input)
    {
        while (!input.IsEmpty)
        {
            var separator = input.IndexOf((byte) ':');
            var field = separator < 0 ? input : input[..separator];
            var parsed = TryDecimal(field, allowEmpty: false, out var value);
            var scalar = Rune.TryCreate(value, out var rune);
            System.Diagnostics.Debug.Assert(
                parsed && scalar,
                "Associated text is fully validated before emission.");
            var text = new Text(rune);
            _sink.Input(in text);
            input = separator < 0 ? [] : input[(separator + 1)..];
        }
    }

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
                return Code.Backspace;
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
            case >= 57376 and <= 57398:
                return (Code) ((int) Code.F13 + native - 57376);
            default:
                break;
        }

        if (native is >= 57344 and <= 63743 ||
            !Rune.TryCreate(native, out var rune) ||
            Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.Control)
        {
            return Code.Unknown;
        }

        character = rune;
        return Code.Character;
    }

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

    private static bool TryReadModifiers(
        ReadOnlySpan<byte> input,
        out Modifiers modifiers,
        out InputAction action)
    {
        var separator = input.IndexOf((byte) ':');
        var modifierField = separator < 0 ? input : input[..separator];

        if (!TryDecimal(modifierField, allowEmpty: true, out var encoded))
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
        action = InputAction.Press;

        if (separator < 0)
        {
            return true;
        }

        var eventField = input[(separator + 1)..];

        if (eventField.IndexOf((byte) ':') >= 0 ||
            !TryDecimal(eventField, allowEmpty: false, out var eventType) ||
            eventType is < 1 or > 3)
        {
            return false;
        }

        action = (InputAction) (eventType - 1);
        return true;
    }

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

    private static bool TryDecimal(ReadOnlySpan<byte> input, bool allowEmpty, out int value)
    {
        value = 0;

        if (input.IsEmpty)
        {
            return allowEmpty;
        }

        foreach (var item in input)
        {
            if (item is < (byte) '0' or > (byte) '9')
            {
                value = 0;
                return false;
            }

            var digit = item - (byte) '0';

            if (value > int.MaxValue / 10 ||
                (value == int.MaxValue / 10 && digit > int.MaxValue % 10))
            {
                value = 0;
                return false;
            }

            value = (value * 10) + digit;
        }

        return true;
    }
}
