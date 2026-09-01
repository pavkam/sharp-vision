// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Text.Encodings.Web;
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

    // Object-key labels are serialized back to a JSON string literal for display, matching the
    // control's other punctuation. The default encoder escapes every non-ASCII scalar as \uXXXX,
    // which would render a key like "café" or "名前" unreadable even though the sibling string
    // VALUE for the same property renders those exact characters verbatim via GetRawText(). This
    // still escapes the characters a JSON string literal requires escaped (quote, backslash,
    // control characters) - only the blanket non-ASCII escaping is relaxed.
    private static readonly JsonSerializerOptions LabelSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private JsonViewNode _root;
    private List<JsonViewNode> _visibleNodes = [];
    private List<JsonViewLine> _sourceLines = [];
    private List<JsonViewLine> _lines = [];
    private readonly JsonViewContent _content;
    private readonly LayoutStack _stack;
    private readonly RetainedScrollPart _scrollPart;
    private readonly WidthDependentViewportCoordinator _projectionCoordinator;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private readonly StyleSlot<JsonViewStyle> _style;
    private JsonViewNode? _selectedNode;
    private int? _projectionWidth;
    private ulong _projectionVersion;
    private (Rune Collapsed, Rune IsExpanded)? _builtWithGlyphs;

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
        InitializeContent(_stack);
        _projectionCoordinator = new WidthDependentViewportCoordinator(
            this,
            _stack,
            _content,
            static () => true,
            () => _projectionWidth,
            Reproject);
        _scrollPart = RegisterRetainedScrollPart(_stack, forwardsScrollEvent: false);
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
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            var root = Parse(value);
            var visibleNodes = CollectVisibleNodes(root);

            var previousPath = SelectedPath;
            field = value;
            _root = root;
            _visibleNodes = visibleNodes;
            _sourceLines = BuildLines(root, Indent);
            _lines = _sourceLines;
            _projectionWidth = null;
            _projectionVersion++;
            _stack.HorizontalOffset = 0;
            _stack.VerticalOffset = 0;
            CommitSelection(previousPath, visibleNodes.FirstOrDefault());
            NotifyPropertyChanged(nameof(Json), InvalidationImpact.Measure);
            InvalidateRetainedDescendant(_content, InvalidationImpact.Measure);
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
                    _projectionVersion++;
                    InvalidateRetainedDescendant(_content, InvalidationImpact.Measure);
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
        get => _scrollPart.ScrollBars;
        set => _scrollPart.ScrollBars = value;
    }

    /// <summary>Gets or sets when generated scrollbars are visible.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => _scrollPart.ShowScrollBars;
        set => _scrollPart.ShowScrollBars = value;
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
    /// more than one internal arrange to settle the wrapped projection's width, the shared
    /// coordinator coalesces every intermediate change that occurs
    /// while it settles into the single event actually raised, so a subscriber only ever observes
    /// the final settled offset, extent, and viewport for one layout pass, never a transient value
    /// clamped against a since-superseded wrap. An offset change from any other cause - scrolling,
    /// programmatic <see cref="ScrollBy"/>, a resize that does not need reconciling - is forwarded
    /// exactly as it occurs, individually.
    /// </remarks>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged
    {
        add => _projectionCoordinator.ScrollChanged += value;
        remove => _projectionCoordinator.ScrollChanged -= value;
    }

    /// <summary>Gets the committed content extent.</summary>
    public Size Extent => _scrollPart.Extent;

    /// <summary>Gets the committed visible viewport extent.</summary>
    public Size Viewport => _scrollPart.Viewport;

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int HorizontalOffset
    {
        get => _scrollPart.HorizontalOffset;
        set => _scrollPart.HorizontalOffset = value;
    }

    /// <summary>Gets or sets the valid vertical content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int VerticalOffset
    {
        get => _scrollPart.VerticalOffset;
        set => _scrollPart.VerticalOffset = value;
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
        get => _scrollPart.LineSize;
        set => _scrollPart.LineSize = value;
    }

    /// <summary>Gets or sets the non-negative cells of context retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int PageOverlap
    {
        get => _scrollPart.PageOverlap;
        set => _scrollPart.PageOverlap = value;
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
    /// <see cref="ScrollBars.Both"/> configuration; arrange-time reconciliation is what
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
        // Stashed for the coordinator, which needs to remeasure the composed viewport
        // with the exact same constraint this control itself received - not a constraint it could
        // reconstruct from Bounds, since reconciliation runs inside this control's own
        // ArrangeOverride, before any later Measure call would refresh it.
        _projectionCoordinator.CaptureMeasureConstraint(constraint);
        return base.MeasureOverride(constraint);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        _projectionCoordinator.Arrange(bounds, () => base.ArrangeOverride(bounds));

    /// <summary>Rebuilds the JSON projection for the coordinator's positive settled width.</summary>
    /// <param name="width">The positive scrollbar-aware viewport width in cells.</param>
    private void Reproject(int width)
    {
        Debug.Assert(width > 0, "Projection reconciliation requires a positive viewport width.");
        _projectionWidth = width;
        _lines = BuildDisplayLines(_sourceLines, width);
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
            InvalidateRetainedDescendant(_content, InvalidationImpact.Measure);
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

        var projectionVersion = _projectionVersion;
        CommitSelection(SelectedPath, node);

        if (!IsInputContinuationCurrent(node, projectionVersion))
        {
            eventArgs.IsHandled = true;
            return;
        }

        _ = Focus();

        if (onDisclosure && IsInputContinuationCurrent(node, projectionVersion))
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
                    JsonSerializer.Serialize(property.Name, LabelSerializerOptions),
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
        _projectionVersion++;
        NormalizeSelection();
        InvalidateRetainedDescendant(_content, InvalidationImpact.Measure);
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
        InvalidateRetainedDescendant(_content, InvalidationImpact.Render);
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

        if (code is Code.Up or Code.Down or Code.PageUp or Code.PageDown or
            Code.Home or Code.End or Code.Left or Code.Right &&
            !KeyboardModifierPolicy.IsScalarNavigationEligible(eventArgs.Stroke.Modifiers))
        {
            return;
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
        var node = _visibleNodes[target];
        var projectionVersion = _projectionVersion;
        CommitSelection(SelectedPath, node);

        if (IsInputContinuationCurrent(node, projectionVersion))
        {
            RevealSelection();
        }

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

        var projectionVersion = _projectionVersion;
        CommitSelection(SelectedPath, node);

        if (IsInputContinuationCurrent(node, projectionVersion))
        {
            RevealSelection();
        }

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

        var node = first ? _visibleNodes[0] : _visibleNodes[^1];
        var projectionVersion = _projectionVersion;
        CommitSelection(SelectedPath, node);

        if (IsInputContinuationCurrent(node, projectionVersion))
        {
            RevealSelection();
        }

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

        var projectionVersion = _projectionVersion;
        CommitSelection(SelectedPath, parent);

        if (IsInputContinuationCurrent(parent, projectionVersion))
        {
            RevealSelection();
        }

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

        var child = selected.Children[0];
        var projectionVersion = _projectionVersion;
        CommitSelection(SelectedPath, child);

        if (IsInputContinuationCurrent(child, projectionVersion))
        {
            RevealSelection();
        }

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

    [Pure]
    private bool IsInputContinuationCurrent(JsonViewNode node, ulong projectionVersion) =>
        !IsDisposed &&
        _projectionVersion == projectionVersion &&
        ReferenceEquals(_selectedNode, node) &&
        _visibleNodes.Contains(node);

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
        expanded
            ? ActualStyle.ExpandedGlyph.Resolve(ControlGlyphs.Disclosure.Expanded.Fallback, CellPolicy.AmbiguousWidth)
            : ActualStyle.CollapsedGlyph.Resolve(ControlGlyphs.Disclosure.Collapsed.Fallback, CellPolicy.AmbiguousWidth);

}
