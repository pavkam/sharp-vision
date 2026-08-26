// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Text.Json;

using Scrolling;

using SharpVision.Terminal.Input;

using LayoutStack = Layout.Stack;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using TextAlignment = Text.Alignment;
using TextLayout = Text.Layout;
using TextLine = Text.Line;
using TextOverflow = Text.Overflow;

/// <summary>Displays JSON as a focusable hierarchical collection of properties and array entries.</summary>
[PublicAPI]
public sealed class JsonView: CompositeControlBase, IStyled<JsonViewStyle>
{
    /// <summary>Gets the largest caller-configurable indentation step retained by the projection.</summary>
    internal const int MaximumIndent = 4096;

    /// <summary>Gets the maximum indentation prefix materialized for any projected line.</summary>
    internal const int MaximumProjectedIndentationCells = 4096;

    private JsonViewNode _root;
    private List<JsonViewNode> _visibleNodes = [];
    private List<JsonViewLine> _sourceLines = [];
    private List<JsonViewLine> _lines = [];
    private readonly JsonViewContent _content;
    private readonly LayoutStack _stack;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private readonly StyleSlot<JsonViewStyle> _style;
    private JsonViewNode? _selectedNode;
    private int? _projectionWidth;
    private (Rune Collapsed, Rune IsExpanded)? _builtWithGlyphs;
    private Constraint _lastMeasureConstraint;
    private bool _suppressScrollChangedPassthrough;
    private ScrollChangedEventArgs? _pendingScrollChanged;

