using System.Buffers;
using System.Diagnostics;
using System.Text;

using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Input;

public sealed partial class Decoder
{
    private bool TryHandleMouse(ReadOnlySpan<byte> parameters, bool release)
    {
        if (parameters.IsEmpty && !release)
        {
            _x10Pending = true;
            _x10Length = 0;
            return true;
        }

        if (!TryReadMouse(parameters, out var marker, out var code, out var x, out var y))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return true;
        }

        if (marker == (byte) '<')
        {
            EmitPointer(code, x, y, release);
            return true;
        }

        if (marker == 0 && !release)
        {
            EmitPointer(code - 32, x, y, release: false);
            return true;
        }

        Report(DiagnosticCode.Malformed, SequenceKind.Csi);
        return true;
    }

    private int ConsumeX10(ReadOnlySpan<byte> value)
    {
        var consumed = 0;

        while (_x10Pending && consumed < value.Length)
        {
            if (_x10Length == _x10.Length)
            {
                EndX10IfPending();
                break;
            }

            _x10[_x10Length++] = value[consumed++];

            if (TryReadX10(out var code, out var x, out var y))
            {
                _x10Pending = false;
                _x10.AsSpan(0, _x10Length).Clear();
                _x10Length = 0;
                EmitPointer(code - 32, x - 32, y - 32, release: false);
            }
        }

        return consumed;
    }

    private bool TryReadX10(out int code, out int x, out int y)
    {
        Span<int> values = stackalloc int[3];
        var position = 0;

        for (var index = 0; index < values.Length; index++)
        {
            var status = Rune.DecodeFromUtf8(
                _x10.AsSpan(position, _x10Length - position),
                out var rune,
                out var consumed);

            if (status == OperationStatus.NeedMoreData)
            {
                code = 0;
                x = 0;
                y = 0;
                return false;
            }

            if (status != OperationStatus.Done)
            {
                EndX10IfPending();
                code = 0;
                x = 0;
                y = 0;
                return false;
            }

            values[index] = rune.Value;
            position += consumed;
        }

        code = values[0];
        x = values[1];
        y = values[2];
        return position == _x10Length;
    }

    private void EndX10IfPending()
    {
        if (!_x10Pending)
        {
            return;
        }

        _x10Pending = false;
        _x10.AsSpan(0, _x10Length).Clear();
        _x10Length = 0;
        Report(DiagnosticCode.Malformed, SequenceKind.Csi);
    }

    private void EmitPointer(int code, int wireX, int wireY, bool release)
    {
        var motion = (code & 32) != 0;
        var low = code & 3;

        if (code is < 0 or > 255 ||
            ((code & 128) != 0 && ((code & 64) != 0 || low > 1)))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        if (wireX == 0 && wireY == 0 && motion && low == 3)
        {
            var leave = new Pointer(
                default,
                null,
                Buttons.None,
                PointerAction.Leave,
                0,
                0,
                DecodeMouseModifiers(code),
                true,
                false);
            _sink.Input(in leave);
            return;
        }

        if (wireX <= 0 || wireY <= 0)
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        var source = new Geometry.Point(wireX - 1, wireY - 1);
        var cells = source;
        Geometry.Point? pixels = null;
        var inferred = false;

        if (_pixelMouse)
        {
            pixels = source;

            if (_cellMetrics is { } metrics)
            {
                cells = new Geometry.Point(source.X / metrics.Width, source.Y / metrics.Height);
                inferred = true;
            }
            else
            {
                cells = default;
            }
        }

        var modifiers = DecodeMouseModifiers(code);
        var buttons = DecodeButtons(code);
        var action = PointerAction.Press;
        var wheelX = 0;
        var wheelY = 0;

        if ((code & 64) != 0)
        {
            action = PointerAction.Wheel;
            buttons = Buttons.None;

            switch (low)
            {
                case 0:
                    wheelY = 1;
                    break;
                case 1:
                    wheelY = -1;
                    break;
                case 2:
                    wheelX = -1;
                    break;
                case 3:
                    wheelX = 1;
                    break;
                default:
                    throw new UnreachableException("A two-bit wheel selector must be bounded.");
            }
        }
        else if (motion)
        {
            action = PointerAction.Move;
        }
        else if (release || low == 3)
        {
            action = PointerAction.Release;
        }

        var pointer = new Pointer(
            cells,
            pixels,
            buttons,
            action,
            wheelX,
            wheelY,
            modifiers,
            motion,
            inferred);
        _sink.Input(in pointer);
    }

    private static Buttons DecodeButtons(int code)
    {
        var selector = code & 3;
        return (code & 64) != 0 || selector == 3
            ? Buttons.None
            : (code & 128) != 0
                ? selector switch
                {
                    0 => Buttons.Back,
                    1 => Buttons.Forward,
                    _ => Buttons.None,
                }
                : selector switch
                {
                    0 => Buttons.Primary,
                    1 => Buttons.Middle,
                    2 => Buttons.Secondary,
                    _ => Buttons.None,
                };
    }

    private static Modifiers DecodeMouseModifiers(int code)
    {
        var modifiers = Modifiers.None;

        if ((code & 4) != 0)
        {
            modifiers |= Modifiers.Shift;
        }

        if ((code & 8) != 0)
        {
            modifiers |= Modifiers.Alt;
        }

        if ((code & 16) != 0)
        {
            modifiers |= Modifiers.Control;
        }

        return modifiers;
    }

    private static bool TryReadMouse(
        ReadOnlySpan<byte> input,
        out byte marker,
        out int code,
        out int x,
        out int y)
    {
        var parameters = new Parameters(input, 4, int.MaxValue);
        marker = parameters.PrivateMarker;
        x = 0;
        y = 0;
        return ReadMouseField(ref parameters, ParameterSeparator.Semicolon, out code) &&
            ReadMouseField(ref parameters, ParameterSeparator.Semicolon, out x) &&
            ReadMouseField(ref parameters, ParameterSeparator.None, out y) &&
            parameters.Read(out _, out _) == ParameterStatus.End;
    }

    private static bool ReadMouseField(
        ref Parameters parameters,
        ParameterSeparator expected,
        out int value) =>
        parameters.Read(out value, out var separator) == ParameterStatus.Value &&
        separator == expected;
}
