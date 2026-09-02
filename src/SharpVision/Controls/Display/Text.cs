// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using SharpVision.Text;

using Terminal.Rendering;

using TerminalUnderline = Underline;
using TextLayout = SharpVision.Text.Layout;
using UnicodeWidth = Width;

/// <summary>Displays grapheme-safe inline-markup text through semantic terminal cells.</summary>
/// <remarks>Parsed spans cache the effective caption-owned mnemonic settings and resolved hotkey
/// color. An active marked mnemonic registers a render-only dependency on
/// <see cref="Theme.Hotkey"/> so a theme replacement reparses before the next frame without
/// remeasuring unchanged text geometry.</remarks>
[PublicAPI]
public sealed class Text: ControlBase, IAccessKeyCaption, IStyled<TextStyle>
{
    private const TerminalAttributes _blinkAttributes =
        TerminalAttributes.Blink | TerminalAttributes.RapidBlink;

    private static readonly ThemeValueDependency<Color> _hotkeyThemeDependency = new(
        static theme => theme.Hotkey,
        InvalidationImpact.Render);

    private string _display = string.Empty;
    private StyleSpan[] _spans = [];
    private string? _parsedContent;
    private bool _parsedHighlightMnemonic;
    private bool _parsedUseMnemonic;
    private Color? _parsedHotkeyColor;
    private int _cachedWidth;
    private Overflow _cachedOverflow;
    private Alignment _cachedAlignment;
    private Ambiguous _cachedAmbiguous;
    private Line[] _lines = [];
    private int _lineCount;
    private bool _layoutValid;
    private int _measuredMaxCells;
    private readonly StyleSlot<TextStyle> _style;

    string? IAccessKeyCaption.Text => Content;

    /// <summary>Raised after the <see cref="Content"/> property changes.</summary>
    public event EventHandler? TextChanged;

