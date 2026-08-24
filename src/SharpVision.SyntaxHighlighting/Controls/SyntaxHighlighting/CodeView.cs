// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.SyntaxHighlighting;

using System.ComponentModel;

using SharpVision.Controls.Scrolling;
using SharpVision.Scrolling;
using SharpVision.SyntaxHighlighting;
using SharpVision.Terminal.Input;

using KeyCode = Code;
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
/// this control exposes (<see cref="Selection"/>, <see cref="SelectedText"/>) is relative to that
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
public sealed class CodeView: CompositeControlBase, IStyled<CodeViewStyle>
{
    private const int _foldGutterWidth = 2;

    /// <summary>The interval between auto-scroll steps while a captured drag holds past the
    /// content's edge. Matches the cadence a moving pointer would otherwise supply through its own
    /// motion events, so a held-still drag makes the same steady forward progress.</summary>
    private static readonly TimeSpan _autoScrollInterval = TimeSpan.FromMilliseconds(60);

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
    private int _pointerAnchor;
    private bool _pointerSelecting;
    private Point _lastDragCells;
    private DispatcherTimer? _autoScrollTimer;
    private int? _desiredColumn;

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
        _ = AddHandler(Events.Key, OnKeyRouted);
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

    /// <summary>Gets the current directional selection over the normalized <see cref="Code"/> text.</summary>
    public TextSelection Selection { get; private set; }

    /// <summary>Gets the selected substring of the normalized <see cref="Code"/> text, or an empty string.</summary>
    public string SelectedText => Selection.IsEmpty ? string.Empty : NormalizedCode.Substring(Selection.Start, Selection.Length);

    /// <summary>Raised after the committed selection changes.</summary>
    public event EventHandler<EventArgs>? SelectionChanged;

    /// <summary>Replaces the current selection with a validated grapheme-boundary range.</summary>
    /// <param name="selection">The proposed selection over the normalized <see cref="Code"/> text.</param>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the normalized text length.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme cluster.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void SetSelection(TextSelection selection)
    {
        VerifyMutable();
        TextEdit.Validate(NormalizedCode, selection);
        CommitSelection(selection, resetDesiredColumn: true);
    }

    /// <summary>Selects the entire normalized <see cref="Code"/> text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void SelectAll()
    {
        VerifyMutable();
        CommitSelection(new TextSelection(0, NormalizedCode.Length), resetDesiredColumn: true);
    }

    /// <summary>Collapses the selection to an empty range at its current caret.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ClearSelection()
    {
        VerifyMutable();
        CommitSelection(new TextSelection(Selection.Caret, Selection.Caret), resetDesiredColumn: true);
    }

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
    /// Ctrl+C. Left null by default: unlike <c>TextInput</c>, this control's host application is
    /// never automatically discovered by <c>Application</c> (that mechanism is hard-typed to
    /// <c>TextInput</c> and cannot be extended from another assembly), so a host that wants Copy to
    /// reach a real clipboard must assign this delegate itself - for example to
    /// <c>value => Application.Terminal.Clipboard.Write(value)</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Action<string>? ClipboardWriter
    {
        get;
        set
        {
            VerifyMutable();
            field = value;
        }
    }

    /// <summary>Invokes <see cref="ClipboardWriter"/>, if any, with the current <see cref="CopySelection"/> result.</summary>
    internal void RequestClipboardCopy() => ClipboardWriter?.Invoke(CopySelection());