    /// <summary>Initializes an empty JSON view whose document is the JSON null value.</summary>
    public JsonView()
    {
        _style = InitializeStyle(JsonViewStyle.Definition, OnStyleChanged);
        _root = Parse("null");
        _sourceLines = BuildLines(_root, Indent);
        _lines = _sourceLines;
        _content = new JsonViewContent(this);
        _stack = new LayoutStack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Children = { _content }
        };
        _stack.ScrollChanged += OnStackScrollChanged;
        InitializeContent(_stack);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _stack, nameof(ScrollBarStyle));
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
        _ = AddHandler(Events.Key, OnKeyRouted);
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The view is disposed.</exception>
    public JsonViewStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public JsonViewStyle ActualStyle => _style.Actual;

    /// <summary>Gets or sets the complete non-null JSON document text.</summary>
    /// <remarks>The replacement is parsed completely before any observable state changes.</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="JsonException">
    /// The value is not one complete valid JSON document, or a JSON object in it has duplicate keys.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string Json
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (field == value)
            {
                return;
            }

            var root = Parse(value);
            var visibleNodes = CollectVisibleNodes(root);

            VerifyMutable();
            var previousPath = SelectedPath;
            field = value;
            _root = root;
            _visibleNodes = visibleNodes;
            _sourceLines = BuildLines(root, Indent);
            _lines = _sourceLines;
            _projectionWidth = null;
            _stack.HorizontalOffset = 0;
            _stack.VerticalOffset = 0;
            CommitSelection(previousPath, visibleNodes.FirstOrDefault());
            NotifyPropertyChanged(nameof(Json), InvalidationImpact.Measure);
            _content.Invalidate(Invalidation.Measure);
        }
    } = "null";

    /// <summary>Gets or sets the number of terminal cells reserved for each nesting level.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or exceeds 4096.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int Indent
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumIndent);

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () =>
                {
                    _sourceLines = BuildLines(_root, Indent);
                    _lines = _sourceLines;
                    _projectionWidth = null;
                    _content.Invalidate(Invalidation.Measure);
                });
        }
    } = 2;

    /// <summary>Gets the RFC 6901 JSON Pointer of the selected property or array entry.</summary>
    public string? SelectedPath { get; private set; }

    /// <summary>Raised after the selected property or array-entry pointer changes.</summary>
    public event EventHandler<JsonViewSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets or sets which overflow axes provide generated scrollbars.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown flags.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get => _stack.ScrollBars;
        set
        {
            VerifyMutable();

            if (_stack.ScrollBars == value)
            {
                return;
            }

            _stack.ScrollBars = value;
            NotifyPropertyChanged(nameof(ScrollBars), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets when generated scrollbars are visible.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => _stack.ShowScrollBars;
        set
        {
            VerifyMutable();

            if (_stack.ShowScrollBars == value)
            {
                return;
            }

            _stack.ShowScrollBars = value;
            NotifyPropertyChanged(nameof(ShowScrollBars), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the complete local style for generated scrollbars.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _scrollBarStyle.Local;
        set => _scrollBarStyle.Local = value;
    }

    /// <summary>Gets the resolved style applied to generated scrollbars.</summary>
    public ScrollBarStyle ActualScrollBarStyle => _scrollBarStyle.Actual;

    /// <summary>Raised after a generated scrolling viewport commits changed offsets.</summary>
    /// <remarks>
    /// This is not a direct forward of the composed viewport's own event: a layout pass that needs
    /// more than one internal arrange to settle the wrapped projection's width -
    /// <see cref="ReconcileProjectionWidth"/> - coalesces every intermediate change that occurs
    /// while it settles into the single event actually raised, so a subscriber only ever observes
    /// the final settled offset, extent, and viewport for one layout pass, never a transient value
    /// clamped against a since-superseded wrap. An offset change from any other cause - scrolling,
    /// programmatic <see cref="ScrollBy"/>, a resize that does not need reconciling - is forwarded
    /// exactly as it occurs, individually.
    /// </remarks>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    /// <summary>Gets the committed content extent.</summary>
    public Size Extent => _stack.Extent;

    /// <summary>Gets the committed visible viewport extent.</summary>
    public Size Viewport => _stack.Viewport;

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int HorizontalOffset
    {
        get => _stack.HorizontalOffset;
        set => _stack.HorizontalOffset = value;
    }

    /// <summary>Gets or sets the valid vertical content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int VerticalOffset
    {
        get => _stack.VerticalOffset;
        set => _stack.VerticalOffset = value;
    }

    /// <summary>Gets or sets the non-negative wheel-scroll increment in cells.</summary>
    /// <remarks>
    /// Keyboard navigation always moves the selection by exactly one line regardless of this
    /// value - only the mouse wheel consults it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int LineSize
    {
        get => _stack.LineSize;
        set
        {
            VerifyMutable();

            if (_stack.LineSize == value)
            {
                return;
            }

            _stack.LineSize = value;
            NotifyPropertyChanged(nameof(LineSize), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the non-negative cells of context retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int PageOverlap
    {
        get => _stack.PageOverlap;
        set
        {
            VerifyMutable();

            if (_stack.PageOverlap == value)
            {
                return;
            }

            _stack.PageOverlap = value;
            NotifyPropertyChanged(nameof(PageOverlap), InvalidationImpact.None);
        }
    }

    /// <summary>Scrolls by signed cell deltas with saturation and endpoint clamping.</summary>
    /// <param name="x">The requested horizontal delta.</param>
    /// <param name="y">The requested vertical delta.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when at least one offset changes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ScrollBy(int x, int y, ScrollCause cause = ScrollCause.Programmatic) =>
        _stack.ScrollBy(x, y, cause);

    /// <summary>Sets the disclosure state of one container entry.</summary>
    /// <param name="path">The non-null RFC 6901 pointer of a non-root object or array entry.</param>
    /// <param name="expanded">Whether descendants should be visible.</param>
    /// <returns>True when the disclosure state changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">The path does not identify a container entry.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool SetExpanded(string path, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(path);
        var node = FindNode(path);

        if (node is not { IsContainer: true, Parent: not null })
        {
            throw new ArgumentException("The path must identify an object or array entry.", nameof(path));
        }

        VerifyMutable();

        if (node.IsExpanded == expanded)
        {
            return false;
        }

        node.IsExpanded = expanded;
        RebuildProjection();
        return true;
    }

    /// <summary>Expands every object and array entry.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ExpandAll() => SetAllExpanded(true);

    /// <summary>Collapses every non-root object and array entry.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void CollapseAll() => SetAllExpanded(false);

    /// <summary>Gets the visible selectable-entry count used to prove disclosure projection invariants.</summary>
    [NonNegativeValue]
    internal int VisibleEntryCount => _visibleNodes.Count;

    /// <summary>Measures the current projected document for the private content surface.</summary>
    /// <returns>The width-aware visual extent in terminal cells.</returns>
    internal Size MeasureProjectedContent()
    {
        var width = 0;

        foreach (var line in _lines)
        {
            width = Math.Max(width, MeasureCells(line.Text));
        }

        return new Size(width, _lines.Count);
    }

    /// <summary>Rewraps the projected document against a measure-time width constraint, then
    /// measures the result.</summary>
    /// <remarks>
    /// Rebuilding is skipped when the requested width matches the cached projection width, since
    /// a width-constrained host's measure probe runs twice per settled width and rewrapping on
    /// both would be a redundant pass over every line. A null width leaves the current projection
    /// untouched instead of falling back to the unwrapped source lines: the composed viewport
    /// always measures its content unbounded on the horizontal axis (an axis it can scroll is
    /// measured unbounded so it can report its natural extent - see <see cref="Container"/>'s
    /// width-nulling for a scrollable horizontal axis), so a null width here is the routine
    /// per-child probe of that mechanism, not a signal that this control's own host is genuinely
    /// unconstrained. Discarding the projection on every one of those probes would make word-wrap
    /// permanently unreachable for the default
    /// <see cref="ScrollBars.Both"/> configuration; <see cref="ReconcileProjectionWidth"/> is what
    /// actually keeps the projection matched to the real, scrollbar-reservation-aware width in
    /// that configuration.
    /// </remarks>
    /// <param name="width">The available width in cells, or null when unconstrained.</param>
    /// <returns>The width-aware visual extent in terminal cells.</returns>
    internal Size MeasureAndWrap(int? width)
    {
        if (width is { } bounded && bounded > 0 && _projectionWidth != bounded)
        {
            _projectionWidth = bounded;
            _lines = BuildDisplayLines(_sourceLines, bounded);
        }

        return MeasureProjectedContent();
    }

    /// <summary>Draws projected lines intersecting the private content surface clip.</summary>
    /// <param name="canvas">The clipped semantic canvas.</param>
    /// <param name="bounds">The content surface bounds.</param>
    internal void RenderProjectedContent(TerminalCanvas canvas, Rect bounds)
    {
        var first = Math.Max(0, canvas.Bounds.Y - bounds.Y);
        var last = Math.Min(_lines.Count, canvas.Bounds.Bottom - bounds.Y);
        var actualStyle = ActualStyle;
        var punctuation = ResolvedStyle.WithForeground(ResolveColor(actualStyle.PunctuationColor, Theme));
        var disclosure = ResolvedStyle.WithForeground(ResolveColor(actualStyle.DisclosureColor, Theme));

        for (var index = first; index < last; index++)
        {
            var line = _lines[index];
            var x = bounds.X;
            var y = bounds.Y + index;

            DrawToken(canvas, line.Leading, line.Node is null ? punctuation : disclosure, ref x, y);

            if (line.Node is { } node && line.Label.Length > 0)
            {
                var selected = ReferenceEquals(node, _selectedNode);
                var labelStyle = selected
                    ? WithColors(
                        ResolvedStyle,
                        ResolveColor(actualStyle.SelectedTextColor, Theme),
                        ResolveColor(actualStyle.SelectedBackground, Theme))
                    : ResolvedStyle.WithForeground(ResolveColor(
                        node.IsArrayElement ? actualStyle.IndexColor : actualStyle.KeyColor,
                        Theme));
                DrawToken(
                    canvas,
                    line.Label,
                    labelStyle,
                    ref x,
                    y,
                    selected ? BackgroundMode.Opaque : BackgroundMode.Transparent);
            }

            DrawToken(canvas, line.Separator, punctuation, ref x, y);
            var valueStyle = line.ValueKind is { } valueKind
                ? ResolveValueStyle(valueKind, actualStyle, punctuation)
                : punctuation;
            DrawToken(canvas, line.Value, valueStyle, ref x, y);
            DrawToken(canvas, line.Suffix, punctuation, ref x, y);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        // Stashed for ReconcileProjectionWidth, which needs to remeasure the composed viewport
        // with the exact same constraint this control itself received - not a constraint it could
        // reconstruct from Bounds, since ReconcileProjectionWidth runs inside this control's own
        // ArrangeOverride, before any later Measure call would refresh it.
        _lastMeasureConstraint = constraint;
        return base.MeasureOverride(constraint);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        // Every _stack.Arrange this method causes - the one base.ArrangeOverride below performs,
        // and any additional one ReconcileProjectionWidth performs while settling the projection
        // width - can independently fire the composed viewport's own ScrollChanged with a
        // still-transitional Extent/Viewport. Suppressing the passthrough for the whole call and
        // replaying at most one coalesced event afterward is what keeps a subscriber from ever
        // observing one of those transitional values.
        _suppressScrollChangedPassthrough = true;
        ScrollChangedEventArgs? pending;

        try
        {
            base.ArrangeOverride(bounds);
            ReconcileProjectionWidth(bounds);
        }
        finally
        {
            // Captured and cleared here, inside the finally, rather than after the try/finally:
            // an exception from either call above must not strand a pending event whose stale
            // PreviousOffset would otherwise seed the next transaction's own merge.
            _suppressScrollChangedPassthrough = false;
            pending = _pendingScrollChanged;
            _pendingScrollChanged = null;
        }

        if (pending is { } settled && settled.PreviousOffset != settled.Offset)
        {
            ScrollChanged?.Invoke(this, settled);
        }
    }

    /// <summary>Forwards the composed viewport's own scroll transitions, coalescing every one that
    /// occurs while <see cref="ArrangeOverride"/> is settling the wrapped projection's width into
    /// the single event that override replays once settled.</summary>
    /// <param name="sender">The composed viewport; unused.</param>
    /// <param name="e">The composed viewport's own committed transition.</param>
    private void OnStackScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        _ = sender;

        if (!_suppressScrollChangedPassthrough)
        {
            ScrollChanged?.Invoke(this, e);
            return;
        }

        // Keep the earliest PreviousOffset seen this transaction - the offset before any of this
        // arrange's internal reconcile passes ran - paired with the latest Offset/Extent/Viewport,
        // so the eventually-replayed event reports the one meaningful transition: from where the
        // subscriber last saw this control settle, to where it settles now.
        _pendingScrollChanged = new ScrollChangedEventArgs(
            _pendingScrollChanged?.PreviousOffset ?? e.PreviousOffset,
            e.Offset,
            e.Extent,
            e.Viewport,
            e.Cause);
    }

    /// <summary>Keeps the wrapped projection matched to the composed viewport's real,
    /// scrollbar-reservation-aware width, entirely within the current layout transaction.</summary>
    /// <remarks>
    /// The composed viewport measures its content unbounded on the horizontal axis (see
    /// <see cref="MeasureAndWrap"/>'s remarks), so the only place the real width - after a vertical
    /// scrollbar has claimed its column - becomes known is here, once the arrange this override
    /// delegated to has resolved it. Rewrapping can itself change the content's height enough to
    /// flip whether a vertical scrollbar is needed at all, exactly the coupling
    /// <see cref="Container"/>'s own two-probe scrollbar resolution exists to settle for a
    /// non-scrolling-width axis; since that mechanism is a no-op for a horizontally-scrollable one,
    /// this reproduces it by hand for as many rounds as it takes to settle, bounded defensively
    /// against runaway growth. Remeasuring and rearranging the composed viewport synchronously -
    /// instead of only invalidating it for a future pass - is what keeps <see cref="Extent"/> and
    /// <see cref="Viewport"/> accurate after a single layout pass instead of one frame behind it.
    /// Every intermediate <see cref="ScrollChanged"/> raised by an internal arrange in this loop is
    /// coalesced rather than passed through - see <see cref="OnStackScrollChanged"/> - so a
    /// subscriber never observes an offset clamped against a since-superseded wrap.
    /// </remarks>
    /// <param name="bounds">The content-box bounds this control's own arrange resolved.</param>
    private void ReconcileProjectionWidth(Rect bounds)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var viewportWidth = _stack.Viewport.Width;

            if (viewportWidth <= 0 || _projectionWidth == viewportWidth)
            {
                return;
            }

            _projectionWidth = viewportWidth;
            _lines = BuildDisplayLines(_sourceLines, viewportWidth);

            _stack.InvalidateSelf(Invalidation.Measure);
            _content.InvalidateSelf(Invalidation.Measure);
            _stack.Measure(_lastMeasureConstraint);
            _stack.Arrange(bounds, widthResolved: true, heightResolved: true);

            // The composed viewport's own ScrollChanged only fires when this arrange's offset
            // clamp actually moved, but Extent/Viewport can still change without one - an earlier
            // iteration's fire must not leave a later, unclamped iteration's fresher geometry
            // behind. Unconditionally re-stamping the pending event's Extent/Viewport after every
            // arrange this loop performs is what keeps the eventually-replayed event's geometry
            // always the final settled one, not whichever iteration happened to reclamp last.
            if (_pendingScrollChanged is { } pending)
            {
                _pendingScrollChanged = new ScrollChangedEventArgs(
                    pending.PreviousOffset,
                    pending.Offset,
                    _stack.Extent,
                    _stack.Viewport,
                    pending.Cause);
            }
        }
    }

    private void OnStyleChanged(JsonViewStyle previous, JsonViewStyle current)
    {
        _ = previous;
        _ = current;
        _content?.Invalidate(Invalidation.Render);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        // The disclosure arrow is part of the measured line text, so a style that changes it has to
        // rebuild the lines rather than just repaint them. Checked here rather than in
        // OnStyleChanged because a theme swap reaches the resolved style without routing through
        // it, and this is already the place the projection is revalidated against current state.
        var glyphs = (ActualStyle.CollapsedGlyph, ActualStyle.ExpandedGlyph);

        if (_builtWithGlyphs != glyphs)
        {
            _builtWithGlyphs = glyphs;
            _sourceLines = BuildLines(_root, Indent);

            // The rebuilt source lines need rewrapping against the current width at measure time;
            // forcing a mismatch here and invalidating Measure makes the next measure pass pick it
            // up.
            _projectionWidth = null;
            _content.Invalidate(Invalidation.Measure);
        }

        if (Bounds.Width > 0 && Bounds.Height > 0 && this.HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(Bounds, ResolvedStyle);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        base.OnEvent(eventArgs);

        if (eventArgs.IsHandled || eventArgs is not PointerEventArgs
            {
                Pointer.Action: PointerAction.Press,
                Pointer.Buttons: var buttons,
                Pointer.Cells: { } cells
            } || (buttons & Buttons.Primary) == 0 || !_content.Bounds.Contains(cells))
        {
            return;
        }

        var lineIndex = cells.Y - _content.Bounds.Y;

        if (lineIndex < 0 || lineIndex >= _lines.Count || _lines[lineIndex].Node is not { } node)
        {
            return;
        }

        var line = _lines[lineIndex];
        var x = cells.X - _content.Bounds.X;
        var leadingWidth = MeasureCells(line.Leading);
        var labelWidth = MeasureCells(line.Label);
        var onLabel = x >= leadingWidth && x < leadingWidth + labelWidth;

        // Leading is "{indentation}{glyph} ": one trailing gap cell after the disclosure glyph,
        // whose own width varies with the active ambiguous-width policy ('▶'/'▼' are East Asian
        // Ambiguous, so they occupy two cells under Ambiguous.Wide). Measuring the actual glyph
        // instead of assuming a fixed two-cell "leadingWidth - 2" literal keeps every cell the
        // glyph occupies clickable, not just its last one.
        var disclosureGlyphWidth = node.IsContainer && node.Children.Count > 0
            ? MeasureCells(DisclosureGlyph(node.IsExpanded).ToString())
            : 0;
        var onDisclosure = disclosureGlyphWidth > 0 &&
                           x >= leadingWidth - 1 - disclosureGlyphWidth &&
                           x < leadingWidth - 1;

        if (!onLabel && !onDisclosure)
        {
            return;
        }

        CommitSelection(SelectedPath, node);
        _ = Focus();

        if (onDisclosure)
        {
            _ = SetExpanded(node.Path, !node.IsExpanded);
        }

        eventArgs.IsHandled = true;
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            SelectionChanged = null;
        }
    }

    [Pure]
    private static JsonViewNode Parse(string value)
    {
        using var document = JsonDocument.Parse(value);
        return BuildNode(document.RootElement, string.Empty, null, null, false);
    }

    [Pure]
    private static JsonViewNode BuildNode(
        JsonElement element,
        string path,
        string? label,
        JsonViewNode? parent,
        bool isArrayElement)
    {
        var rawValue = element.ValueKind switch
        {
            JsonValueKind.Object => "{",
            JsonValueKind.Array => "[",
            JsonValueKind.String => element.GetRawText(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Undefined => throw new JsonException("Undefined is not a JSON value."),
            _ => throw new JsonException("The JSON value kind is unknown.")
        };
        var node = new JsonViewNode(path, label, element.ValueKind, rawValue, parent, isArrayElement);

        if (element.ValueKind == JsonValueKind.Object)
        {
            var seenPropertyNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in element.EnumerateObject())
            {
                if (!seenPropertyNames.Add(property.Name))
                {
                    throw new JsonException($"Duplicate object key '{property.Name}' at '{path}'.");
                }

                var childPath = $"{path}/{EscapePointerSegment(property.Name)}";
                node.Children.Add(BuildNode(
                    property.Value,
                    childPath,
                    JsonSerializer.Serialize(property.Name),
                    node,
                    false));
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;

            foreach (var item in element.EnumerateArray())
            {
                node.Children.Add(BuildNode(item, $"{path}/{index}", $"[{index}]", node, true));
                index++;
            }
        }

        return node;
    }

    [Pure]
    private static string EscapePointerSegment(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    [Pure]
    private static List<JsonViewNode> CollectVisibleNodes(JsonViewNode root)
    {
        var result = new List<JsonViewNode>();
        AppendVisibleChildren(root, result);
        return result;
    }

    private static void AppendVisibleChildren(JsonViewNode node, List<JsonViewNode> result)
    {
        foreach (var child in node.Children)
        {
            result.Add(child);

            if (child.IsExpanded)
            {
                AppendVisibleChildren(child, result);
            }
        }
    }

    [Pure]
    private List<JsonViewLine> BuildLines(JsonViewNode root, int indent)
    {
        var lines = new List<JsonViewLine>();

        if (!root.IsContainer)
        {
            lines.Add(new JsonViewLine(
                string.Empty,
                string.Empty,
                string.Empty,
                root.RawValue,
                string.Empty,
                root.Kind,
                root));
            return lines;
        }

        var open = root.Kind == JsonValueKind.Object ? "{" : "[";
        var close = root.Kind == JsonValueKind.Object ? "}" : "]";

        if (root.Children.Count == 0)
        {
            lines.Add(new JsonViewLine(
                string.Empty,
                string.Empty,
                string.Empty,
                $"{open}{close}",
                string.Empty,
                null,
                null));
            return lines;
        }

        lines.Add(new JsonViewLine(string.Empty, string.Empty, string.Empty, open, string.Empty, null, null));
        AppendChildLines(root, 0, indent, lines);
        lines.Add(new JsonViewLine(string.Empty, string.Empty, string.Empty, close, string.Empty, null, null));
        return lines;
    }

    private void AppendChildLines(
        JsonViewNode parent,
        int depth,
        int indent,
        List<JsonViewLine> lines)
    {
        for (var index = 0; index < parent.Children.Count; index++)
        {
            var node = parent.Children[index];
            var comma = index + 1 < parent.Children.Count ? "," : string.Empty;
            var indentation = BuildIndentation(depth, indent);
            var label = node.Label ?? string.Empty;

            if (!node.IsContainer || node.Children.Count == 0)
            {
                var value = node.IsContainer
                    ? node.Kind == JsonValueKind.Object ? "{}" : "[]"
                    : node.RawValue;
                lines.Add(new JsonViewLine(
                    $"{indentation}  ",
                    label,
                    ": ",
                    value,
                    comma,
                    node.IsContainer ? null : node.Kind,
                    node));
                continue;
            }

            var open = node.Kind == JsonValueKind.Object ? "{" : "[";
            var close = node.Kind == JsonValueKind.Object ? "}" : "]";

            if (!node.IsExpanded)
            {
                lines.Add(new JsonViewLine(
                    $"{indentation}{DisclosureGlyph(expanded: false)} ",
                    label,
                    ": ",
                    $"{open}…{close}",
                    comma,
                    null,
                    node));
                continue;
            }

            lines.Add(new JsonViewLine(
                $"{indentation}{DisclosureGlyph(expanded: true)} ",
                label,
                ": ",
                open,
                string.Empty,
                null,
                node));
            AppendChildLines(node, depth + 1, indent, lines);
            lines.Add(new JsonViewLine(
                BuildIndentation((long) depth + 1, indent),
                string.Empty,
                string.Empty,
                close,
                comma,
                null,
                null));
        }
    }

    [Pure]
    private static string BuildIndentation(long depth, int indent)
    {
        var cells = Math.Min(MaximumProjectedIndentationCells, depth * indent);
        return new string(' ', (int) cells);
    }

    [Pure]
    private List<JsonViewLine> BuildDisplayLines(List<JsonViewLine> source, int width)
    {
        var result = new List<JsonViewLine>(source.Count);

        foreach (var line in source)
        {
            if (line.ValueKind != JsonValueKind.String || MeasureCells(line.Text) <= width)
            {
                result.Add(line);
                continue;
            }

            var prefixWidth = MeasureCells($"{line.Leading}{line.Label}{line.Separator}");
            var suffixWidth = MeasureCells(line.Suffix);
            var valueWidth = Math.Max(1, width - prefixWidth - suffixWidth);
            var lineCount = TextLayout.Format(
                line.Value,
                valueWidth,
                TextOverflow.Wrap,
                TextAlignment.Start,
                CellPolicy.AmbiguousWidth,
                []);
            var wrapped = new TextLine[lineCount];
            _ = TextLayout.Format(
                line.Value,
                valueWidth,
                TextOverflow.Wrap,
                TextAlignment.Start,
                CellPolicy.AmbiguousWidth,
                wrapped);

            for (var index = 0; index < wrapped.Length; index++)
            {
                var segment = wrapped[index];
                var first = index == 0;
                var last = index + 1 == wrapped.Length;
                result.Add(new JsonViewLine(
                    first ? line.Leading : new string(' ', prefixWidth),
                    first ? line.Label : string.Empty,
                    first ? line.Separator : string.Empty,
                    line.Value[segment.Offset..(segment.Offset + segment.Length)],
                    last ? line.Suffix : string.Empty,
                    JsonValueKind.String,
                    first ? line.Node : null));
            }
        }

        return result;
    }

    [Pure]
    private JsonViewNode? FindNode(string path)
    {
        var pending = new Stack<JsonViewNode>();
        pending.Push(_root);

        while (pending.Count > 0)
        {
            var node = pending.Pop();

            if (node.Path == path)
            {
                return node;
            }

            for (var index = node.Children.Count - 1; index >= 0; index--)
            {
                pending.Push(node.Children[index]);
            }
        }

        return null;
    }

    private void RebuildProjection()
    {
        _visibleNodes = CollectVisibleNodes(_root);
        _sourceLines = BuildLines(_root, Indent);
        _lines = _sourceLines;
        _projectionWidth = null;
        NormalizeSelection();
        _content.Invalidate(Invalidation.Measure);
        NotifyPropertyChanged(nameof(VisibleEntryCount), InvalidationImpact.Measure);
    }

    private void SetAllExpanded(bool expanded)
    {
        VerifyMutable();
        var pending = new Stack<JsonViewNode>();
        pending.Push(_root);
        var changed = false;

        while (pending.Count > 0)
        {
            var node = pending.Pop();

            if (!ReferenceEquals(node, _root) && node.IsContainer && node.IsExpanded != expanded)
            {
                node.IsExpanded = expanded;
                changed = true;
            }

            foreach (var child in node.Children)
            {
                pending.Push(child);
            }
        }

        if (changed)
        {
            RebuildProjection();
        }
    }

    private void CommitSelection(string? previousPath, JsonViewNode? node)
    {
        var previousNode = _selectedNode;
        _selectedNode = node;
        SelectedPath = node?.Path;

        if (ReferenceEquals(previousNode, node) && previousPath == SelectedPath)
        {
            return;
        }

        NotifyPropertyChanged(nameof(SelectedPath), InvalidationImpact.Render);
        _content.Invalidate(Invalidation.Render);
        SelectionChanged?.Invoke(this, new JsonViewSelectionChangedEventArgs(previousPath, SelectedPath));
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Bubble || !eventArgs.IsKeyDown)
        {
            return;
        }

        var code = eventArgs.Stroke.Code;

        if (eventArgs.IsInitialKeyDown &&
            code == Code.Character &&
            eventArgs.Stroke.Character == new Rune(' '))
        {
            code = Code.Enter;
        }

        if (code == Code.Up)
        {
            eventArgs.IsHandled = MoveSelection(-1);
        }
        else if (code == Code.Down)
        {
            eventArgs.IsHandled = MoveSelection(1);
        }
        else if (code == Code.PageUp)
        {
            eventArgs.IsHandled = MoveSelectionByPage(-1);
        }
        else if (code == Code.PageDown)
        {
            eventArgs.IsHandled = MoveSelectionByPage(1);
        }
        else if (code == Code.Home)
        {
            eventArgs.IsHandled = SelectEndpoint(first: true);
        }
        else if (code == Code.End)
        {
            eventArgs.IsHandled = SelectEndpoint(first: false);
        }
        else if (code == Code.Left)
        {
            eventArgs.IsHandled = NavigateLeft();
        }
        else if (code == Code.Right)
        {
            eventArgs.IsHandled = NavigateRight();
        }
        else if (eventArgs.IsInitialKeyDown && code == Code.Enter)
        {
            eventArgs.IsHandled = eventArgs.Stroke.Modifiers.IsActivationEligible() && ToggleSelected();
        }
    }

    private bool MoveSelection(int direction)
    {
        if (_visibleNodes.Count == 0)
        {
            return false;
        }

        var index = _selectedNode is null ? -1 : _visibleNodes.IndexOf(_selectedNode);
        var target = Math.Clamp(index + direction, 0, _visibleNodes.Count - 1);
        CommitSelection(SelectedPath, _visibleNodes[target]);
        RevealSelection();
        return true;
    }

    // Pages the selection by lines rather than by visible-node count, so a word-wrapped value's
    // continuation lines count toward the page step the same as any other line. The landing line
    // is found the same way RevealSelection maps a node to its owning line; a page step can land
    // on a continuation or closing-bracket line that owns no node, so the search continues in the
    // paging direction until one is found, falling back to the opposite direction only if the
    // document ends first.
    private bool MoveSelectionByPage(int direction)
    {
        if (_visibleNodes.Count == 0)
        {
            return false;
        }

        var currentLineIndex = _selectedNode is null
            ? -1
            : _lines.FindIndex(line => ReferenceEquals(line.Node, _selectedNode));
        var baseline = currentLineIndex < 0 ? (direction > 0 ? -1 : _lines.Count) : currentLineIndex;
        var step = PagingStep.TargetExtent(_stack.Viewport.Height, PageOverlap);
        var targetLineIndex = Math.Clamp(baseline + (direction * step), 0, _lines.Count - 1);
        var node = FindOwningNode(targetLineIndex, direction) ?? FindOwningNode(targetLineIndex, -direction);

        if (node is null)
        {
            return false;
        }

        CommitSelection(SelectedPath, node);
        RevealSelection();
        return true;
    }

    [Pure]
    private JsonViewNode? FindOwningNode(int lineIndex, int direction)
    {
        var index = SingleSelectionIndex.FindLinear(lineIndex, direction, _lines.Count, i => _lines[i].Node is not null);
        return index < 0 ? null : _lines[index].Node;
    }

    private bool SelectEndpoint(bool first)
    {
        if (_visibleNodes.Count == 0)
        {
            return false;
        }

        CommitSelection(SelectedPath, first ? _visibleNodes[0] : _visibleNodes[^1]);
        RevealSelection();
        return true;
    }

    private bool NavigateLeft()
    {
        if (_selectedNode is null)
        {
            return false;
        }

        if (_selectedNode is { IsContainer: true, IsExpanded: true, Children.Count: > 0 })
        {
            if (!SetExpanded(_selectedNode.Path, false))
            {
                return false;
            }

            // Collapsing removes every projected line below the selection, so a viewport that
            // was scrolled into the removed range would otherwise keep an offset past the
            // selected row - every other navigation path reveals, and so must this one.
            RevealSelection();
            return true;
        }

        var parent = _selectedNode.Parent;

        if (parent is null || ReferenceEquals(parent, _root))
        {
            return false;
        }

        CommitSelection(SelectedPath, parent);
        RevealSelection();
        return true;
    }

    private bool NavigateRight()
    {
        if (_selectedNode is not { IsContainer: true, Children.Count: > 0 } selected)
        {
            return false;
        }

        if (!selected.IsExpanded)
        {
            return SetExpanded(selected.Path, true);
        }

        CommitSelection(SelectedPath, selected.Children[0]);
        RevealSelection();
        return true;
    }

    private bool ToggleSelected()
    {
        if (_selectedNode is not { IsContainer: true, Children.Count: > 0 } selected ||
            !SetExpanded(selected.Path, !selected.IsExpanded))
        {
            return false;
        }

        // Same reveal as NavigateLeft's collapse branch: a collapse can strand the viewport
        // below the selected row.
        RevealSelection();
        return true;
    }

    private void NormalizeSelection()
    {
        if (_selectedNode is null || _visibleNodes.Contains(_selectedNode))
        {
            return;
        }

        var candidate = _selectedNode.Parent;

        while (candidate is not null && !_visibleNodes.Contains(candidate))
        {
            candidate = candidate.Parent;
        }

        CommitSelection(SelectedPath, candidate ?? _visibleNodes.FirstOrDefault());
    }

    private void RevealSelection()
    {
        if (_selectedNode is null || _stack.Viewport.Height == 0)
        {
            return;
        }

        var lineIndex = _lines.FindIndex(line => ReferenceEquals(line.Node, _selectedNode));

        Debug.Assert(lineIndex >= 0, "A visible selected JSON entry must own one projected line.");

        if (lineIndex < 0)
        {
            return;
        }

        var line = _lines[lineIndex];
        var labelStart = MeasureCells(line.Leading);
        var labelWidth = MeasureCells(line.Label);

        if (labelStart < _stack.HorizontalOffset)
        {
            _stack.HorizontalOffset = labelStart;
        }
        else if (labelStart + labelWidth > _stack.HorizontalOffset + _stack.Viewport.Width)
        {
            _stack.HorizontalOffset = Math.Min(
                Math.Max(0, _stack.Extent.Width - _stack.Viewport.Width),
                labelStart + labelWidth - _stack.Viewport.Width);
        }

        if (lineIndex < _stack.VerticalOffset)
        {
            _stack.VerticalOffset = lineIndex;
        }
        else if (lineIndex >= _stack.VerticalOffset + _stack.Viewport.Height)
        {
            _stack.VerticalOffset = lineIndex - _stack.Viewport.Height + 1;
        }
    }

    private void DrawToken(
        TerminalCanvas canvas,
        ReadOnlySpan<char> value,
        TerminalStyle style,
        ref int x,
        int y,
        BackgroundMode background = BackgroundMode.Transparent)
    {
        if (value.Length == 0)
        {
            return;
        }

        _ = canvas.Draw(value, new Point(x, y), style, background: background);
        x += MeasureCells(value);
    }

    [Pure]
    private TerminalStyle ResolveValueStyle(
        JsonValueKind kind,
        JsonViewStyle style,
        TerminalStyle punctuation) => kind switch
        {
            JsonValueKind.String => ResolvedStyle.WithForeground(ResolveColor(style.StringColor, Theme)),
            JsonValueKind.Number => ResolvedStyle.WithForeground(ResolveColor(style.NumberColor, Theme)),
            JsonValueKind.True or JsonValueKind.False => ResolvedStyle.WithForeground(ResolveColor(style.BooleanColor, Theme)),
            JsonValueKind.Null => ResolvedStyle.WithForeground(ResolveColor(style.NullColor, Theme)),
            JsonValueKind.Object or JsonValueKind.Array => punctuation,
            JsonValueKind.Undefined => punctuation,
            _ => throw new NotImplementedException()
        };

    [Pure]
    private static TerminalStyle WithColors(TerminalStyle source, Color foreground, Color background) => new(
        foreground,
        background,
        source.Attributes,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);
    // The single source for all three copies - the two that build the measured line text and the
    // one hit-testing measures to compute the clickable span. They were three literals that had to
    // stay in lockstep by hand, and the glyph participates in layout, so a drift moved the
    // clickable region away from the drawn arrow.
    [Pure]
    private Rune DisclosureGlyph(bool expanded) =>
        expanded ? ActualStyle.ExpandedGlyph : ActualStyle.CollapsedGlyph;

}