    /// <summary>Initializes empty text with documented formatting defaults.</summary>
    public Text()
    {
        _style = InitializeStyle(TextStyle.Definition);
        UseMnemonic = false;
    }

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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);

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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
        }
    }

    /// <summary>Gets the East Asian Ambiguous cell-width policy inherited from the ambient
    /// <see cref="ControlBase.CellPolicy"/>.</summary>
    /// <remarks>
    /// This always tracks the ambient policy the rest of the render pipeline uses - it cannot be
    /// overridden per-instance. <see cref="TerminalCanvas"/> classifies every
    /// rune it draws against the frame's single ambiguous-width policy, so a <see cref="Text"/>
    /// whose own layout diverged from that policy could format an ellipsis or wrap boundary using
    /// a cell width the canvas would then measure differently, which is unsound.
    /// </remarks>
    public Ambiguous AmbiguousWidth => CellPolicy.AmbiguousWidth;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached text control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The text control is disposed.</exception>
    public TextStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public TextStyle ActualStyle => _style.Actual;

    /// <summary>
    /// Gets this control's private, in-place-mutated render/measurement line cache.
    /// </summary>
    /// <remarks>
    /// Not a stable content property: it can be observed empty before the first layout pass, stale
    /// after <see cref="Content"/> changes until the next layout, and the returned memory can later
    /// mutate in place or be silently orphaned by a subsequent reformat. Callers that need formatted
    /// lines for arbitrary text with an unambiguous width, lifetime, and ownership story should use
    /// <see cref="TextLayout.Format"/> directly instead of inspecting this cache.
    /// </remarks>
    internal ReadOnlyMemory<Line> Lines => _lines.AsMemory(0, _lineCount);

    /// <inheritdoc/>
    public override SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();
        EnsureParsed();

        if (!EffectiveIsVisible)
        {
            return new SelectableTextSnapshot(_display, [], isAuthoritative: true);
        }

        var bounds = ContentBounds.Intersect(Bounds);
        var clip = bounds.Intersect(SelectableTextAggregation.GetEffectiveClip(this));
        EnsureLayout(bounds.Width);
        var glyphs = new List<SelectableTextGlyph>();
        var lines = Lines.Span;

        for (var row = 0; row < lines.Length && row < bounds.Height; row++)
        {
            var line = lines[row];
            var x = 0;

            foreach (var grapheme in Graphemes.Enumerate(_display.AsSpan(line.Offset, line.Length)))
            {
                var offset = line.Offset + grapheme.Offset;
                var cluster = _display.AsSpan(offset, grapheme.Length);
                var width = cluster.Length == 1 && cluster[0] == '\t'
                    ? TextLayout.TabSize - (x % TextLayout.TabSize)
                    : UnicodeWidth.Measure(cluster, AmbiguousWidth).Cells;
                var absolute = new Rect(
                    bounds.X.Add(line.Leading).Add(x),
                    bounds.Y.Add(row),
                    width,
                    1);

                if (width > 0 && SelectableTextAggregation.ContainsCompleteGlyph(clip, absolute))
                {
                    glyphs.Add(new SelectableTextGlyph(
                        new Selection(offset, offset + grapheme.Length),
                        new Rect(
                            absolute.X - Bounds.X,
                            absolute.Y - Bounds.Y,
                            absolute.Width,
                            absolute.Height)));
                }

                x += width;
            }
        }

        return new SelectableTextSnapshot(_display, glyphs, isAuthoritative: true);
    }

    /// <summary>Escapes dynamic visible text for safe interpolation into markup content.</summary>
    /// <param name="value">The non-null visible text.</param>
    /// <returns>The text with opening-angle and backslash metacharacters escaped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    [Pure]
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Escape();
    }

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
        var isCaption = Parent is IAccessKeyCaptionOwner owner && owner.OwnsAccessKeyCaption(this);
        var captionOwner = isCaption ? Parent : null;
        var useMnemonic = captionOwner?.UseMnemonic ?? UseMnemonic;
        var highlightMnemonic = captionOwner?.EffectiveIsEnabled ?? EffectiveIsEnabled;
        var hasHighlightedMnemonic =
            useMnemonic &&
            highlightMnemonic &&
            Content.AsSpan().TryGetKey(out _);
        Color? hotkeyColor = hasHighlightedMnemonic
            ? ResolveThemeValue(_hotkeyThemeDependency)
            : null;

        if (!hasHighlightedMnemonic)
        {
            SetThemeValueDependency(_hotkeyThemeDependency, active: false);
        }

        if (ReferenceEquals(_parsedContent, Content) &&
            _parsedUseMnemonic == useMnemonic &&
            _parsedHighlightMnemonic == highlightMnemonic &&
            _parsedHotkeyColor == hotkeyColor)
        {
            return;
        }

        _spans = Content.ToMarkup(useMnemonic, highlightMnemonic, hotkeyColor).Parse(out _display);
        _parsedContent = Content;
        _parsedUseMnemonic = useMnemonic;
        _parsedHighlightMnemonic = highlightMnemonic;
        _parsedHotkeyColor = hotkeyColor;
        _layoutValid = false;
    }

    /// <inheritdoc/>
    protected override string? AccessKeyText => Content;

    /// <summary>Gets or sets the control this label's access key focuses directly.</summary>
    /// <remarks>
    /// When unset, a standalone label-like <see cref="Text"/> (not owned as an <see cref="InputBase"/>
    /// caption) falls back to moving focus to the next tab stop, per the documented default. When
    /// set, the access key focuses this target instead — independent of tree position, ownership,
    /// or intervening tab stops. The target is validated at dispatch time: it must belong to the
    /// same focus tree and be an eligible focus target in the active modal plane, or the access key
    /// is declined rather than falling back to tab-stop traversal.
    /// </remarks>
    public ControlBase? AccessKeyTarget { get; set; }

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        _ = key;

        return AccessKeyTarget is { } target
            ? FocusOwner is { } focus &&
              target.FocusOwner is not null &&
              ReferenceEquals(target.FocusOwner, focus) &&
              focus.Focus(target, FocusReason.Keyboard, cancellable: true)
            : FocusAccessKeyTarget();
    }

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
            // The text fits in both the previous and new widths without truncation: lines are
            // identical, only alignment within the available width may change. This equivalence
            // only holds for an overflow policy whose FormatUnwrapped path always emits exactly
            // one line per paragraph no matter the width - Overflow.Wrap and WrapAnywhere can
            // split one paragraph into several lines, and _measuredMaxCells only ever records the
            // longest INDIVIDUAL already-wrapped line, not the paragraph's true unwrapped extent.
            // A widened arrange slot that still exceeds every existing line's own cell count would
            // then wrongly compare as "already fits" and skip the reformat that should have
            // merged those wrapped lines back together.
            //
            // Only Overflow.Visible guarantees a line's Cells never depends on width (FormatUnwrapped
            // always emits the full completeCells for Visible), so only Visible can safely skip
            // reformat on widen. Clip and Ellipsis routinely leave cells < width after truncation
            // (word-boundary snap-back, or a wide grapheme that doesn't fit the last cell), so reusing
            // stale line data there can leave truncated text stuck forever.
            if (Overflow == Overflow.Visible &&
                width >= _measuredMaxCells && _cachedWidth > _measuredMaxCells)
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
        var origin = new Point(bounds.X.Add(line.Leading), bounds.Y.Add(row));
        var cells = 0;
        var spanIndex = SpanIndexAt(line.Offset);
        var runOffset = line.Offset;
        var runLength = 0;
        var runSpanIndex = spanIndex;

        // Markup.Parse's spans tile the visible text in source order, so every grapheme in one
        // run shares the identical resolved style and background — batching them into a single
        // Canvas.Draw call per run (instead of one per grapheme cluster) cannot change the
        // rendered output, only how many times the cluster-analysis loop inside Draw runs.
        // DrawResult.Final carries the exact advance Canvas already computed while
        // writing, so the run doesn't need a second, redundant Width.Measure pass.
        void FlushRun()
        {
            if (runLength == 0)
            {
                return;
            }

            var span = runSpanIndex >= 0 ? _spans[runSpanIndex] : default;
            var position = new Point(origin.X.Add(cells), origin.Y);
            var result = canvas.Draw(
                _display.AsSpan(runOffset, runLength),
                position,
                ResolveSpanStyle(span),
                background: ResolveBackgroundMode(span));
            cells += result.Final.X - position.X;
        }

        foreach (var grapheme in Graphemes.Enumerate(_display.AsSpan(line.Offset, line.Length)))
        {
            var offset = line.Offset + grapheme.Offset;

            // A tab cannot join a style run: Canvas.Draw resolves control-cluster advance
            // relative to the run's own origin, not the line's, so a tab inside a run lands at
            // the wrong stop. Flush, advance the line counter to the next
            // line-relative four-cell stop directly (mirroring Layout.cs), and start a fresh
            // run after it.
            if (grapheme.Length == 1 && _display[offset] == '\t')
            {
                FlushRun();
                cells += TextLayout.TabSize - (cells % TextLayout.TabSize);
                spanIndex = AdvanceSpan(spanIndex, offset);
                runOffset = offset + grapheme.Length;
                runSpanIndex = spanIndex;
                runLength = 0;
                continue;
            }

            var nextSpanIndex = AdvanceSpan(spanIndex, offset);

            if (nextSpanIndex != runSpanIndex)
            {
                FlushRun();
                runOffset = offset;
                runSpanIndex = nextSpanIndex;
            }

            runLength = offset + grapheme.Length - runOffset;
            spanIndex = nextSpanIndex;
        }

        FlushRun();

        if (line.HasEllipsis)
        {
            var span = spanIndex >= 0 ? _spans[spanIndex] : default;
            var themed = ControlGlyphs.Text.Ellipsis;
            canvas.DrawRune(
                ActualStyle.EllipsisGlyph.Resolve(themed.Fallback, AmbiguousWidth),
                new Point(bounds.X.Add(line.Leading).Add(cells), bounds.Y.Add(row)),
                ResolveSpanStyle(span),
                ResolveBackgroundMode(span));
        }
    }

    [Pure]
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

    [Pure]
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

    [Pure]
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

    [Pure]
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