    private void CommitSelection(TextSelection selection, bool resetDesiredColumn)
    {
        Debug.Assert(
            selection.Start >= 0 && selection.End <= NormalizedCode.Length,
            "Every call site (SetSelection via TextEdit.Validate, or a computed offset already clamped to a line's own bounds) provides endpoints within the normalized text.");

        if (Selection == selection)
        {
            return;
        }

        Selection = selection;

        if (resetDesiredColumn)
        {
            _desiredColumn = null;
        }

        _content.RequestInvalidate(InvalidationImpact.Render);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

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

    /// <summary>Draws every visible projected line intersecting the clipped content surface.</summary>
    /// <param name="canvas">The clipped semantic canvas.</param>
    /// <param name="bounds">The content surface bounds.</param>
    internal void RenderProjectedContent(TerminalCanvas canvas, Rect bounds)
    {
        var first = Math.Max(0, canvas.Bounds.Y - bounds.Y);
        var last = Math.Min(_visibleLines.Count, canvas.Bounds.Bottom - bounds.Y);
        var style = ActualStyle;
        var gutterStyle = ResolvedStyle.WithForeground(ResolveColor(style.GutterColor, Theme));

        for (var row = first; row < last; row++)
        {
            var sourceLine = _visibleLines[row];
            var y = bounds.Y + row;
            var x = bounds.X;

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

        DrawSelectionOverlay(canvas, sourceLine, text, style, x, y, offset);
    }

    private void DrawSelectionOverlay(TerminalCanvas canvas, int sourceLine, string text, CodeViewStyle style, int x, int y, int horizontalOffset)
    {
        if (Selection.IsEmpty)
        {
            return;
        }

        var lineStart = _lineStartOffsets[sourceLine];
        var lineEnd = LineEndOffset(sourceLine);
        var selectionStart = Math.Max(Selection.Start, lineStart);
        var selectionEnd = Math.Min(Selection.End, lineEnd);

        if (selectionStart >= selectionEnd)
        {
            return;
        }

        var overlayStyle = WithColors(
            ResolvedStyle,
            ResolveColor(style.SelectedTextColor, Theme),
            ResolveColor(style.SelectedBackground, Theme));
        var column = selectionStart - lineStart;
        var slice = text.AsSpan(selectionStart - lineStart, selectionEnd - selectionStart);
        DrawSlice(canvas, slice, overlayStyle, x, y, ref column, horizontalOffset, BackgroundMode.Opaque, startColumn: selectionStart - lineStart);
    }

    private static void DrawSlice(
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
        var length = raw.Length;
        column = tokenStartColumn + length;

        if (length == 0 || tokenStartColumn + length <= horizontalOffset)
        {
            return;
        }

        var visibleStart = Math.Max(0, horizontalOffset - tokenStartColumn);

        if (visibleStart >= length)
        {
            return;
        }

        var visible = raw[visibleStart..];
        var drawX = x + Math.Max(0, tokenStartColumn - horizontalOffset);

        var buffer = visible.Length <= 512 ? stackalloc char[visible.Length] : new char[visible.Length];

        for (var i = 0; i < visible.Length; i++)
        {
            buffer[i] = visible[i] == '\t' ? ' ' : visible[i];
        }

        _ = canvas.Draw(buffer, new Point(drawX, y), style, background: background);
    }

    [Pure]
    private static TerminalStyle WithColors(TerminalStyle source, Color foreground, Color background) => new(
        foreground,
        background,
        source.Attributes,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);

    #endregion

    #region Keyboard input

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        var stroke = eventArgs.Stroke;
        var extend = (stroke.Modifiers & Modifiers.Shift) != 0;

        if (stroke.Code == KeyCode.Character && (stroke.Modifiers & Modifiers.Control) != 0)
        {
            if (stroke.Character == new Rune('a') || stroke.Character == new Rune('A'))
            {
                SelectAll();
                eventArgs.IsHandled = true;
                return;
            }

            if (stroke.Character == new Rune('c') || stroke.Character == new Rune('C'))
            {
                RequestClipboardCopy();
                eventArgs.IsHandled = true;
                return;
            }
        }

        var code = stroke.Code;

        if (code == KeyCode.Left)
        {
            eventArgs.IsHandled = MoveCaret(TextEdit.MovePrevious(NormalizedCode, Selection, extend).Selection);
        }
        else if (code == KeyCode.Right)
        {
            eventArgs.IsHandled = MoveCaret(TextEdit.MoveNext(NormalizedCode, Selection, extend).Selection);
        }
        else if (code == KeyCode.Up)
        {
            eventArgs.IsHandled = MoveVertically(-1, extend);
        }
        else if (code == KeyCode.Down)
        {
            eventArgs.IsHandled = MoveVertically(1, extend);
        }
        else if (code == KeyCode.Home)
        {
            var lineStart = LineStartOffset(LineAt(Selection.Caret));
            eventArgs.IsHandled = MoveCaret(new TextSelection(extend ? Selection.Anchor : lineStart, lineStart));
        }
        else if (code == KeyCode.End)
        {
            var lineEnd = LineEndOffset(LineAt(Selection.Caret));
            eventArgs.IsHandled = MoveCaret(new TextSelection(extend ? Selection.Anchor : lineEnd, lineEnd));
        }
        else if (code == KeyCode.PageUp)
        {
            eventArgs.IsHandled = MoveVertically(-Math.Max(1, Viewport.Height - PageOverlap), extend);
        }
        else if (code == KeyCode.PageDown)
        {
            eventArgs.IsHandled = MoveVertically(Math.Max(1, Viewport.Height - PageOverlap), extend);
        }
    }

    private bool MoveCaret(TextSelection selection)
    {
        CommitSelection(selection, resetDesiredColumn: true);
        RevealCaret();
        return true;
    }

    private bool MoveVertically(int lineDelta, bool extend)
    {
        if (_visibleLines.Count == 0)
        {
            return false;
        }

        var caretLine = LineAt(Selection.Caret);
        var currentColumn = _desiredColumn ?? (Selection.Caret - LineStartOffset(caretLine));
        var visibleIndex = _visibleLines.BinarySearch(caretLine);

        if (visibleIndex < 0)
        {
            visibleIndex = Math.Min(~visibleIndex, _visibleLines.Count - 1);
        }

        var targetIndex = Math.Clamp(visibleIndex + lineDelta, 0, _visibleLines.Count - 1);
        var targetLine = _visibleLines[targetIndex];
        var targetLineLength = _lines[targetLine].Length;
        var targetOffset = LineStartOffset(targetLine) + Math.Min(currentColumn, targetLineLength);

        CommitSelection(new TextSelection(extend ? Selection.Anchor : targetOffset, targetOffset), resetDesiredColumn: false);
        _desiredColumn = currentColumn;
        RevealCaret();
        return true;
    }

    private void RevealCaret()
    {
        var caretLine = LineAt(Selection.Caret);
        var visibleIndex = _visibleLines.BinarySearch(caretLine);

        if (visibleIndex < 0)
        {
            return;
        }

        if (visibleIndex < VerticalOffset)
        {
            VerticalOffset = visibleIndex;
        }
        else if (visibleIndex >= VerticalOffset + Viewport.Height)
        {
            VerticalOffset = visibleIndex - Viewport.Height + 1;
        }

        var column = Selection.Caret - LineStartOffset(caretLine);

        // The gutter occupies the leftmost GutterWidth cells of Viewport.Width and never scrolls,
        // so only the remaining cells actually show scrolled text columns. Comparing column against
        // the full Viewport.Width - as if the gutter's cells could show text too - let the caret
        // drift up to GutterWidth columns past the true right edge before a scroll ever triggered.
        var textViewportWidth = Math.Max(1, Viewport.Width - GutterWidth);

        if (column < HorizontalOffset)
        {
            HorizontalOffset = column;
        }
        else if (column >= HorizontalOffset + textViewportWidth)
        {
            // Clamped rather than assigned outright: the extent's widest line reserves exactly its
            // own printable width with no phantom trailing column for a caret sitting one past its
            // last character, so revealing that exact position can compute one cell past the
            // Container-validated maximum offset. Clamping saturates at the furthest offset that is
            // still valid - matching ScrollBy's own saturating contract - instead of letting the
            // Container's offset setter throw ArgumentOutOfRangeException out of a keyboard handler.
            var maximumHorizontalOffset = Math.Max(0, Extent.Width - Viewport.Width);
            HorizontalOffset = Math.Min(maximumHorizontalOffset, column - textViewportWidth + 1);
        }
    }

    #endregion

    #region Pointer input

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        Debug.Assert(Dispatcher is not null, "An attached CodeView owns a dispatcher.");
        _autoScrollTimer = new DispatcherTimer(Dispatcher, _autoScrollInterval);
        _autoScrollTimer.Tick += OnAutoScrollTick;
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        ReleaseAutoScrollTimer();
        base.OnDetached();
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        ReleaseAutoScrollTimer();
        base.OnDisposing();
    }

