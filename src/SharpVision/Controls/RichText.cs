using System.Diagnostics;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Unicode;
using SharpVision.Text;

using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

/// <summary>Displays owned styled runs, line breaks, and semantic hyperlinks.</summary>
public sealed class RichText: Control
{
    #region Construction and properties

    /// <summary>Initializes an empty mutable inline document.</summary>
    public RichText() => Inlines = new Inlines(this);

    /// <summary>Gets the ordered single-owner inline collection.</summary>
    public Inlines Inlines { get; }

    /// <summary>Gets or sets the grapheme wrapping policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public Wrapping Wrapping
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The wrapping policy is unknown.");
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Gets or sets horizontal placement for each formatted line.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public Alignment TextAlignment
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The alignment is unknown.");
            }

            _ = Set(ref field, value, Invalidation.Render);
        }
    }

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        var limit = Wrapping == Wrapping.None ? int.MaxValue : constraint.Width ?? int.MaxValue;
        var widths = GetLineWidths(limit);
        var width = widths.Length == 0 ? 0 : widths.Max();
        return new Size(width, widths.Length);
    }

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;
        var limit = Wrapping == Wrapping.None ? int.MaxValue : bounds.Width;
        var widths = GetLineWidths(limit);
        var line = 0;
        var cells = 0;

        foreach (var inline in Inlines)
        {
            switch (inline)
            {
                case LineBreak:
                    line++;
                    cells = 0;
                    break;
                case Run run:
                    RenderText(
                        canvas,
                        bounds,
                        widths,
                        run.Content,
                        ResolveInlineStyle(run),
                        ref line,
                        ref cells);
                    break;
                case Hyperlink hyperlink:
                    RenderText(
                        canvas,
                        bounds,
                        widths,
                        hyperlink.Content,
                        ResolveInlineStyle(hyperlink),
                        ref line,
                        ref cells);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
    }

    #endregion

    /// <summary>Invalidates layout after one owned inline mutation.</summary>
    internal void InlineChanged() => Invalidate(Invalidation.Measure);

    /// <inheritdoc/>
    internal override void DisposeChildren() => Inlines.Clear();

    private int[] GetLineWidths(int limit)
    {
        var widths = new List<int>();
        var cells = 0;
        var hasContent = false;

        foreach (var inline in Inlines)
        {
            if (inline is LineBreak)
            {
                widths.Add(cells);
                cells = 0;
                hasContent = true;
                continue;
            }

            var content = inline switch
            {
                Run run => run.Content,
                Hyperlink hyperlink => hyperlink.Content,
                _ => throw new UnreachableException(),
            };

            hasContent |= content.Length != 0;
            MeasureText(content, limit, widths, ref cells);
        }

        if (hasContent)
        {
            widths.Add(cells);
        }

        return [.. widths];
    }

    private void MeasureText(string content, int limit, List<int> widths, ref int cells)
    {
        foreach (var segment in Graphemes.Enumerate(content))
        {
            var cluster = content.AsSpan(segment.Offset, segment.Length);

            if (cluster.Contains('\r') || cluster.Contains('\n'))
            {
                widths.Add(cells);
                cells = 0;
                continue;
            }

            var width = Terminal.Unicode.Width.Measure(cluster).Cells;

            if (Wrapping != Wrapping.None && cells > 0 && width > limit - cells)
            {
                widths.Add(cells);
                cells = 0;
            }

            cells = Add(cells, width);
        }
    }

    private void RenderText(
        TerminalCanvas canvas,
        Rect bounds,
        int[] widths,
        string content,
        TerminalStyle style,
        ref int line,
        ref int cells)
    {
        foreach (var segment in Graphemes.Enumerate(content))
        {
            var cluster = content.AsSpan(segment.Offset, segment.Length);

            if (cluster.Contains('\r') || cluster.Contains('\n'))
            {
                line++;
                cells = 0;
                continue;
            }

            var width = Terminal.Unicode.Width.Measure(cluster).Cells;

            if (Wrapping != Wrapping.None && cells > 0 && width > bounds.Width - cells)
            {
                line++;
                cells = 0;
            }

            if (line >= bounds.Height || line >= widths.Length)
            {
                cells = Add(cells, width);
                continue;
            }

            var leading = Align(bounds.Width, widths[line]);
            _ = canvas.Draw(cluster, new Point(bounds.X + leading + cells, bounds.Y + line), style);
            cells = Add(cells, width);
        }
    }

    private TerminalStyle ResolveInlineStyle(Run run)
    {
        var inherited = ResolvedStyle;
        return new TerminalStyle(
            run.Foreground ?? inherited.Foreground,
            run.Background ?? inherited.Background,
            run.Attributes ?? inherited.Attributes,
            inherited.Hyperlink);
    }

    private TerminalStyle ResolveInlineStyle(Hyperlink hyperlink)
    {
        var inherited = ResolvedStyle;
        return new TerminalStyle(
            hyperlink.Foreground ?? inherited.Foreground,
            hyperlink.Background ?? inherited.Background,
            hyperlink.Attributes ?? inherited.Attributes,
            hyperlink.Target);
    }

    private int Align(int width, int cells)
    {
        var remaining = Math.Max(0, width - cells);
        return TextAlignment switch
        {
            Alignment.Start => 0,
            Alignment.Center => remaining / 2,
            Alignment.End => remaining,
            _ => throw new UnreachableException(),
        };
    }

    private static int Add(int left, int right)
    {
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }
}
