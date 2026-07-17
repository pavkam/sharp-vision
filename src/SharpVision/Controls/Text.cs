// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Rendering;
using SharpVision.Text;

using TerminalUnderline = Underline;
using TextLayout = SharpVision.Text.Layout;

/// <summary>Displays grapheme-safe inline-markup text through semantic terminal cells.</summary>
public sealed class Text: Control
{
    private const TerminalAttributes _blinkAttributes =
        TerminalAttributes.Blink | TerminalAttributes.RapidBlink;
    private const string _ellipsis = "…";
    private string _display = string.Empty;
    private StyleSpan[] _spans = [];
    private string? _parsedContent;
    private int _cachedWidth;
    private Overflow _cachedOverflow;
    private Alignment _cachedAlignment;
    private Ambiguous _cachedAmbiguous;
    private bool _hasAmbiguousWidth;
    private Line[] _lines = [];
    private int _lineCount;
    private bool _layoutValid;

    /// <summary>Initializes empty text with documented formatting defaults.</summary>
    public Text()
    {
    }

    /// <summary>Initializes text with non-null inline-markup content.</summary>
    /// <param name="content">The non-null markup string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    public Text(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    /// <summary>Gets or sets non-null inline-markup content.</summary>
    /// <remarks>Malformed markup renders literally and never changes the setter's exception surface.</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string Content
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (SetProperty(ref field, value, ChangeImpact.Measure))
            {
                _layoutValid = false;
            }
        }
    } = string.Empty;

    /// <summary>Gets or sets how horizontal overflow is formatted.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Overflow Overflow
    {
        get;
        set
        {
            Validate(value);

            if (SetProperty(ref field, value, ChangeImpact.Measure))
            {
                _layoutValid = false;
            }
        }
    } = Overflow.Visible;

    /// <summary>Gets or sets horizontal placement within each formatted line.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Alignment TextAlignment
    {
        get;
        set
        {
            Validate(value);
            _ = SetProperty(ref field, value, ChangeImpact.Arrange);
        }
    }

    /// <summary>Gets or sets the East Asian Ambiguous cell-width policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Ambiguous AmbiguousWidth
    {
        get => _hasAmbiguousWidth ? AmbiguousWidthValue : CellPolicy.AmbiguousWidth;
        set
        {
            Validate(value);
            VerifyMutable();

            if (_hasAmbiguousWidth && AmbiguousWidthValue == value)
            {
                return;
            }

            AmbiguousWidthValue = value;
            _hasAmbiguousWidth = true;
            _layoutValid = false;
            NotifyPropertyChanged(nameof(AmbiguousWidth), ChangeImpact.Measure);
        }
    }

    private Ambiguous AmbiguousWidthValue { get; set; }

    /// <summary>Gets committed visible-text line metrics until the next successful layout.</summary>
    public ReadOnlyMemory<Line> Lines => _lines.AsMemory(0, _lineCount);

    /// <summary>Escapes dynamic visible text for safe interpolation into markup content.</summary>
    /// <param name="value">The non-null visible text.</param>
    /// <returns>The text with opening-angle and backslash metacharacters escaped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static string Escape(string value) => Markup.Escape(value);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        EnsureLayout(constraint.Width ?? int.MaxValue);
        var width = 0;

        foreach (var line in Lines.Span)
        {
            width = Math.Max(width, line.Cells);
        }

        return new Size(width, _lineCount);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => EnsureLayout(bounds.Width);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;
        EnsureLayout(bounds.Width);
        var lines = Lines.Span;

        for (var index = 0; index < lines.Length && index < bounds.Height; index++)
        {
            RenderLine(canvas, bounds, lines[index], index);
        }
    }

    private void EnsureParsed()
    {
        if (ReferenceEquals(_parsedContent, Content))
        {
            return;
        }

        _spans = Markup.Parse(Content, out _display);
        _parsedContent = Content;
        _layoutValid = false;
    }

    private void EnsureLayout(int width)
    {
        Debug.Assert(width >= 0, "Control layout provides a non-negative content width.");
        EnsureParsed();

        if (!_layoutValid ||
            _cachedWidth != width ||
            _cachedOverflow != Overflow ||
            _cachedAmbiguous != AmbiguousWidth)
        {
            Format(width);
            return;
        }

        if (_cachedAlignment != TextAlignment)
        {
            Align(width);
        }
    }

    private void Format(int width)
    {
        var required = TextLayout.Format(
            _display,
            width,
            Overflow,
            TextAlignment,
            AmbiguousWidth,
            _lines);

        if (required > _lines.Length)
        {
            Array.Resize(ref _lines, required);
            _ = TextLayout.Format(
                _display,
                width,
                Overflow,
                TextAlignment,
                AmbiguousWidth,
                _lines);
        }

        _lineCount = required;
        _cachedWidth = width;
        _cachedOverflow = Overflow;
        _cachedAlignment = TextAlignment;
        _cachedAmbiguous = AmbiguousWidth;
        _layoutValid = true;
    }

    private void Align(int width)
    {
        for (var index = 0; index < _lineCount; index++)
        {
            var line = _lines[index];
            var remaining = Math.Max(0, width - line.Cells);
            var leading = TextAlignment switch
            {
                Alignment.Start => 0,
                Alignment.Center => remaining / 2,
                Alignment.End => remaining,
                _ => throw new UnreachableException(),
            };
            _lines[index] = new Line(
                line.Offset,
                line.Length,
                line.Cells,
                leading,
                line.HasEllipsis);
        }

        _cachedAlignment = TextAlignment;
    }

    private void RenderLine(TerminalCanvas canvas, Rect bounds, Line line, int row)
    {
        var cells = 0;
        var spanIndex = SpanIndexAt(line.Offset);

        foreach (var grapheme in Graphemes.Enumerate(_display.AsSpan(line.Offset, line.Length)))
        {
            var offset = line.Offset + grapheme.Offset;
            spanIndex = AdvanceSpan(spanIndex, offset);
            var span = spanIndex >= 0 ? _spans[spanIndex] : default;
            var cluster = _display.AsSpan(offset, grapheme.Length);
            _ = canvas.Draw(
                cluster,
                new Point(bounds.X + line.Leading + cells, bounds.Y + row),
                ResolveSpanStyle(span),
                background: ResolveBackgroundMode(span));
            cells += Terminal.Unicode.Width.Measure(cluster, AmbiguousWidth).Cells;
        }

        if (line.HasEllipsis)
        {
            var span = spanIndex >= 0 ? _spans[spanIndex] : default;
            _ = canvas.Draw(
                _ellipsis,
                new Point(bounds.X + line.Leading + cells, bounds.Y + row),
                ResolveSpanStyle(span),
                background: ResolveBackgroundMode(span));
        }
    }

    private int SpanIndexAt(int offset)
    {
        for (var index = 0; index < _spans.Length; index++)
        {
            var span = _spans[index];

            if (offset >= span.Offset && offset < span.Offset + span.Length)
            {
                return index;
            }
        }

        return -1;
    }

    private int AdvanceSpan(int index, int offset)
    {
        if (index < 0)
        {
            return SpanIndexAt(offset);
        }

        while (index + 1 < _spans.Length && offset >= _spans[index].Offset + _spans[index].Length)
        {
            index++;
        }

        return index;
    }

    private TerminalStyle ResolveSpanStyle(StyleSpan span)
    {
        var inherited = ResolvedStyle;
        var attributes = inherited.Attributes;

        if ((span.Attributes & _blinkAttributes) != 0)
        {
            attributes &= ~_blinkAttributes;
        }

        attributes |= span.Attributes;
        TerminalUnderline? underline = null;

        if (span.Underline != TerminalUnderline.None)
        {
            attributes &= ~TerminalAttributes.Underline;
            underline = span.Underline;
        }

        var underlineColor = span.UnderlineColor;
        var (resolvedAttributes, resolvedUnderline, resolvedUnderlineColor) = Decoration.Resolve(
            inherited,
            attributes,
            underline,
            underlineColor);
        return new TerminalStyle(
            span.Foreground ?? inherited.Foreground,
            span.Background ?? inherited.Background,
            resolvedAttributes,
            span.Link ?? inherited.Hyperlink,
            resolvedUnderline,
            resolvedUnderlineColor);
    }

    private static BackgroundMode ResolveBackgroundMode(StyleSpan span) =>
        span.Background.HasValue
            ? BackgroundMode.Opaque
            : BackgroundMode.Transparent;

    private static void Validate<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
        }
    }
}