    private void ReleaseAutoScrollTimer()
    {
        if (_autoScrollTimer is not { } timer)
        {
            return;
        }

        timer.Tick -= OnAutoScrollTick;
        timer.Dispose();
        _autoScrollTimer = null;
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        base.OnEvent(eventArgs);

        if (eventArgs.IsHandled || eventArgs is not PointerEventArgs { Pointer: var pointer } pointerEventArgs)
        {
            return;
        }

        if (pointer.Action == PointerAction.Wheel)
        {
            eventArgs.IsHandled = ScrollBy(pointer.WheelX, -pointer.WheelY, ScrollCause.Wheel);
            return;
        }

        if (pointer.Action == PointerAction.Press && (pointer.Buttons & Buttons.Primary) != 0 && pointer.Cells is { } pressedCells)
        {
            HandlePress(pointerEventArgs, pressedCells);
            return;
        }

        if (!_pointerSelecting)
        {
            return;
        }

        if (pointer.Cells is not { } cells)
        {
            if (pointer.Action is PointerAction.Release or PointerAction.Leave)
            {
                CancelPointerSelection();
            }

            eventArgs.IsHandled = true;
            return;
        }

        // Never gated on _content.Bounds.Contains(cells): a captured drag routinely reports
        // positions past the visible content - most usefully, past its right edge on a line
        // wider than the viewport, or past its top/bottom edge on a buffer taller than the
        // viewport. AdvanceDragSelection resolves one step immediately from this move event;
        // UpdateAutoScroll arms a repeating timer that keeps invoking that same step for as long
        // as the drag position remains outside the content while the button stays held, so a
        // drag held still past an edge keeps scrolling and extending the selection instead of
        // stalling the instant the pointer itself stops moving.
        AdvanceDragSelection(cells);
        UpdateAutoScroll(cells);

        eventArgs.IsHandled = true;

        if (pointer.Action is PointerAction.Release or PointerAction.Leave)
        {
            CancelPointerSelection();
        }
    }

