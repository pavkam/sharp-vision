// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Test.Shared;

/// <summary>Applies emitted terminal bytes to an independent semantic component screen.</summary>
public sealed class ComponentScreen: ISequenceSink
{
    private readonly Lock _gate = new();
    private readonly SurfaceCell[] _cells;
    private Point _position;
    private Color _foreground;
    private Color _background;
    private Color _underlineColor;
    private TerminalAttributes _attributes;
    private Underline _underline;
    private string? _hyperlink;
    private int _scrollTop;
    private int _scrollBottom;
    private bool CursorVisibleValue { get; set; } = true;
    private CursorShape CursorShapeValue { get; set; }

    /// <summary>Initializes a blank screen with positive dimensions.</summary>
    /// <param name="size">The positive terminal surface dimensions.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    public ComponentScreen(Size size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height);
        Size = size;
        _scrollBottom = size.Height - 1;
        _cells = new SurfaceCell[checked(size.Width * size.Height)];

        for (var index = 0; index < _cells.Length; index++)
        {
            _cells[index] = SurfaceCell.Blank;
        }
    }

    /// <summary>Gets the fixed terminal surface dimensions.</summary>
    public Size Size { get; }

    /// <summary>Gets the latest terminal cursor position under the screen lock.</summary>
    public Point CursorPosition
    {
        get
        {
            lock (_gate)
            {
                return _position;
            }
        }
    }

    /// <summary>Gets whether the latest terminal cursor mode is visible under the screen lock.</summary>
    public bool CursorVisible
    {
        get
        {
            lock (_gate)
            {
                return CursorVisibleValue;
            }
        }
    }

    /// <summary>Gets the latest modeled semantic cursor shape under the screen lock.</summary>
    public CursorShape CursorShape
    {
        get
        {
            lock (_gate)
            {
                return CursorShapeValue;
            }
        }
    }

    /// <summary>Applies one complete encoded terminal write.</summary>
    /// <param name="value">The complete emitted bytes.</param>
    public void Apply(ReadOnlySpan<byte> value)
    {
        lock (_gate)
        {
            using ProtocolParser parser = new();
            var sink = this;
            parser.Parse(value, ref sink);
            parser.Complete(ref sink);
        }
    }

    /// <summary>Gets an immutable copy of one in-bounds semantic cell.</summary>
    /// <param name="point">The zero-based surface coordinate.</param>
    /// <returns>The copied semantic cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="point"/> is outside the surface.</exception>
    public SurfaceCell Cell(Point point)
    {
        lock (_gate)
        {
            return _cells[Index(point)];
        }
    }

    /// <summary>Copies the complete screen as fixed-width newline-separated text.</summary>
    /// <returns>One row per surface cell row, including blank cells.</returns>
    public string CopyText()
    {
        lock (_gate)
        {
            var value = new StringBuilder();

            for (var y = 0; y < Size.Height; y++)
            {
                if (y > 0)
                {
                    _ = value.Append('\n');
                }

                for (var x = 0; x < Size.Width; x++)
                {
                    var cell = _cells[IndexUnchecked(new Point(x, y))];

                    if (!cell.Continuation)
                    {
                        _ = value.Append(cell.Text);
                    }
                }
            }

            return value.ToString();
        }
    }

    /// <inheritdoc/>
    public void Text(ReadOnlySpan<byte> value)
    {
        var text = Encoding.UTF8.GetString(value);

        foreach (var segment in Graphemes.Enumerate(text.AsSpan()))
        {
            var cluster = text.AsSpan(segment.Offset, segment.Length);
            var width = Width.Measure(cluster).Cells;

            if (width > 0)
            {
                Write(cluster.ToString(), width);
            }
        }
    }

    /// <inheritdoc/>
    public void Control(byte value) => _ = value;

    /// <inheritdoc/>
    public void Escape(ReadOnlySpan<byte> intermediates, byte final)
    {
        _ = intermediates;
        _ = final;
    }

    /// <inheritdoc/>
    public void Csi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        if (final is (byte) 'H' or (byte) 'f')
        {
            var (row, column) = ParsePosition(parameters);
            _position = new Point(column - 1, row - 1);
        }
        else if (final == (byte) 'm')
        {
            ApplySgr(parameters);
        }
        else if (parameters.SequenceEqual("?25"u8) && final is (byte) 'h' or (byte) 'l')
        {
            CursorVisibleValue = final == (byte) 'h';
        }
        else if (final == (byte) 'q' && intermediates.SequenceEqual(" "u8))
        {
            var value = int.Parse(parameters, CultureInfo.InvariantCulture);
            CursorShapeValue = value switch
            {
                3 or 4 => CursorShape.Underline,
                5 or 6 => CursorShape.Bar,
                _ => CursorShape.Block
            };
        }
        else if (final == (byte) 'r' && intermediates.IsEmpty)
        {
            ApplyScrollRegion(parameters);
        }
        else if (final is (byte) 'S' or (byte) 'T' && intermediates.IsEmpty)
        {
            var count = parameters.IsEmpty ? 1 : Math.Max(1, int.Parse(parameters, CultureInfo.InvariantCulture));
            Scroll(count, up: final == (byte) 'S');
        }
    }

    /// <inheritdoc/>
    public void Sequence(SequenceKind kind, ReadOnlySpan<byte> value, StringTerminator terminator)
    {
        _ = terminator;

        if (kind != SequenceKind.Osc || !value.StartsWith("8;"u8))
        {
            return;
        }

        var separator = value[2..].IndexOf((byte) ';');

        if (separator >= 0)
        {
            var uri = value[(separator + 3)..];
            _hyperlink = uri.IsEmpty ? null : Encoding.UTF8.GetString(uri);
        }
    }

    /// <inheritdoc/>
    public void Dcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        _ = parameters;
        _ = intermediates;
        _ = final;
        _ = value;
        _ = terminator;
    }

    /// <inheritdoc/>
    public void Report(in Diagnostic value) => throw new InvalidOperationException(value.ToString());

    private void ApplySgr(ReadOnlySpan<byte> parameters)
    {
        var text = Encoding.ASCII.GetString(parameters);
        var values = text.Length == 0 ? ["0"] : text.Split(';');

        for (var index = 0; index < values.Length; index++)
        {
            if (values[index].StartsWith("4:", StringComparison.Ordinal))
            {
                ApplyTypedUnderline(values[index]);
                continue;
            }

            var value = ParseNumber(values[index]);

            switch (value)
            {
                case 0:
                    ResetStyle();
                    break;
                case 1:
                    _attributes |= TerminalAttributes.Bold;
                    break;
                case 2:
                    _attributes |= TerminalAttributes.Dim;
                    break;
                case 3:
                    _attributes |= TerminalAttributes.Italic;
                    break;
                case 4:
                    _attributes |= TerminalAttributes.Underline;
                    _underline = Underline.None;
                    break;
                case 5:
                    _attributes |= TerminalAttributes.Blink;
                    break;
                case 6:
                    _attributes |= TerminalAttributes.RapidBlink;
                    break;
                case 7:
                    _attributes |= TerminalAttributes.Reverse;
                    break;
                case 8:
                    _attributes |= TerminalAttributes.Hidden;
                    break;
                case 9:
                    _attributes |= TerminalAttributes.Strike;
                    break;
                case 22:
                    _attributes &= ~(TerminalAttributes.Bold | TerminalAttributes.Dim);
                    break;
                case 23:
                    _attributes &= ~TerminalAttributes.Italic;
                    break;
                case 24:
                    _attributes &= ~TerminalAttributes.Underline;
                    _underline = Underline.None;
                    break;
                case 25:
                    _attributes &= ~(TerminalAttributes.Blink | TerminalAttributes.RapidBlink);
                    break;
                case 27:
                    _attributes &= ~TerminalAttributes.Reverse;
                    break;
                case 28:
                    _attributes &= ~TerminalAttributes.Hidden;
                    break;
                case 29:
                    _attributes &= ~TerminalAttributes.Strike;
                    break;
                case >= 30 and <= 37:
                    _foreground = ReferenceColors.Get(value - 30);
                    break;
                case 38:
                    _foreground = ParseColor(values, ref index);
                    break;
                case 39:
                    _foreground = Color.Default;
                    break;
                case >= 40 and <= 47:
                    _background = ReferenceColors.Get(value - 40);
                    break;
                case 48:
                    _background = ParseColor(values, ref index);
                    break;
                case 49:
                    _background = Color.Default;
                    break;
                case 53:
                    _attributes |= TerminalAttributes.Overline;
                    break;
                case 55:
                    _attributes &= ~TerminalAttributes.Overline;
                    break;
                case 58:
                    _underlineColor = ParseColor(values, ref index);
                    break;
                case 59:
                    _underlineColor = Color.Default;
                    break;
                case >= 90 and <= 97:
                    _foreground = ReferenceColors.Get(value - 90 + 8);
                    break;
                case >= 100 and <= 107:
                    _background = ReferenceColors.Get(value - 100 + 8);
                    break;
                default:
                    break;
            }
        }
    }

    private void ApplyTypedUnderline(string parameter)
    {
        var value = int.Parse(parameter.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture);

        if (!Enum.IsDefined((Underline) value))
        {
            throw new InvalidOperationException("The component screen received an unknown underline variant.");
        }

        _attributes &= ~TerminalAttributes.Underline;
        _underline = (Underline) value;
    }

    private void ResetStyle()
    {
        _foreground = Color.Default;
        _background = Color.Default;
        _underlineColor = Color.Default;
        _attributes = TerminalAttributes.None;
        _underline = Underline.None;
    }

    private void Write(string value, int width)
    {
        if (!IsPositionInBounds())
        {
            return;
        }

        Repair(_position.X, _position.Y);

        if (width == 2 && _position.X + 1 < Size.Width)
        {
            Repair(_position.X + 1, _position.Y);
        }

        var style = new TerminalStyle(
            _foreground,
            _background,
            _attributes,
            _hyperlink,
            _underline,
            _underlineColor);
        _cells[IndexUnchecked(_position)] = new SurfaceCell(value, style, width, false, _position.X);

        if (width == 2 && _position.X + 1 < Size.Width)
        {
            _cells[IndexUnchecked(new Point(_position.X + 1, _position.Y))] =
                new SurfaceCell(value, style, 0, true, _position.X);
        }

        _position = new Point(_position.X + width, _position.Y);
    }

    private void Repair(int x, int y)
    {
        var cell = _cells[IndexUnchecked(new Point(x, y))];
        var leadX = cell.Continuation ? cell.LeadX : x;
        var lead = _cells[IndexUnchecked(new Point(leadX, y))];

        for (var offset = 0; offset < Math.Max(1, lead.Width) && leadX + offset < Size.Width; offset++)
        {
            _cells[IndexUnchecked(new Point(leadX + offset, y))] = SurfaceCell.Blank with { Style = lead.Style };
        }
    }

    private void ApplyScrollRegion(ReadOnlySpan<byte> parameters)
    {
        if (parameters.IsEmpty)
        {
            _scrollTop = 0;
            _scrollBottom = Size.Height - 1;
        }
        else
        {
            var (top, bottom) = ParsePosition(parameters);
            _scrollTop = Math.Clamp(top - 1, 0, Size.Height - 1);
            _scrollBottom = Math.Clamp(bottom - 1, _scrollTop, Size.Height - 1);
        }

        _position = default;
    }

    private void Scroll(int requestedCount, bool up)
    {
        var height = _scrollBottom - _scrollTop + 1;
        var count = Math.Min(requestedCount, height);

        if (up)
        {
            for (var row = _scrollTop; row <= _scrollBottom - count; row++)
            {
                CopyRow(row + count, row);
            }

            ClearRows(_scrollBottom - count + 1, _scrollBottom);
            return;
        }

        for (var row = _scrollBottom; row >= _scrollTop + count; row--)
        {
            CopyRow(row - count, row);
        }

        ClearRows(_scrollTop, _scrollTop + count - 1);
    }

    private void CopyRow(int source, int destination)
    {
        var sourceOffset = checked(source * Size.Width);
        var destinationOffset = checked(destination * Size.Width);
        _cells.AsSpan(sourceOffset, Size.Width).CopyTo(_cells.AsSpan(destinationOffset, Size.Width));
    }

    private void ClearRows(int first, int last)
    {
        for (var row = first; row <= last; row++)
        {
            _cells.AsSpan(checked(row * Size.Width), Size.Width).Fill(SurfaceCell.Blank);
        }
    }

    private int Index(Point point) => new Rect(0, 0, Size.Width, Size.Height).Contains(point)
        ? IndexUnchecked(point)
        : throw new ArgumentOutOfRangeException(nameof(point), point, "The point is outside the component surface.");

    private int IndexUnchecked(Point point) => checked((point.Y * Size.Width) + point.X);

    private bool IsPositionInBounds() =>
        _position.X >= 0 &&
        _position.X < Size.Width &&
        _position.Y >= 0 &&
        _position.Y < Size.Height;

    private static (int Row, int Column) ParsePosition(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return (1, 1);
        }

        var text = Encoding.ASCII.GetString(value);
        var values = text.Split(';');
        var row = values.Length > 0 && values[0].Length > 0 ? ParseNumber(values[0]) : 1;
        var column = values.Length > 1 && values[1].Length > 0 ? ParseNumber(values[1]) : 1;
        return (row, column);
    }

    private static Color ParseColor(string[] values, ref int index)
    {
        var mode = ParseNumber(values[++index]);

        return mode == 5
            ? ReferenceColors.Get(ParseNumber(values[++index]))
            : mode == 2
                ? Color.Rgb(
                    ParseNumber(values[++index]),
                    ParseNumber(values[++index]),
                    ParseNumber(values[++index]))
                : throw new InvalidOperationException("The component screen received an unknown color mode.");
    }

    private static int ParseNumber(string value) =>
        int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
}
