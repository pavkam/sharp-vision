// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Text;

using TextLayout = SharpVision.Text.Layout;

/// <summary>Displays owned styled runs, line breaks, and semantic hyperlinks.</summary>
public sealed class RichText: Control
{
    #region Construction and properties

    /// <summary>Initializes an empty mutable inline document.</summary>
    public RichText() => Inlines = new Inlines(this);

    /// <summary>Gets the ordered single-owner inline collection.</summary>
    public Inlines Inlines { get; }

    /// <summary>Gets or sets the logical-line wrapping policy.</summary>
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
    } = Wrapping.Word;

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
    protected override Size MeasureOverride(Constraint constraint)
    {
        var limit = Wrapping == Wrapping.None ? int.MaxValue : constraint.Width ?? int.MaxValue;

        if (Wrapping == Wrapping.Word)
        {
            var wordLines = GetWordLines(limit);
            var wordWidth = wordLines.Length == 0 ? 0 : wordLines.Max(static line => line.Cells);
            return new Size(wordWidth, wordLines.Length);
        }

        var widths = GetLineWidths(limit);
        var width = widths.Length == 0 ? 0 : widths.Max();
        return new Size(width, widths.Length);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;

        if (Wrapping == Wrapping.Word)
        {
            RenderWordWrapped(canvas, bounds);
            return;
        }

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
                        ResolveBackgroundMode(run),
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
                        ResolveBackgroundMode(hyperlink),
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
        List<int> widths = [];
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

    private Line[] GetWordLines(int width)
    {
        // Format the whole inline sequence so a word can span styled Run and Hyperlink boundaries.
        var document = GetDocument();

        if (document.Length == 0)
        {
            return [];
        }

        var buffer = new Line[document.Length + 1];
        var count = TextLayout.Format(
            document,
            width,
            Wrapping.Word,
            Trimming.None,
            TextAlignment,
            CellPolicy.AmbiguousWidth,
            buffer);
        return buffer[..count];
    }

    private string GetDocument()
    {
        var builder = new StringBuilder();

        foreach (var inline in Inlines)
        {
            _ = builder.Append(inline switch
            {
                LineBreak => "\n",
                Run run => run.Content,
                Hyperlink hyperlink => hyperlink.Content,
                _ => throw new UnreachableException(),
            });
        }

        return builder.ToString();
    }

    private void MeasureText(string content, int limit, List<int> widths, ref int cells)
    {
        Debug.Assert(content is not null, "RichText measurement requires non-null content.");
        Debug.Assert(widths is not null, "RichText measurement requires a non-null width list.");
        Debug.Assert(limit >= 0, "RichText measurement limit is non-negative.");

        foreach (var segment in Graphemes.Enumerate(content))
        {
            var cluster = content.AsSpan(segment.Offset, segment.Length);

            if (cluster.Contains('\r') || cluster.Contains('\n'))
            {
                widths.Add(cells);
                cells = 0;
                continue;
            }

            var width = Terminal.Unicode.Width.Measure(
                cluster,
                CellPolicy.AmbiguousWidth).Cells;

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
        BackgroundMode background,
        ref int line,
        ref int cells)
    {
        Debug.Assert(widths is not null, "RichText rendering requires line widths.");
        Debug.Assert(content is not null, "RichText rendering requires non-null content.");
        Debug.Assert(line >= 0, "RichText render line index is non-negative.");
        Debug.Assert(cells >= 0, "RichText render cell offset is non-negative.");

        foreach (var segment in Graphemes.Enumerate(content))
        {
            var cluster = content.AsSpan(segment.Offset, segment.Length);

            if (cluster.Contains('\r') || cluster.Contains('\n'))
            {
                line++;
                cells = 0;
                continue;
            }

            var width = Terminal.Unicode.Width.Measure(
                cluster,
                CellPolicy.AmbiguousWidth).Cells;

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
            _ = canvas.Draw(
                cluster,
                new Point(bounds.X + leading + cells, bounds.Y + line),
                style,
                background: background);
            cells = Add(cells, width);
        }
    }

    private void RenderWordWrapped(TerminalCanvas canvas, Rect bounds)
    {
        // Reuse the shared source offsets from Text.Layout while applying each inline's own rendition.
        var lines = GetWordLines(bounds.Width);
        var sourceOffset = 0;
        var line = 0;
        var cells = 0;

        foreach (var inline in Inlines)
        {
            switch (inline)
            {
                case LineBreak:
                    sourceOffset++;
                    break;
                case Run run:
                    RenderWordText(
                        canvas,
                        bounds,
                        lines,
                        run.Content,
                        ResolveInlineStyle(run),
                        ResolveBackgroundMode(run),
                        CellPolicy.AmbiguousWidth,
                        ref sourceOffset,
                        ref line,
                        ref cells);
                    break;
                case Hyperlink hyperlink:
                    RenderWordText(
                        canvas,
                        bounds,
                        lines,
                        hyperlink.Content,
                        ResolveInlineStyle(hyperlink),
                        ResolveBackgroundMode(hyperlink),
                        CellPolicy.AmbiguousWidth,
                        ref sourceOffset,
                        ref line,
                        ref cells);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
    }

    private static void RenderWordText(
        TerminalCanvas canvas,
        Rect bounds,
        Line[] lines,
        string content,
        TerminalStyle style,
        BackgroundMode background,
        Ambiguous ambiguous,
        ref int sourceOffset,
        ref int line,
        ref int cells)
    {
        Debug.Assert(lines is not null, "RichText word rendering requires layout lines.");
        Debug.Assert(content is not null, "RichText word rendering requires non-null content.");
        Debug.Assert(sourceOffset >= 0, "RichText source offset is non-negative.");
        Debug.Assert(line >= 0, "RichText word render line index is non-negative.");
        Debug.Assert(cells >= 0, "RichText word render cell offset is non-negative.");

        foreach (var segment in Graphemes.Enumerate(content))
        {
            var cluster = content.AsSpan(segment.Offset, segment.Length);
            var offset = sourceOffset + segment.Offset;

            if (cluster.Contains('\r') || cluster.Contains('\n'))
            {
                continue;
            }

            while (line < lines.Length && offset >= lines[line].Offset + lines[line].Length)
            {
                line++;
                cells = 0;
            }

            if (line >= lines.Length || line >= bounds.Height || offset < lines[line].Offset)
            {
                continue;
            }

            var width = Terminal.Unicode.Width.Measure(cluster, ambiguous).Cells;
            _ = canvas.Draw(
                cluster,
                new Point(bounds.X + lines[line].Leading + cells, bounds.Y + line),
                style,
                background: background);
            cells = Add(cells, width);
        }

        sourceOffset += content.Length;
    }

    private TerminalStyle ResolveInlineStyle(Run run)
    {
        var inherited = ResolvedStyle;
        (var attributes, var underline, var underlineColor) = Decoration.Resolve(
            inherited,
            run.Attributes,
            run.Underline,
            run.UnderlineColor);
        return new TerminalStyle(
            run.Foreground ?? inherited.Foreground,
            run.Background ?? inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
    }

    private TerminalStyle ResolveInlineStyle(Hyperlink hyperlink)
    {
        var inherited = ResolvedStyle;
        (var attributes, var underline, var underlineColor) = Decoration.Resolve(
            inherited,
            hyperlink.Attributes,
            hyperlink.Underline,
            hyperlink.UnderlineColor);
        return new TerminalStyle(
            hyperlink.Foreground ?? inherited.Foreground,
            hyperlink.Background ?? inherited.Background,
            attributes,
            hyperlink.Target,
            underline,
            underlineColor);
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "RichText accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "RichText accumulation uses non-negative extents.");

        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private BackgroundMode ResolveBackgroundMode(Run run)
    {
        return run.Background.HasValue || ControlAppearance.HasOpaqueFill(this, GetVisualState())
            ? BackgroundMode.Opaque
            : BackgroundMode.Transparent;
    }

    private BackgroundMode ResolveBackgroundMode(Hyperlink hyperlink)
    {
        return hyperlink.Background.HasValue || ControlAppearance.HasOpaqueFill(this, GetVisualState())
            ? BackgroundMode.Opaque
            : BackgroundMode.Transparent;
    }

    private int Align(int width, int cells)
    {
        Debug.Assert(width >= 0, "RichText alignment width is non-negative.");
        Debug.Assert(cells >= 0, "RichText alignment cell count is non-negative.");

        var remaining = Math.Max(0, width - cells);
        return TextAlignment switch
        {
            Alignment.Start => 0,
            Alignment.Center => remaining / 2,
            Alignment.End => remaining,
            _ => throw new UnreachableException(),
        };
    }
}