    private void HandlePress(PointerEventArgs eventArgs, Point pressedCells)
    {
        if (!Bounds.Contains(pressedCells))
        {
            return;
        }

        _ = RequestFocus();

        if (!_content.Bounds.Contains(pressedCells))
        {
            eventArgs.IsHandled = true;
            return;
        }

        if (IsFoldingEnabled && TryToggleFoldAt(pressedCells))
        {
            eventArgs.IsHandled = true;
            return;
        }

        if (OffsetAt(pressedCells) is not { } offset)
        {
            eventArgs.IsHandled = true;
            return;
        }

        if (eventArgs.ClickCount == 2)
        {
            var line = LineAt(offset);
            var column = offset - LineStartOffset(line);
            var word = TextEdit.SelectWord(_lines[line], column);
            CommitSelection(new TextSelection(LineStartOffset(line) + word.Anchor, LineStartOffset(line) + word.Caret), resetDesiredColumn: true);
            eventArgs.IsHandled = true;
            return;
        }

        if (eventArgs.ClickCount >= 3)
        {
            var line = LineAt(offset);
            CommitSelection(new TextSelection(LineStartOffset(line), LineEndOffset(line)), resetDesiredColumn: true);
            eventArgs.IsHandled = true;
            return;
        }

        if (!CapturePointer())
        {
            eventArgs.IsHandled = true;
            return;
        }

        _pointerAnchor = offset;
        _pointerSelecting = true;
        CommitSelection(new TextSelection(offset, offset), resetDesiredColumn: true);
        eventArgs.IsHandled = true;
    }

