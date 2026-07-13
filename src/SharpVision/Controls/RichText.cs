// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Terminal.Unicode;
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
        int limit = Wrapping == Wrapping.None ? int.MaxValue : constraint.Width ?? int.MaxValue;

        if (Wrapping == Wrapping.Word)
        {
            Line[] wordLines = GetWordLines(limit);
            int wordWidth = wordLines.Length == 0 ? 0 : wordLines.Max(static line => line.Cells);
            return new Size(wordWidth, wordLines.Length);
        }

        int[] widths = GetLineWidths(limit);
        int width = widths.Length == 0 ? 0 : widths.Max();
        return new Size(width, widths.Length);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        Rect bounds = ContentBounds;

        if (Wrapping == Wrapping.Word)
        {
            RenderWordWrapped(canvas, bounds);
            return;
        }

        int limit = Wrapping == Wrapping.None ? int.MaxValue : bounds.Width;
        int[] widths = GetLineWidths(limit);
        int line = 0;
        int cells = 0;

        foreach (Inline inline in Inlines)
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
        int cells = 0;
        bool hasContent = false;

        foreach (Inline inline in Inlines)
        {
            if (inline is LineBreak)
            {
                widths.Add(cells);
                cells = 0;
                hasContent = true;
                continue;
            }

            string content = inline switch
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
        string document = GetDocument();

        if (document.Length == 0)
        {
            return [];
        }

        Line[] buffer = new Line[document.Length + 1];
        int count = TextLayout.Format(
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
        StringBuilder builder = new();

        foreach (Inline inline in Inlines)
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
        foreach (Grapheme segment in Graphemes.Enumerate(content))
        {
            ReadOnlySpan<char> cluster = content.AsSpan(segment.Offset, segment.Length);

            if (cluster.Contains('\r') || cluster.Contains('\n'))
            {
                widths.Add(cells);
                cells = 0;
                continue;
            }

            int width = Terminal.Unicode.Width.Measure(
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
        foreach (Grapheme segment in Graphemes.Enumerate(content))
        {
            ReadOnlySpan<char> cluster = content.AsSpan(segment.Offset, segment.Length);

            if (cluster.Contains('\r') || cluster.Contains('\n'))
            {
                line++;
                cells = 0;
                continue;
            }

            int width = Terminal.Unicode.Width.Measure(
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

            int leading = Align(bounds.Width, widths[line]);
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
        Line[] lines = GetWordLines(bounds.Width);
        int sourceOffset = 0;
        int line = 0;
        int cells = 0;

        foreach (Inline inline in Inlines)
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
        foreach (Grapheme segment in Graphemes.Enumerate(content))
        {
            ReadOnlySpan<char> cluster = content.AsSpan(segment.Offset, segment.Length);
            int offset = sourceOffset + segment.Offset;

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

            int width = Terminal.Unicode.Width.Measure(cluster, ambiguous).Cells;
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
        TerminalStyle inherited = ResolvedStyle;
        (TerminalAttributes attributes, Terminal.Protocols.Underline underline, Terminal.Protocols.Color underlineColor) = Decoration.Resolve(
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
        TerminalStyle inherited = ResolvedStyle;
        (TerminalAttributes attributes, Terminal.Protocols.Underline underline, Terminal.Protocols.Color underlineColor) = Decoration.Resolve(
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

    private BackgroundMode ResolveBackgroundMode(Run run) => run.Background.HasValue || ControlAppearance.HasOpaqueFill(this, GetVisualState())
        ? BackgroundMode.Opaque
        : BackgroundMode.Transparent;

    private BackgroundMode ResolveBackgroundMode(Hyperlink hyperlink) =>
        hyperlink.Background.HasValue || ControlAppearance.HasOpaqueFill(this, GetVisualState())
            ? BackgroundMode.Opaque
            : BackgroundMode.Transparent;

    private int Align(int width, int cells)
    {
        int remaining = Math.Max(0, width - cells);
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
        long result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }
}
