// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using SharpVision.Text;

using Terminal.Rendering;

using TerminalUnderline = Underline;
using TextLayout = SharpVision.Text.Layout;

/// <summary>Displays grapheme-safe inline-markup text through semantic terminal cells.</summary>
[PublicAPI]
public sealed class Text: Control, IAccessKeyCaption
{
    private const TerminalAttributes _blinkAttributes =
        TerminalAttributes.Blink | TerminalAttributes.RapidBlink;

    private string _display = string.Empty;
    private StyleSpan[] _spans = [];
    private string? _parsedContent;
    private bool _parsedHighlightMnemonic;
    private bool _parsedUseMnemonic;
    private int _cachedWidth;
    private Overflow _cachedOverflow;
    private Alignment _cachedAlignment;
    private Ambiguous _cachedAmbiguous;
    private bool _hasAmbiguousWidth;
    private Line[] _lines = [];
    private int _lineCount;
    private bool _layoutValid;
    private int _measuredMaxCells;
    private Rune? _ellipsisGlyph;

    string? IAccessKeyCaption.Text => Content;

    /// <summary>Raised after the <see cref="Content"/> property changes.</summary>
    public event EventHandler? TextChanged;

    /// <summary>Initializes empty text with documented formatting defaults.</summary>
    public Text() => UseMnemonic = false;

    /// <summary>Initializes text with non-null inline-markup content.</summary>
    /// <param name="content">The non-null markup string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    public Text(string content)
        : this()
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

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _layoutValid = false;
                TextChanged?.Invoke(this, EventArgs.Empty);
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
            EnumValidation.ValidateDefined(value);

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
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
            EnumValidation.ValidateDefined(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
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
            EnumValidation.ValidateDefined(value);
            VerifyMutable();

            if (_hasAmbiguousWidth && AmbiguousWidthValue == value)
            {
                return;
            }

            AmbiguousWidthValue = value;
            _hasAmbiguousWidth = true;
            _layoutValid = false;
            NotifyPropertyChanged(nameof(AmbiguousWidth), InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the local one-cell truncation glyph.</summary>
    public Rune EllipsisGlyph
    {
        get => _ellipsisGlyph ?? ControlGlyphs.Text.Ellipsis.Value;
        set
        {
            _ = new ControlGlyph(value, value);
            VerifyMutable();
            if (_ellipsisGlyph == value)
            {
                return;
            }

            _ellipsisGlyph = value;
            NotifyPropertyChanged(nameof(EllipsisGlyph), InvalidationImpact.Render);
        }
    }

    /// <summary>Clears the local truncation glyph to the code-owned default.</summary>
    /// <exception cref="InvalidOperationException">The attached text control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The text control is disposed.</exception>
    public void ResetEllipsisGlyph()
    {
        VerifyMutable();

        if (_ellipsisGlyph.HasValue)
        {
            _ellipsisGlyph = null;
            NotifyPropertyChanged(nameof(EllipsisGlyph), InvalidationImpact.Render);
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
        var captionOwner = Parent is Pressable { Content: var content } owner && ReferenceEquals(content, this)
            ? owner
            : null;
        var useMnemonic = captionOwner?.UseMnemonic ?? UseMnemonic;
        var highlightMnemonic = captionOwner?.EffectiveIsEnabled ?? EffectiveIsEnabled;

        if (ReferenceEquals(_parsedContent, Content) &&
            _parsedUseMnemonic == useMnemonic &&
            _parsedHighlightMnemonic == highlightMnemonic)
        {
            return;
        }

        var hotkeyColor = highlightMnemonic ? Theme?.Hotkey : null;
        _spans = Markup.Parse(SharpVision.Input.AccessKeyText.ToMarkup(Content, useMnemonic, highlightMnemonic, hotkeyColor), out _display);
        _parsedContent = Content;
        _parsedUseMnemonic = useMnemonic;
        _parsedHighlightMnemonic = highlightMnemonic;
        _layoutValid = false;
    }

    /// <inheritdoc/>
    protected override string? AccessKeyText => Content;

    private void EnsureLayout(int width)
    {
        Debug.Assert(width >= 0, "Control layout provides a non-negative content width.");
        EnsureParsed();

        if (!_layoutValid ||
            _cachedOverflow != Overflow ||
            _cachedAmbiguous != AmbiguousWidth)
        {
            Format(width);
            return;
        }

        if (_cachedWidth != width)
        {
            // The text fits in both the previous and new widths without
            // wrapping or truncation: lines are identical, only alignment
            // within the available width may change.
            if (width >= _measuredMaxCells && _cachedWidth > _measuredMaxCells)
            {
                Align(width);
                _cachedWidth = width;
                return;
            }

            Format(width);
            return;
        }

        if (_cachedWidth != width)
        {
            // The text fits in both the previous and new widths without
            // wrapping or truncation: lines are identical, only alignment
            // within the available width may change.
            if (width >= _measuredMaxCells && _cachedWidth > _measuredMaxCells)
            {
                Align(width);
                _cachedWidth = width;
                return;
            }

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

        var maxCells = 0;
        for (var index = 0; index < _lineCount; index++)
        {
            maxCells = Math.Max(maxCells, _lines[index].Cells);
        }

        _measuredMaxCells = maxCells;
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
                _ => throw new UnreachableException()
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
            var themed = ControlGlyphs.Text.Ellipsis;
            canvas.DrawRune(
                CellGlyphResolver.Resolve(EllipsisGlyph, themed.Fallback, AmbiguousWidth),
                new Point(bounds.X + line.Leading + cells, bounds.Y + row),
                ResolveSpanStyle(span),
                ResolveBackgroundMode(span));
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

        var underlineColor = span.UnderlineColor is { } configuredUnderlineColor
            ? ResolveThemeColor(configuredUnderlineColor)
            : (Color?) null;
        var (resolvedAttributes, resolvedUnderline, resolvedUnderlineColor) = DecorationResolver.Resolve(
            inherited,
            attributes,
            underline,
            underlineColor);
        return new TerminalStyle(
            span.Foreground is { } configuredForeground
                ? ResolveThemeColor(configuredForeground)
                : inherited.Foreground,
            span.Background is { } configuredBackground
                ? ResolveThemeColor(configuredBackground)
                : inherited.Background,
            resolvedAttributes,
            span.Link ?? inherited.Hyperlink,
            resolvedUnderline,
            resolvedUnderlineColor);
    }

    private static BackgroundMode ResolveBackgroundMode(StyleSpan span) =>
        span.Background.HasValue
            ? BackgroundMode.Opaque
            : BackgroundMode.Transparent;

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            TextChanged = null;
        }
    }
}