    /// <summary>Toggles the fold starting at the visible line under a press, if the press landed in the fold gutter.</summary>
    /// <param name="pressedCells">The root-relative press position, already known to be within <see cref="_content"/>'s bounds.</param>
    /// <returns>True when the press landed in the gutter of a line that begins a fold range.</returns>
    private bool TryToggleFoldAt(Point pressedCells)
    {
        var column = pressedCells.X - _content.Bounds.X;

        if (column < 0 || column >= GutterWidth)
        {
            return false;
        }

        var row = pressedCells.Y - _content.Bounds.Y;

        if (row < 0 || row >= _visibleLines.Count)
        {
            return false;
        }

        var sourceLine = _visibleLines[row];
        return _foldStartRanges.ContainsKey(sourceLine) && ToggleFold(sourceLine);
    }

    private void CancelPointerSelection()
    {
        _pointerSelecting = false;
        _autoScrollTimer?.Stop();
        ReleasePointerCapture();
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _pointerSelecting = false;
        _autoScrollTimer?.Stop();
    }

    /// <summary>Gets the currently visible, clipped viewing rectangle: <see cref="_content"/>'s
    /// own arranged position combined with <see cref="Viewport"/>'s size, rather than <see
    /// cref="_content"/>'s own Bounds size. <see cref="_content"/> is arranged once at its full
    /// unclipped logical extent - which can be far larger than the viewport - and the scrollable
    /// stack shifts that arranged position by the negative scroll offset so the correct portion
    /// lines up with the clip region; <see cref="_content"/>'s own Bounds therefore already
    /// reports the viewport's true root-relative origin, but its Width and Height still describe
    /// the full extent rather than what actually renders on screen.</summary>
    private Rect ViewportBounds => new(_content.Bounds.X, _content.Bounds.Y, Viewport.Width, Viewport.Height);

    /// <summary>Extends the active drag selection toward one pointer position, taking the
    /// vertical step when the position sits above or below the visible <see
    /// cref="ViewportBounds"/> - which <see cref="OffsetAt"/> alone cannot resolve, since a source
    /// line's row on screen depends on which lines are currently scrolled into view, not on a
    /// continuous pixel offset - and otherwise resolving through <see cref="OffsetAt"/>'s own
    /// scroll-aware column clamp for a position that is merely left or right of the viewport.</summary>
    /// <param name="cells">The root-relative pointer position to extend the selection toward.</param>
    private void AdvanceDragSelection(Point cells)
    {
        var viewport = ViewportBounds;

        if (cells.Y < viewport.Y)
        {
            if (!ScrollBy(0, -1, ScrollCause.Pointer))
            {
                return;
            }

            Debug.Assert(
                VerticalOffset < _visibleLines.Count,
                "MeasureProjection reports Extent.Height as exactly _visibleLines.Count, so a saturated VerticalOffset always indexes a real visible line.");
            var line = _visibleLines[VerticalOffset];
            CommitSelection(new TextSelection(_pointerAnchor, LineStartOffset(line)), resetDesiredColumn: true);
            RevealCaret();
            return;
        }

        if (cells.Y >= viewport.Bottom)
        {
            if (!ScrollBy(0, 1, ScrollCause.Pointer))
            {
                return;
            }

            var bottomVisibleIndex = VerticalOffset + Viewport.Height - 1;
            Debug.Assert(
                bottomVisibleIndex < _visibleLines.Count,
                "MeasureProjection reports Extent.Height as exactly _visibleLines.Count, so a saturated VerticalOffset always leaves the bottommost viewport row indexing a real visible line.");
            var line = _visibleLines[bottomVisibleIndex];
            CommitSelection(new TextSelection(_pointerAnchor, LineEndOffset(line)), resetDesiredColumn: true);
            RevealCaret();
            return;
        }

        if (OffsetAt(cells) is { } dragged)
        {
            CommitSelection(new TextSelection(_pointerAnchor, dragged), resetDesiredColumn: true);
            RevealCaret();
        }
    }

