// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.SyntaxHighlighting;

using System.ComponentModel;

using SharpVision.Controls.Scrolling;
using SharpVision.Runtime;
using SharpVision.Scrolling;
using SharpVision.SyntaxHighlighting;
using SharpVision.Terminal.Input;

using LayoutStack = Layout.Stack;
using TextEdit = Edit;
using TextSelection = Selection;

/// <summary>
/// Displays a read-only, syntax-colored, selectable, copyable, and foldable block of source code
/// against a KDE-format <see cref="SyntaxGrammar"/>.
/// </summary>
/// <remarks>
/// <para>
/// This control never mutates <see cref="Code"/>: there is no editing API at all. Selection and
/// copying follow the same "pure <c>CopySelection</c>, host wires the clipboard" contract
/// <c>TextInput</c> and <c>Table</c> use - see <see cref="CopySelection"/>.
/// </para>
/// <para>
/// <see cref="Code"/> is tokenized with its line endings normalized to <c>\n</c>; every offset
/// this control exposes (<see cref="Selection"/>, <see cref="ControlBase.SelectedText"/>) is relative to that
/// normalized text, not to whatever line-ending bytes the assigned string happened to contain.
/// </para>
/// <para>
/// Unlike an editor, source lines are never wrapped: long lines scroll horizontally instead. A
/// tab character measures and draws as exactly one cell; this control does not implement
/// tab-stop expansion.
/// </para>
/// <para>
/// <b>Known limitation:</b> horizontal position bookkeeping for drawing, horizontal scrolling,
/// and pointer hit-testing currently tracks a token's UTF-16 char count as its column, not its
/// measured terminal cell width, even though the committed horizontal <see cref="Extent"/> is
/// already grapheme-and-width aware. A line containing a two-cell-wide grapheme cluster (an East
/// Asian wide character, a wide emoji, or fullwidth punctuation, all plausible inside a string
/// literal, comment, or identifier) can therefore mis-position every subsequent token on that
/// line, slice a cluster in half when the horizontal scroll offset lands inside it, or resolve a
/// click past such a cluster to the wrong character offset. Plain ASCII and narrow-only text are
/// unaffected.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class CodeView:
    CompositeControlBase,
    IStyled<CodeViewStyle>,
    ISelectableTextViewport,
    IClipboardCopySource
{
    private const int _foldGutterWidth = 2;

    private readonly CodeViewContent _content;
    private readonly LayoutStack _stack;
    private readonly StyleSlot<CodeViewStyle> _style;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;

    private string[] _lines = [string.Empty];
    private int[] _lineStartOffsets = [0, 0];
    private SyntaxHighlightResult _result = BuildPlainResult([string.Empty]);
    private List<int> _visibleLines = [0];
    private readonly HashSet<int> _foldedStartLines = [];
    private Dictionary<int, SyntaxFoldRange> _foldStartRanges = [];
    private int _extentWidth;
    private int? _pendingRevealOffset;
    private List<int>? _pendingRevealProjection;
    private bool _pendingRevealPosted;

    /// <summary>Gets how many projected source lines the most recent selectable snapshot inspected.
    /// This test seam proves projection work remains bounded by the clipped viewport.</summary>
    internal int LastSelectableTextSnapshotInspectedLineCount { get; private set; }

    /// <summary>Initializes an empty, unstyled read-only code view.</summary>
    public CodeView()
    {
        _style = InitializeStyle(CodeViewStyle.Definition);
        PropertyChanged += OnOwnPropertyChanged;
        _content = new CodeViewContent(this);
        _stack = new LayoutStack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Children = { _content },
        };
        InitializeContent(_stack);
        _scrollBarStyle = InitializePartStyle(ScrollBarStyle.ForwardingDefinition, nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _stack, nameof(ScrollBarStyle));
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
        ContextMenu = new CodeViewContextMenu(this);
        RebuildProjection();
        IsTextSelectionEnabled = true;
    }

    #region Content

    /// <summary>Gets or sets the complete non-null source text to display.</summary>
    /// <remarks>Line endings are normalized to <c>\n</c> before tokenizing and before any observable state changes.</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string Code
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

            field = value;
            RebuildProjection();
            NotifyPropertyChanged(nameof(Code), InvalidationImpact.None);
        }
    } = string.Empty;

    /// <summary>Gets or sets the catalog <see cref="Language"/> resolves a grammar from. Replacing
    /// this while <see cref="Language"/> is non-null immediately re-resolves and re-tokenizes
    /// against the replacement catalog's own grammar for that language name - the previously
    /// resolved grammar is never reused across a <see cref="Catalog"/> change.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    /// <exception cref="KeyNotFoundException"><see cref="Language"/> is non-null and its exact name is absent from the replacement value.</exception>
    /// <exception cref="InvalidDataException">An embedded resource disagrees with its recorded provenance while resolving <see cref="Language"/> against the replacement value.</exception>
    /// <exception cref="FormatException"><see cref="Language"/>'s definition in the replacement value is not well-formed.</exception>
    public SyntaxDefinitionCatalog Catalog
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            if (EqualityComparer<SyntaxDefinitionCatalog>.Default.Equals(field, value))
            {
                return;
            }

            // Resolved against the replacement value before any observable state changes, so an
            // unknown or malformed Language in the replacement catalog leaves Catalog and every
            // dependent field exactly as they were - the same validate-before-mutate contract
            // Language's own setter keeps.
            var grammar = Language is null ? null : value.GetGrammar(Language);
            _ = SetProperty(ref field, value, InvalidationImpact.None);

            if (Language is not null)
            {
                _grammar = grammar;
                RebuildProjection();
            }
        }
    } = SyntaxDefinitionCatalog.Default;

    /// <summary>Gets or sets the exact case-sensitive <see cref="Catalog"/> language name to
    /// highlight against, or null to display <see cref="Code"/> with no syntax coloring.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="value"/> is not in <see cref="Catalog"/>.</exception>
    /// <exception cref="InvalidDataException">An embedded resource disagrees with its recorded provenance while resolving <paramref name="value"/>.</exception>
    /// <exception cref="FormatException">The <paramref name="value"/> definition is not well-formed.</exception>
    public string? Language
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            var grammar = value is null ? null : Catalog.GetGrammar(value);
            field = value;
            _grammar = grammar;
            RebuildProjection();
            NotifyPropertyChanged(nameof(Language), InvalidationImpact.None);
        }
    }

    private SyntaxGrammar? _grammar;

    #endregion

    #region Styling

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The view is disposed.</exception>
    public CodeViewStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public CodeViewStyle ActualStyle => _style.Actual;

    /// <summary>Gets or sets the complete local scrollbar presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The view is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _scrollBarStyle.Local;
        set => _scrollBarStyle.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned scrollbar presentation.</summary>
    public ScrollBarStyle ActualScrollBarStyle => _scrollBarStyle.Actual;

    /// <summary>
    /// Repaints the render surface whenever <see cref="ActualStyle"/> actually changes - whether
    /// from a local <see cref="Style"/> assignment or purely from an inherited Theme swap.
    /// </summary>
    /// <remarks>
    /// <see cref="_content"/> owns no style slot of its own (unlike, for example, <see cref="_stack"/>'s
    /// bound <see cref="ScrollBarStyle"/>), so nothing about a Theme swap alone would otherwise ever
    /// invalidate it: the framework's own per-control Theme-transition invalidation is computed
    /// against each control's <em>own</em> style, and <see cref="_content"/>'s own style is the
    /// generic control default, which does not reference any of <see cref="CodeViewStyle"/>'s
    /// syntax-color roles. <see cref="INotifyPropertyChanged.PropertyChanged"/>'s
    /// <see cref="ActualStyle"/> notification already fires for both the local-assignment and the
    /// Theme-swap path, so subscribing once here - rather than only from the local-assignment
    /// callback a Theme swap never invokes - keeps the fold gutter glyph and every syntax color
    /// live across both paths identically.
    /// </remarks>
    private void OnOwnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;

        if (e.PropertyName == nameof(ActualStyle))
        {
            _content.RequestInvalidate(InvalidationImpact.Render);
        }
    }

    #endregion

    #region Scrolling

    /// <summary>Gets or sets which overflow axes provide generated scrollbars.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown flags.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get => _stack.ScrollBars;
        set => _stack.ScrollBars = value;
    }

    /// <summary>Gets or sets the visibility policy for generated scrollbars.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a known member.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => _stack.ShowScrollBars;
        set => _stack.ShowScrollBars = value;
    }

    /// <summary>Gets the committed content extent in terminal cells.</summary>
    public Size Extent => _stack.Extent;

    /// <summary>Gets the committed visible extent in terminal cells.</summary>
    public Size Viewport => _stack.Viewport;

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int HorizontalOffset
    {
        get => _stack.HorizontalOffset;
        set => _stack.HorizontalOffset = value;
    }

    /// <summary>Gets or sets the valid vertical content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int VerticalOffset
    {
        get => _stack.VerticalOffset;
        set => _stack.VerticalOffset = value;
    }

    /// <summary>Gets or sets the non-negative wheel-scroll cell increment.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int LineSize
    {
        get => _stack.LineSize;
        set => _stack.LineSize = value;
    }

    /// <summary>Gets or sets the non-negative cells of context retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int PageOverlap
    {
        get => _stack.PageOverlap;
        set => _stack.PageOverlap = value;
    }

    /// <summary>Applies signed cell deltas with saturation and endpoint clamping.</summary>
    /// <param name="x">The horizontal cell delta.</param>
    /// <param name="y">The vertical cell delta.</param>
    /// <param name="cause">The originating cause reported to <see cref="ScrollChanged"/>.</param>
    /// <returns>True when the committed offset changed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ScrollBy(int x, int y, ScrollCause cause = ScrollCause.Programmatic) => _stack.ScrollBy(x, y, cause);

    /// <summary>Raised after one settled offset, extent, or viewport transition.</summary>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged
    {
        add => _stack.ScrollChanged += value;
        remove => _stack.ScrollChanged -= value;
    }

    #endregion

    #region Selection

    /// <inheritdoc/>
    public override SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();
        LastSelectableTextSnapshotInspectedLineCount = 0;

        if (!EffectiveIsVisible)
        {
            return new SelectableTextSnapshot(NormalizedCode, [], isAuthoritative: true);
        }

        var clip = GetDescendantSelectableTextInheritedClip(_content);
        var viewport = SelectableTextViewportAbsolute();
        var textViewport = new Rect(
            viewport.X + GutterWidth,
            viewport.Y,
            Math.Max(0, viewport.Width - GutterWidth),
            viewport.Height);
        clip = clip.Intersect(textViewport);
        var glyphs = new List<SelectableTextGlyph>();
        var firstVisibleIndex = (int) Math.Clamp(
            (long) VerticalOffset + clip.Y - viewport.Y,
            0,
            _visibleLines.Count);
        var lastVisibleIndex = (int) Math.Clamp(
            (long) VerticalOffset + clip.Bottom - viewport.Y,
            firstVisibleIndex,
            _visibleLines.Count);

        for (var visibleIndex = firstVisibleIndex; visibleIndex < lastVisibleIndex; visibleIndex++)
        {
            LastSelectableTextSnapshotInspectedLineCount++;
            var sourceLine = _visibleLines[visibleIndex];
            var y = viewport.Y + visibleIndex - VerticalOffset;

            var line = _lines[sourceLine];
            var lineStart = LineStartOffset(sourceLine);
            var cells = 0;

            foreach (var grapheme in Graphemes.Enumerate(line))
            {
                var cluster = line.AsSpan(grapheme.Offset, grapheme.Length);
                var width = CodeClusterWidth(cluster);
                var absolute = new Rect(
                    viewport.X + GutterWidth + cells - HorizontalOffset,
                    y,
                    width,
                    1);

                if (width > 0 && ContainsCompleteGlyph(clip, absolute))
                {
                    glyphs.Add(new SelectableTextGlyph(
                        new TextSelection(
                            lineStart + grapheme.Offset,
                            lineStart + grapheme.Offset + grapheme.Length),
                        new Rect(
                            absolute.X - Bounds.X,
                            absolute.Y - Bounds.Y,
                            absolute.Width,
                            absolute.Height)));
                }

                cells += width;
            }
        }

        return new SelectableTextSnapshot(NormalizedCode, glyphs, isAuthoritative: true);
    }

    /// <inheritdoc/>
    public Rect SelectableTextViewport
    {
        get
        {
            VerifyMutable();
            var viewport = SelectableTextViewportAbsolute();
            return new Rect(
                viewport.X - Bounds.X,
                viewport.Y - Bounds.Y,
                viewport.Width,
                viewport.Height);
        }
    }

    /// <inheritdoc/>
    public bool RevealSelectableTextOffset(int offset)
    {
        VerifyMutable();
        TextEdit.Validate(NormalizedCode, new TextSelection(offset, offset));

        var line = LineAt(offset);
        var expanded = ExpandFoldsContaining(line);

        if (expanded)
        {
            RebuildVisibleLines();
            _pendingRevealOffset = offset;
            _pendingRevealProjection = _visibleLines;
            _content.RequestInvalidate(InvalidationImpact.Measure);
            return true;
        }

        if (ReferenceEquals(_pendingRevealProjection, _visibleLines))
        {
            _pendingRevealOffset = offset;
            return true;
        }

        return RevealOffset(offset, _visibleLines);
    }

    /// <inheritdoc/>
    public bool ScrollSelectableTextViewport(int horizontal, int vertical)
    {
        VerifyMutable();
        return _stack.ScrollBy(horizontal, vertical, ScrollCause.Pointer);
    }

    [Pure]
    private Rect SelectableTextViewportAbsolute() => new(
        _stack.Bounds.X,
        _stack.Bounds.Y,
        Viewport.Width,
        Viewport.Height);

    [Pure]
    private static bool ContainsCompleteGlyph(Rect clip, Rect candidate) =>
        candidate.X >= clip.X && candidate.Y >= clip.Y &&
        (long) candidate.X + candidate.Width <= (long) clip.X + clip.Width &&
        (long) candidate.Y + candidate.Height <= (long) clip.Y + clip.Height;

    [Pure]
    private int CodeClusterWidth(ReadOnlySpan<char> cluster) =>
        cluster is ['\t'] ? 1 : MeasureCells(cluster);

    [Pure]
    private int MeasureCodeCells(ReadOnlySpan<char> text)
    {
        var cells = 0;

        foreach (var grapheme in Graphemes.Enumerate(text))
        {
            cells += CodeClusterWidth(text.Slice(grapheme.Offset, grapheme.Length));
        }

        return cells;
    }

    /// <summary>Gets the current directional selection over the normalized <see cref="Code"/> text.</summary>
    public TextSelection Selection => CommittedTextSelection;

    /// <inheritdoc/>
    public override string SelectedText => CommittedTextSelection.IsEmpty
        ? string.Empty
        : NormalizedCode.Substring(CommittedTextSelection.Start, CommittedTextSelection.Length);

    /// <summary>Raised after the committed selection changes.</summary>
    public event EventHandler<EventArgs>? SelectionChanged;

    /// <summary>Replaces the current selection with a validated grapheme-boundary range.</summary>
    /// <param name="selection">The proposed selection over the normalized <see cref="Code"/> text.</param>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the normalized text length.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme cluster.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void SetSelection(TextSelection selection) => SetTextSelection(selection);

    /// <summary>Selects the entire normalized <see cref="Code"/> text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void SelectAll() => SelectAllText();

    /// <summary>Collapses the selection to an empty range at its current caret.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ClearSelection() => ClearTextSelection();

    /// <summary>
    /// Returns the currently selected text without touching any clipboard. Mirrors
    /// <c>TextInput.CopySelection</c>: the host application - not this control - decides whether
    /// and how to publish the returned text to a real clipboard.
    /// </summary>
    /// <returns>The selected substring, or an empty string when nothing is selected.</returns>
    [Pure]
    public string CopySelection() => SelectedText;

    /// <summary>
    /// Gets or sets the delegate <see cref="RequestClipboardCopy"/> forwards <see cref="CopySelection"/>'s
    /// result to, invoked by the default <see cref="CodeViewContextMenu"/>'s Copy item and by
    /// a detached or manually routed Ctrl+C command. Left null by default. An attached control is
    /// discovered through <see cref="IClipboardCopySource"/> by <see cref="Application"/>, so its
    /// ordinary Ctrl+C command uses the application's clipboard path without this delegate.
    /// </summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Action<string>? ClipboardWriter
    {
        get;
        set
        {
            VerifyMutable();
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    }

    /// <summary>Invokes <see cref="ClipboardWriter"/>, if any, with the current <see cref="CopySelection"/> result.</summary>
    internal void RequestClipboardCopy() => ClipboardWriter?.Invoke(CopySelection());

    /// <inheritdoc/>
    protected override void OnTextSelectionCommitted(TextSelectionChangedEventArgs eventArgs)
    {
        _ = eventArgs;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    protected override TerminalStyle ApplyTextSelectionStyle(TerminalStyle current) => new(
        ResolveColor(ActualStyle.SelectedTextColor, Theme),
        ResolveColor(ActualStyle.SelectedBackground, Theme),
        current.Attributes,
        current.Hyperlink,
        current.Underline,
        current.UnderlineColor);

    /// <inheritdoc/>
    protected override bool HasAuthoritativeTextSelectionProjection => true;

    /// <inheritdoc/>
    protected override bool CaptureTextSelectionOnPress => true;

    /// <inheritdoc/>
    protected override bool IsTextSelectionPointerTarget(ControlBase? originalSource, Point cells)
    {
        _ = originalSource;
        var viewport = SelectableTextViewportAbsolute();
        return _content.Bounds.Contains(cells) &&
               cells.X >= viewport.X + GutterWidth;
    }

    /// <inheritdoc/>
    protected override int HitTestTextSelectionCore(Point cells)
    {
        var viewport = SelectableTextViewportAbsolute();
        var y = viewport.Height <= 0
            ? viewport.Y
            : Math.Clamp(cells.Y, viewport.Y, viewport.Bottom - 1);
        return OffsetAt(new Point(cells.X, y)) ?? 0;
    }

    /// <inheritdoc/>
    protected override int TextSelectionPageDistance() => Math.Max(1, Viewport.Height - PageOverlap);

    /// <inheritdoc/>
    protected override SelectableTextSnapshot GetTextSelectionProjection()
    {
        var glyphs = new List<SelectableTextGlyph>();
        var viewport = SelectableTextViewportAbsolute();
        var originX = viewport.X - Bounds.X + GutterWidth;
        var originY = viewport.Y - Bounds.Y;

        for (var visibleIndex = 0; visibleIndex < _visibleLines.Count; visibleIndex++)
        {
            var sourceLine = _visibleLines[visibleIndex];
            var text = _lines[sourceLine];
            var lineStart = LineStartOffset(sourceLine);
            var x = 0;

            foreach (var grapheme in Graphemes.Enumerate(text))
            {
                var cluster = text.AsSpan(grapheme.Offset, grapheme.Length);
                var width = CodeClusterWidth(cluster);
                glyphs.Add(new SelectableTextGlyph(
                    new TextSelection(lineStart + grapheme.Offset, lineStart + grapheme.Offset + grapheme.Length),
                    new Rect(originX + x, originY + visibleIndex, width, 1)));
                x += width;
            }
        }

        return new SelectableTextSnapshot(NormalizedCode, glyphs, isAuthoritative: true);
    }

    /// <inheritdoc/>
    protected override Rect GetTextSelectionAdornmentBounds(Rect bounds) => new(
        Bounds.X + bounds.X - HorizontalOffset,
        Bounds.Y + bounds.Y - VerticalOffset,
        bounds.Width,
        bounds.Height);

    #endregion

    #region Folding

    /// <summary>
    /// Gets or sets whether fold ranges are visible at all: a gutter column showing each fold
    /// start line's collapsed or expanded arrow, clickable to toggle it, and collapsed ranges
    /// actually hiding their interior lines. When false, the gutter is not reserved, no line is
    /// ever hidden regardless of prior <see cref="SetFolded"/>/<see cref="CollapseAll"/> calls, and
    /// <see cref="IsFolded"/> continues to report the preserved collapsed/expanded state those
    /// calls recorded, ready to resume the instant folding is re-enabled.
    /// </summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsFoldingEnabled
    {
        get;
        set
        {
            VerifyMutable();

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                RebuildVisibleLines();
                _content.RequestInvalidate(InvalidationImpact.Measure);
            }
        }
    } = true;

    /// <summary>Gets the fold-gutter column width in cells: <see cref="_foldGutterWidth"/> when
    /// <see cref="IsFoldingEnabled"/>, otherwise zero.</summary>
    private int GutterWidth => IsFoldingEnabled ? _foldGutterWidth : 0;

    /// <summary>Gets every fold range detected in the current <see cref="Code"/>, outer ranges first.</summary>
    public IReadOnlyList<SyntaxFoldRange> FoldRanges => _result.FoldRanges;

    /// <summary>Gets whether a line begins a fold range that is currently collapsed.</summary>
    /// <param name="line">The zero-based source line index.</param>
    /// <returns>True when <paramref name="line"/> begins a currently collapsed fold range.</returns>
    [Pure]
    public bool IsFolded(int line) => _foldedStartLines.Contains(line);

    /// <summary>Gets whether a line begins any fold range at all, collapsed or not.</summary>
    /// <param name="line">The zero-based source line index.</param>
    /// <returns>True when <paramref name="line"/> begins a fold range.</returns>
    [Pure]
    public bool IsFoldStart(int line) => _foldStartRanges.ContainsKey(line);

    /// <summary>Collapses or expands the fold range starting at one line.</summary>
    /// <param name="line">The zero-based source line index that begins a fold range.</param>
    /// <param name="folded">Whether the range should be collapsed.</param>
    /// <returns>True when the range's own state changed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool SetFolded(int line, bool folded)
    {
        VerifyMutable();

        if (!_foldStartRanges.ContainsKey(line))
        {
            return false;
        }

        var changed = folded ? _foldedStartLines.Add(line) : _foldedStartLines.Remove(line);

        if (changed)
        {
            RebuildVisibleLines();
            _content.RequestInvalidate(InvalidationImpact.Measure);
        }

        return changed;
    }

    /// <summary>Toggles the fold range starting at one line.</summary>
    /// <param name="line">The zero-based source line index that begins a fold range.</param>
    /// <returns>True when <paramref name="line"/> begins a fold range and was toggled.</returns>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ToggleFold(int line) => _foldStartRanges.ContainsKey(line) && SetFolded(line, !IsFolded(line));

    private bool ExpandFoldsContaining(int line) =>
        IsFoldingEnabled && _foldedStartLines.RemoveWhere(startLine =>
            _foldStartRanges.TryGetValue(startLine, out var range) &&
            line > range.StartLine && line <= range.EndLine) > 0;

    /// <summary>Collapses every fold range.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void CollapseAll()
    {
        VerifyMutable();
        _foldedStartLines.Clear();

        foreach (var line in _foldStartRanges.Keys)
        {
            _ = _foldedStartLines.Add(line);
        }

        RebuildVisibleLines();
        _content.RequestInvalidate(InvalidationImpact.Measure);
    }

    /// <summary>Expands every fold range.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ExpandAll()
    {
        VerifyMutable();

        if (_foldedStartLines.Count == 0)
        {
            return;
        }

        _foldedStartLines.Clear();
        RebuildVisibleLines();
        _content.RequestInvalidate(InvalidationImpact.Measure);
    }

    #endregion

    #region Layout and rendering

    /// <summary>Gets the normalized (line endings collapsed to <c>\n</c>) source text.</summary>
    private string NormalizedCode { get; set; } = string.Empty;

    /// <summary>Measures the content extent from the current visible-line projection.</summary>
    /// <returns>The width-and-height extent in terminal cells.</returns>
    [Pure]
    internal Size MeasureProjection() => new(_extentWidth, _visibleLines.Count);

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        base.ArrangeOverride(bounds);

        if (_pendingRevealOffset.HasValue && !_pendingRevealPosted && Dispatcher is { } dispatcher)
        {
            _pendingRevealPosted = true;

            try
            {
                dispatcher.Post(ProcessPendingReveal);
            }
            catch
            {
                _pendingRevealPosted = false;
                throw;
            }
        }
    }

    /// <summary>Draws every visible projected line intersecting the clipped content surface.</summary>
    /// <param name="canvas">The clipped semantic canvas.</param>
    /// <param name="bounds">The content surface bounds.</param>
    internal void RenderProjectedContent(TerminalCanvas canvas, Rect bounds)
    {
        var first = Math.Max(0, canvas.Bounds.Y - bounds.Y);
        var last = Math.Min(_visibleLines.Count, canvas.Bounds.Bottom - bounds.Y);
        var style = ActualStyle;
        var gutterStyle = ResolvedStyle.WithForeground(ResolveColor(style.GutterColor, Theme));
        var viewportX = SelectableTextViewportAbsolute().X;

        for (var row = first; row < last; row++)
        {
            var sourceLine = _visibleLines[row];
            var y = bounds.Y + row;
            var x = viewportX;

            if (IsFoldingEnabled)
            {
                DrawGutter(canvas, sourceLine, gutterStyle, style, x, y);
            }

            x += GutterWidth;
            DrawLine(canvas, sourceLine, style, x, y);
        }
    }

    private void DrawGutter(TerminalCanvas canvas, int sourceLine, TerminalStyle gutterStyle, CodeViewStyle style, int x, int y)
    {
        if (!_foldStartRanges.ContainsKey(sourceLine))
        {
            return;
        }

        var glyph = IsFolded(sourceLine) ? style.CollapsedGlyph : style.ExpandedGlyph;
        Span<char> buffer = stackalloc char[2];
        var written = glyph.EncodeToUtf16(buffer);
        _ = canvas.Draw(buffer[..written], new Point(x, y), gutterStyle);
    }

    private void DrawLine(TerminalCanvas canvas, int sourceLine, CodeViewStyle style, int x, int y)
    {
        var text = _lines[sourceLine];
        var tokens = _result.Lines[sourceLine].Tokens;
        var offset = HorizontalOffset;
        var column = 0;

        foreach (var token in tokens)
        {
            var tokenStyle = ResolvedStyle.WithForeground(ResolveColor(style.ColorFor(token.Style), Theme));
            DrawSlice(canvas, text.AsSpan(token.Start, token.Length), tokenStyle, x, y, ref column, offset, BackgroundMode.Transparent);
        }

        if (IsFoldingEnabled && IsFolded(sourceLine))
        {
            var indicatorStyle = ResolvedStyle.WithForeground(ResolveColor(style.GutterColor, Theme));
            DrawSlice(canvas, " (...)", indicatorStyle, x, y, ref column, offset, BackgroundMode.Transparent);
        }

    }

    private void DrawSlice(
        TerminalCanvas canvas,
        ReadOnlySpan<char> raw,
        TerminalStyle style,
        int x,
        int y,
        ref int column,
        int horizontalOffset,
        BackgroundMode background,
        int? startColumn = null)
    {
        var tokenStartColumn = startColumn ?? column;
        var tokenCells = MeasureCodeCells(raw);

        column = tokenStartColumn + tokenCells;

        if (raw.Length == 0 || tokenStartColumn + tokenCells <= horizontalOffset)
        {
            return;
        }

        var cellsToSkip = Math.Max(0, horizontalOffset - tokenStartColumn);
        var visibleStart = 0;
        var skippedCells = 0;

        foreach (var grapheme in Graphemes.Enumerate(raw))
        {
            if (skippedCells >= cellsToSkip)
            {
                break;
            }

            skippedCells += CodeClusterWidth(raw.Slice(grapheme.Offset, grapheme.Length));
            visibleStart = grapheme.Offset + grapheme.Length;
        }

        if (visibleStart >= raw.Length)
        {
            return;
        }

        var visible = raw[visibleStart..];
        var drawX = x + tokenStartColumn + skippedCells - horizontalOffset;

        var buffer = visible.Length <= 512 ? stackalloc char[visible.Length] : new char[visible.Length];

        for (var i = 0; i < visible.Length; i++)
        {
            buffer[i] = visible[i] == '\t' ? ' ' : visible[i];
        }

        _ = canvas.Draw(buffer, new Point(drawX, y), style, background: background);
    }

    #endregion

    #region Keyboard input

    private void ProcessPendingReveal()
    {
        _pendingRevealPosted = false;

        if (_pendingRevealOffset is not { } offset || _pendingRevealProjection is not { } projection)
        {
            return;
        }

        _pendingRevealOffset = null;
        _pendingRevealProjection = null;
        _ = RevealOffset(offset, projection);
    }

    private bool RevealOffset(int offset, List<int> projection)
    {
        if (!CanContinueReveal(offset, projection))
        {
            return false;
        }

        var line = LineAt(offset);
        var visibleIndex = projection.BinarySearch(line);

        if (visibleIndex < 0)
        {
            return false;
        }

        var previousHorizontal = HorizontalOffset;
        var previousVertical = VerticalOffset;
        var targetVertical = previousVertical;

        if (Viewport.Height > 0 && visibleIndex < previousVertical)
        {
            targetVertical = visibleIndex;
        }
        else if (Viewport.Height > 0 && visibleIndex >= previousVertical + Viewport.Height)
        {
            targetVertical = visibleIndex - Viewport.Height + 1;
        }

        if (targetVertical != previousVertical)
        {
            VerticalOffset = targetVertical;

            if (!CanContinueReveal(offset, projection))
            {
                return false;
            }
        }

        var column = MeasureCodeCellsToOffset(line, offset - LineStartOffset(line));

        // The gutter occupies the leftmost GutterWidth cells of Viewport.Width and never scrolls,
        // so only the remaining cells actually show scrolled text columns. Comparing column against
        // the full Viewport.Width - as if the gutter's cells could show text too - let the caret
        // drift up to GutterWidth columns past the true right edge before a scroll ever triggered.
        var textViewportWidth = Viewport.Width - GutterWidth;

        if (textViewportWidth <= 0)
        {
            return VerticalOffset != previousVertical;
        }

        var targetHorizontal = previousHorizontal;

        if (column < previousHorizontal)
        {
            targetHorizontal = column;
        }
        else if (column >= previousHorizontal + textViewportWidth)
        {
            // Clamped rather than assigned outright: the extent's widest line reserves exactly its
            // own printable width with no phantom trailing column for a caret sitting one past its
            // last character, so revealing that exact position can compute one cell past the
            // Container-validated maximum offset. Clamping saturates at the furthest offset that is
            // still valid - matching ScrollBy's own saturating contract - instead of letting the
            // Container's offset setter throw ArgumentOutOfRangeException out of a keyboard handler.
            var maximumHorizontalOffset = Math.Max(0, Extent.Width - Viewport.Width);
            targetHorizontal = Math.Min(maximumHorizontalOffset, column - textViewportWidth + 1);
        }

        if (targetHorizontal != previousHorizontal)
        {
            HorizontalOffset = targetHorizontal;

            if (!CanContinueReveal(offset, projection))
            {
                return false;
            }
        }

        return HorizontalOffset != previousHorizontal || VerticalOffset != previousVertical;
    }

    [Pure]
    private bool CanContinueReveal(int offset, List<int> projection) =>
        !IsDisposed && EffectiveIsVisible &&
        ReferenceEquals(_visibleLines, projection) &&
        offset >= 0 && offset <= NormalizedCode.Length;

    [Pure]
    private int MeasureCodeCellsToOffset(int line, int offset)
        => MeasureCodeCells(_lines[line].AsSpan(0, offset));

    [Pure]
    private int OffsetForCodeCells(int line, int targetCells)
    {
        var text = _lines[line];
        var cells = 0;

        foreach (var grapheme in Graphemes.Enumerate(text))
        {
            var width = CodeClusterWidth(text.AsSpan(grapheme.Offset, grapheme.Length));

            if (targetCells < cells + width)
            {
                return targetCells - cells < (width + 1) / 2
                    ? grapheme.Offset
                    : grapheme.Offset + grapheme.Length;
            }

            cells += width;
        }

        return text.Length;
    }

    #endregion

    #region Pointer input

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        base.OnEvent(eventArgs);

        if (eventArgs.IsHandled || eventArgs is not PointerEventArgs { Pointer: var pointer })
        {
            return;
        }

        if (pointer.Action == PointerAction.Wheel)
        {
            eventArgs.IsHandled = ScrollBy(pointer.WheelX, -pointer.WheelY, ScrollCause.Wheel);
            return;
        }

        if (pointer is
            {
                Action: PointerAction.Press,
                Buttons: var buttons,
                Cells: { } pressedCells
            } &&
            (buttons & Buttons.Primary) != 0 &&
            IsFoldingEnabled &&
            TryToggleFoldAt(pressedCells))
        {
            _ = RequestFocus();
            eventArgs.IsHandled = true;
        }
    }

    /// <summary>Toggles the fold starting at the visible line under a press, if the press landed in the fold gutter.</summary>
    /// <param name="pressedCells">The root-relative press position, already known to be within <see cref="_content"/>'s bounds.</param>
    /// <returns>True when the press landed in the gutter of a line that begins a fold range.</returns>
    private bool TryToggleFoldAt(Point pressedCells)
    {
        var viewport = SelectableTextViewportAbsolute();
        var column = pressedCells.X - viewport.X;

        if (column < 0 || column >= GutterWidth)
        {
            return false;
        }

        var row = pressedCells.Y - viewport.Y + VerticalOffset;

        if (row < 0 || row >= _visibleLines.Count)
        {
            return false;
        }

        var sourceLine = _visibleLines[row];
        return _foldStartRanges.ContainsKey(sourceLine) && ToggleFold(sourceLine);
    }

    #endregion

    #region Offset and line helpers

    [Pure]
    private int LineAt(int offset)
    {
        var index = Array.BinarySearch(_lineStartOffsets, offset);
        var line = index >= 0 ? index : ~index - 1;
        return Math.Clamp(line, 0, _lines.Length - 1);
    }

    [Pure]
    private int LineStartOffset(int line) => _lineStartOffsets[line];

    [Pure]
    private int LineEndOffset(int line) => _lineStartOffsets[line] + _lines[line].Length;

    [Pure]
    private int? OffsetAt(Point cells)
    {
        var viewport = SelectableTextViewportAbsolute();
        var row = cells.Y - viewport.Y + VerticalOffset;

        if (row < 0 || row >= _visibleLines.Count)
        {
            return null;
        }

        var sourceLine = _visibleLines[row];
        Debug.Assert(
            sourceLine >= 0 && sourceLine < _lines.Length,
            "RebuildVisibleLines only ever appends indices it iterated over _lines with, so every entry is a valid source-line index.");
        var column = Math.Max(0, cells.X - viewport.X - GutterWidth + HorizontalOffset);
        return LineStartOffset(sourceLine) + OffsetForCodeCells(sourceLine, column);
    }

    #endregion

    #region Projection

    private void RebuildProjection()
    {
        CancelTextSelectionGesture(releaseCapture: true);
        _pendingRevealOffset = null;
        _pendingRevealProjection = null;
        NormalizedCode = Code.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        _lines = NormalizedCode.Split('\n');
        _lineStartOffsets = new int[_lines.Length + 1];

        var cursor = 0;

        for (var i = 0; i < _lines.Length; i++)
        {
            _lineStartOffsets[i] = cursor;
            cursor += _lines[i].Length + 1;
        }

        _lineStartOffsets[^1] = NormalizedCode.Length;

        _result = _grammar is { } grammar ? SyntaxTokenizer.Tokenize(grammar, NormalizedCode) : BuildPlainResult(_lines);
        _foldedStartLines.Clear();
        _foldStartRanges = BuildFoldStartRanges(_result.FoldRanges);
        RebuildVisibleLines();
        _ = CommitTextSelection(default);
        _content.RequestInvalidate(InvalidationImpact.Measure);
    }

    [Pure]
    private static Dictionary<int, SyntaxFoldRange> BuildFoldStartRanges(IReadOnlyList<SyntaxFoldRange> ranges)
    {
        var result = new Dictionary<int, SyntaxFoldRange>();

        foreach (var range in ranges)
        {
            if (range.EndLine > range.StartLine)
            {
                _ = result.TryAdd(range.StartLine, range);
            }
        }

        return result;
    }

    private void RebuildVisibleLines()
    {
        var hidden = new bool[_lines.Length];

        // Collapsed ranges only actually hide lines while folding is visible; the collapsed
        // bookkeeping in _foldedStartLines is preserved either way, ready to resume the moment
        // IsFoldingEnabled flips back on.
        if (IsFoldingEnabled)
        {
            foreach (var startLine in _foldedStartLines)
            {
                var isTrackedFoldStart = _foldStartRanges.TryGetValue(startLine, out var range);
                Debug.Assert(
                    isTrackedFoldStart,
                    "SetFolded and CollapseAll only ever add a line already present in _foldStartRanges, and RebuildProjection clears _foldedStartLines every time it rebuilds _foldStartRanges, so the two collections never drift apart.");

                for (var i = range.StartLine + 1; i <= range.EndLine && i < hidden.Length; i++)
                {
                    hidden[i] = true;
                }
            }
        }

        _visibleLines = [];

        for (var i = 0; i < _lines.Length; i++)
        {
            if (!hidden[i])
            {
                _visibleLines.Add(i);
            }
        }

        Debug.Assert(
            _visibleLines.Count > 0,
            "string.Split always yields at least one line, and a fold's own start line (index range.StartLine, never range.StartLine + 1 or later) is never marked hidden above, so at least one line always stays visible.");
        _extentWidth = 0;

        foreach (var line in _visibleLines)
        {
            _extentWidth = Math.Max(_extentWidth, GutterWidth + MeasureCodeCells(_lines[line]));
        }
    }

    [Pure]
    private static SyntaxHighlightResult BuildPlainResult(string[] lines)
    {
        var result = new List<SyntaxHighlightedLine>(lines.Length);

        foreach (var line in lines)
        {
            result.Add(new SyntaxHighlightedLine(line.Length == 0 ? [] : [new SyntaxToken(0, line.Length, SyntaxDefaultStyle.Normal)]));
        }

        return new SyntaxHighlightResult(result, []);
    }

    #endregion
}
