using System.Globalization;
using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Unicode;

using Shouldly;

using TerminalColor = SharpVision.Terminal.Protocols.Color;

namespace SharpVision.Terminal.Tests.Support;

/// <summary>
/// Applies emitted terminal bytes to an independent semantic screen model.
/// </summary>
internal sealed class VirtualScreen: ISequenceSink
{
    private readonly ModelCell[] _cells;
    private Point _position;
    private TerminalColor _foreground;
    private TerminalColor _background;
    private Attributes _attributes;
    private string? _hyperlink;

    /// <summary>Initializes a blank virtual screen.</summary>
    /// <param name="size">The non-negative screen size.</param>
    internal VirtualScreen(Size size)
    {
        Size = size;
        _cells = new ModelCell[checked(size.Width * size.Height)];

        for (var index = 0; index < _cells.Length; index++)
        {
            _cells[index] = ModelCell.Blank;
        }
    }

    /// <summary>Gets the virtual dimensions.</summary>
    internal Size Size { get; }

    /// <summary>Gets whether the modeled cursor is visible.</summary>
    internal bool CursorVisible { get; private set; } = true;

    /// <summary>Applies one complete encoded batch.</summary>
    /// <param name="value">The encoded bytes.</param>
    internal void Apply(ReadOnlySpan<byte> value)
    {
        using var parser = new Parser();
        var sink = this;
        parser.Parse(value, ref sink);
        parser.Complete(ref sink);
    }

    /// <summary>Asserts this model equals a semantic frame.</summary>
    /// <param name="frame">The expected frame.</param>
    internal void ShouldMatch(Frame frame)
    {
        Size.ShouldBe(frame.Size);

        for (var y = 0; y < Size.Height; y++)
        {
            for (var x = 0; x < Size.Width; x++)
            {
                var point = new Point(x, y);
                var expected = frame.GetCell(point);
                var actual = _cells[Index(point)];
                var expectedText = FrameText(frame, point);

                actual.Text.ShouldBe(expectedText.Length == 0 ? " " : expectedText);
                actual.Style.ShouldBe(expected.Style);
                actual.IsContinuation.ShouldBe(expected.IsContinuation);
                actual.Width.ShouldBe(expected.Width);
            }
        }

        _position.ShouldBe(frame.Cursor.Position);
        CursorVisible.ShouldBe(frame.Cursor.Visible);
    }

    /// <summary>Asserts two independently applied models are equivalent.</summary>
    /// <param name="other">The comparison model.</param>
    internal void ShouldMatch(VirtualScreen other)
    {
        Size.ShouldBe(other.Size);
        _cells.ShouldBe(other._cells);
        _position.ShouldBe(other._position);
        CursorVisible.ShouldBe(other.CursorVisible);
    }

    /// <inheritdoc/>
    public void Text(ReadOnlySpan<byte> value)
    {
        var text = Encoding.UTF8.GetString(value);

        foreach (var segment in Graphemes.Enumerate(text.AsSpan()))
        {
            var cluster = text.AsSpan(segment.Offset, segment.Length);
            var width = (int) Width.GetCluster(cluster, Ambiguous.Narrow, segment.HasInvalidData);

            if (width > 0)
            {
                Write(cluster.ToString(), width);
            }
        }
    }

    /// <inheritdoc/>
    public void Control(byte value)
    {
    }

    /// <inheritdoc/>
    public void Escape(ReadOnlySpan<byte> intermediates, byte final)
    {
    }

    /// <inheritdoc/>
    public void Csi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final)
    {
        if (final == (byte) 'H')
        {
            var values = Parse(parameters);
            _position = new Point(values[1] - 1, values[0] - 1);
        }
        else if (final == (byte) 'm')
        {
            ApplySgr(Parse(parameters));
        }
        else if (parameters.SequenceEqual("?25"u8) && final is (byte) 'h' or (byte) 'l')
        {
            CursorVisible = final == (byte) 'h';
        }
    }

    /// <inheritdoc/>
    public void Sequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
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
    }

    /// <inheritdoc/>
    public void Report(in Diagnostic value) => throw new InvalidOperationException(value.ToString());

    private void ApplySgr(int[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            switch (values[index])
            {
                case 0:
                    _foreground = TerminalColor.Default;
                    _background = TerminalColor.Default;
                    _attributes = Attributes.None;
                    break;
                case 1:
                    _attributes |= Attributes.Bold;
                    break;
                case 2:
                    _attributes |= Attributes.Dim;
                    break;
                case 3:
                    _attributes |= Attributes.Italic;
                    break;
                case 4:
                    _attributes |= Attributes.Underline;
                    break;
                case 5:
                case 6:
                    _attributes |= Attributes.Blink;
                    break;
                case 7:
                    _attributes |= Attributes.Reverse;
                    break;
                case 8:
                    _attributes |= Attributes.Hidden;
                    break;
                case 9:
                    _attributes |= Attributes.Strike;
                    break;
                case 38:
                    _foreground = ParseColor(values, ref index);
                    break;
                case 48:
                    _background = ParseColor(values, ref index);
                    break;
                case 39:
                    _foreground = TerminalColor.Default;
                    break;
                case 49:
                    _background = TerminalColor.Default;
                    break;
                default:
                    break;
            }
        }
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

        var style = CurrentStyle;
        _cells[Index(_position)] = new ModelCell(value, style, width, false, _position.X);

        if (width == 2 && _position.X + 1 < Size.Width)
        {
            _cells[Index(new Point(_position.X + 1, _position.Y))] =
                new ModelCell(value, style, 0, true, _position.X);
        }

        _position = new Point(_position.X + width, _position.Y);
    }

    private void Repair(int x, int y)
    {
        var cell = _cells[Index(new Point(x, y))];
        var leadX = cell.IsContinuation ? cell.LeadX : x;
        var lead = _cells[Index(new Point(leadX, y))];

        for (var offset = 0; offset < Math.Max(1, lead.Width) && leadX + offset < Size.Width; offset++)
        {
            _cells[Index(new Point(leadX + offset, y))] = ModelCell.Blank with
            {
                Style = lead.Style,
            };
        }
    }

    private Style CurrentStyle => new(_foreground, _background, _attributes, _hyperlink);

    private bool IsPositionInBounds() =>
        _position.X >= 0 &&
        _position.X < Size.Width &&
        _position.Y >= 0 &&
        _position.Y < Size.Height;

    private int Index(Point point) => checked((point.Y * Size.Width) + point.X);

    private static int[] Parse(ReadOnlySpan<byte> value)
    {
        var text = Encoding.ASCII.GetString(value);
        return [.. text.Split(';').Select(static item =>
            int.Parse(item, NumberStyles.None, CultureInfo.InvariantCulture))];
    }

    private static TerminalColor ParseColor(int[] values, ref int index)
    {
        var mode = values[++index];

        return mode == 5
            ? TerminalColor.Indexed(values[++index])
            : mode == 2
            ? TerminalColor.Rgb(
                values[++index],
                values[++index],
                values[++index])
            : throw new InvalidOperationException("The virtual screen received an unknown color mode.");
    }

    private static string FrameText(Frame frame, Point point)
    {
        var length = frame.GetGraphemeByteCount(point);

        if (length == 0)
        {
            return string.Empty;
        }

        var bytes = new byte[length];
        _ = frame.CopyGrapheme(point, bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private readonly record struct ModelCell(
        string Text,
        Style Style,
        int Width,
        bool IsContinuation,
        int LeadX)
    {
        /// <summary>Gets the default blank modeled cell.</summary>
        internal static ModelCell Blank { get; } = new(" ", Style.Default, 1, false, 0);
    }
}