    /// <summary>Arms or disarms the repeating auto-scroll timer for a drag position, and records
    /// that position for the timer's own tick to resolve against once the pointer itself stops
    /// generating new move events.</summary>
    /// <param name="cells">The most recently reported root-relative drag position.</param>
    private void UpdateAutoScroll(Point cells)
    {
        _lastDragCells = cells;

        if (!ViewportBounds.Contains(cells))
        {
            _autoScrollTimer?.Start();
        }
        else
        {
            _autoScrollTimer?.Stop();
        }
    }

    private void OnAutoScrollTick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!_pointerSelecting)
        {
            _autoScrollTimer?.Stop();
            return;
        }

        AdvanceDragSelection(_lastDragCells);

        if (ViewportBounds.Contains(_lastDragCells))
        {
            _autoScrollTimer?.Stop();
        }
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
        var row = cells.Y - _content.Bounds.Y;

        if (row < 0 || row >= _visibleLines.Count)
        {
            return null;
        }

        var sourceLine = _visibleLines[row];
        Debug.Assert(
            sourceLine >= 0 && sourceLine < _lines.Length,
            "RebuildVisibleLines only ever appends indices it iterated over _lines with, so every entry is a valid source-line index.");
        var column = Math.Max(0, cells.X - _content.Bounds.X - GutterWidth + HorizontalOffset);
        return LineStartOffset(sourceLine) + Math.Min(column, _lines[sourceLine].Length);
    }

    #endregion

    #region Projection

    private void RebuildProjection()
    {
        // Code, Language, and Catalog reassignment can land mid-drag - the pointer stays captured
        // and selecting while a host live-reloads or streams new content into an already-mounted
        // view. _pointerAnchor is an absolute offset into the text that is about to be replaced;
        // if the replacement text is shorter, the very next pointer-move event would commit a
        // Selection built from that now out-of-range anchor. CommitSelection's own bounds check is
        // a Debug.Assert, compiled out in Release, so nothing else would catch this before
        // SelectedText's later Substring call throws from what looks like an unrelated read.
        // Canceling here - the same reset a real Release or focus-loss already performs - keeps a
        // reassignment from ever combining with a stale anchor in the first place.
        CancelPointerSelection();
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
        CommitSelection(new TextSelection(0, 0), resetDesiredColumn: true);
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
            _extentWidth = Math.Max(_extentWidth, GutterWidth + MeasureLineCells(_lines[line]));
        }
    }

    /// <summary>Measures one source line the same way <see cref="DrawSlice"/> actually draws it,
    /// rather than the way <see cref="ControlBase.MeasureCells"/> alone would measure its raw
    /// text.</summary>
    /// <remarks>
    /// A raw tab character classifies as <see cref="CellWidth.Control"/> and
    /// contributes zero to <see cref="ControlBase.MeasureCells"/>'s cell count, but <see
    /// cref="DrawSlice"/> substitutes one literal space - one drawn cell - for every tab before
    /// painting. Measuring the raw line would therefore undercount <see cref="_extentWidth"/> by
    /// exactly the tab count on any line that contains one, silently capping <see
    /// cref="RevealCaret"/>'s horizontal scroll short of content that is genuinely drawn past it.
    /// Substituting the same one-space-per-tab text this method measures against keeps the extent
    /// and the paint routine in agreement.
    /// </remarks>
    /// <param name="line">The raw source line, not yet tab-substituted.</param>
    /// <returns>The printable cell count this line actually draws as.</returns>
    [Pure]
    private int MeasureLineCells(string line)
    {
        if (!line.Contains('\t'))
        {
            return MeasureCells(line);
        }

        var buffer = line.Length <= 512 ? stackalloc char[line.Length] : new char[line.Length];

        for (var i = 0; i < line.Length; i++)
        {
            buffer[i] = line[i] == '\t' ? ' ' : line[i];
        }

        return MeasureCells(buffer);
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
