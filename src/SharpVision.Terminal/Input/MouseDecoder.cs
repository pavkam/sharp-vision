// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>
/// Decodes SGR and legacy X10 mouse-report CSI sequences into <see cref="Pointer"/> values,
/// extracted from <see cref="InputDecoder"/> as one of its four self-contained protocol decoders.
/// </summary>
internal sealed class MouseDecoder
{
    private readonly IInputSink _sink;
    private readonly CellMetricsResolver _cellMetrics;
    private readonly Action<DiagnosticCode, SequenceKind> _report;
    private readonly byte[] _x10 = new byte[12];
    private readonly bool _pixelMouse;
    private readonly bool _utf8Coordinates;
    private int _x10Length;

    /// <summary>Initializes a mouse decoder sharing its host's sink, diagnostics, and metrics.</summary>
    /// <param name="sink">The non-null event sink pointer reports are emitted to.</param>
    /// <param name="cellMetrics">The non-null shared cell-metrics resolver.</param>
    /// <param name="pixelMouse">Whether pixel coordinates are preferred over cell inference.</param>
    /// <param name="utf8Coordinates">
    /// Whether the negotiated X10 field encoding is UTF-8 (mode 1005) rather than raw single-byte
    /// X10 fields. The two are mutually ambiguous above <c>0x7F</c>, so this must reflect the
    /// actual negotiated mode rather than being inferred from the byte stream.
    /// </param>
    /// <param name="report">The non-null host diagnostic-reporting delegate.</param>
    public MouseDecoder(
        IInputSink sink,
        CellMetricsResolver cellMetrics,
        bool pixelMouse,
        bool utf8Coordinates,
        Action<DiagnosticCode, SequenceKind> report)
    {
        _sink = sink;
        _cellMetrics = cellMetrics;
        _pixelMouse = pixelMouse;
        _utf8Coordinates = utf8Coordinates;
        _report = report;
    }

    /// <summary>Gets whether a legacy X10 mouse report is awaiting its three coordinate bytes.</summary>
    public bool Pending { get; private set; }

    /// <summary>Clears any pending X10 buffer content without reporting a diagnostic.</summary>
    public void Clear() => _x10.AsSpan().Clear();

    /// <summary>Handles one parsed SGR (<c>&lt;</c>) or legacy (unmarked) mouse CSI final byte.</summary>
    /// <param name="parameters">The borrowed CSI parameter bytes.</param>
    /// <param name="release">Whether the final byte was the SGR release form (<c>m</c>).</param>
    /// <returns>True; the sequence was recognized as a mouse report or reported malformed.</returns>
    public bool TryHandleMouse(ReadOnlySpan<byte> parameters, bool release)
    {
        if (parameters.IsEmpty && !release)
        {
            Pending = true;
            _x10Length = 0;
            return true;
        }

        if (!TryReadMouse(parameters, out var marker, out var code, out var x, out var y))
        {
            _report(DiagnosticCode.Malformed, SequenceKind.Csi);
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

        _report(DiagnosticCode.Malformed, SequenceKind.Csi);
        return true;
    }

    /// <summary>Consumes as many pending X10 coordinate bytes as available from borrowed text.</summary>
    /// <param name="value">The borrowed text bytes.</param>
    /// <returns>The number of bytes consumed from the start of <paramref name="value"/>.</returns>
    public int ConsumeX10(ReadOnlySpan<byte> value)
    {
        var consumed = 0;

        while (Pending && consumed < value.Length)
        {
            if (_x10Length == _x10.Length)
            {
                EndIfPending();
                break;
            }

            _x10[_x10Length++] = value[consumed++];

            if (TryReadX10(out var code, out var x, out var y))
            {
                Pending = false;
                _x10.AsSpan(0, _x10Length).Clear();
                _x10Length = 0;
                EmitPointer(code - 32, x - 32, y - 32, release: false);
            }
        }

        return consumed;
    }

    /// <summary>Discards a pending X10 report and reports it malformed, if one is pending.</summary>
    public void EndIfPending()
    {
        if (!Pending)
        {
            return;
        }

        Pending = false;
        _x10.AsSpan(0, _x10Length).Clear();
        _x10Length = 0;
        _report(DiagnosticCode.Malformed, SequenceKind.Csi);
    }

    private bool TryReadX10(out int code, out int x, out int y)
    {
        // Mode 1005 (Utf8) encodes each field as a UTF-8 scalar; raw X10 (Default) encodes each
        // field as exactly one byte with no character encoding involved. The two are mutually
        // ambiguous above 0x7F by construction, so which reader runs must reflect the negotiated
        // mode rather than being guessed from the bytes.
        return _utf8Coordinates
            ? TryReadUtf8Fields(out code, out x, out y)
            : TryReadRawFields(out code, out x, out y);
    }

    private bool TryReadUtf8Fields(out int code, out int x, out int y)
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
                EndIfPending();
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

    private bool TryReadRawFields(out int code, out int x, out int y)
    {
        if (_x10Length < 3)
        {
            code = 0;
            x = 0;
            y = 0;
            return false;
        }

        code = _x10[0];
        x = _x10[1];
        y = _x10[2];
        return true;
    }

    private void EmitPointer(int code, int wireX, int wireY, bool release)
    {
        var motion = (code & 32) != 0;
        var low = code & 3;

        if (code is < 0 or > 255 ||
            ((code & 128) != 0 && ((code & 64) != 0 || low is 2 or 3)))
        {
            _report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        if (wireX == 0 && wireY == 0 && motion && low == 3)
        {
            var leave = new Pointer(
                null,
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
            _report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        var source = new Point(wireX - 1, wireY - 1);
        Point? cells = source;
        Point? pixels = null;
        var inferred = false;

        if (_pixelMouse)
        {
            pixels = source;
            cells = null;

            if (_cellMetrics.Current is { } metrics && metrics.TryMap(source, out var mapped))
            {
                cells = mapped;
                inferred = true;
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
                    _ => Buttons.None
                }
                : selector switch
                {
                    0 => Buttons.Primary,
                    1 => Buttons.Middle,
                    2 => Buttons.Secondary,
                    _ => Buttons.None
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
