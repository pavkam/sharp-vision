// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

using SharpVision.Controls.Scrolling;
using SharpVision.Documents;
using SharpVision.Runtime;
using SharpVision.Terminal.Input;
using SharpVision.Text;

using LayoutStack = Layout.Stack;
using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Displays a scrollable tree of rich text content: headings, paragraphs with inline markup
/// and activatable links, lists, block quotes, code blocks, and thematic breaks.</summary>
/// <remarks>
/// <para>
/// A document is a semantic content tree. Most nodes are pure text structure; explicit
/// <see cref="DocumentInlineControl"/> and <see cref="DocumentBlockControl"/> nodes mount real retained
/// controls into the same projection for forms without turning every text node into a control.
/// </para>
/// <para>
/// Links use the document's compact focus model, while embedded controls keep their ordinary routed
/// input and focus behavior. Arrow keys, Page Up, Page Down, Home, End, and the wheel scroll the document.
/// </para>
/// <para>
/// One browser-like semantic selection spans ordinary text, links, and selectable text exposed by
/// embedded controls. Pointer dragging, Shift navigation, and Ctrl+A update that range;
/// <see cref="CopySelection"/> returns its normalized text without publishing clipboard state.
/// </para>
/// <para>
/// Presentation is resolved from <see cref="ActualStyle"/> during the paint pass rather than cached
/// onto nodes, so a live theme swap or a local <see cref="Style"/> assignment restyles every heading,
/// marker, bar, and link on the next frame.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class Document:
    CompositeControlBase,
    IStyled<DocumentStyle>,
    IClipboardCopySource,
    ISelectableTextViewport
{
    /// <summary>The width an unbounded measurement lays out against.</summary>
    /// <remarks>
    /// Large enough that no realistic content wraps, small enough that adding a cell width to it
    /// cannot overflow. An unbounded pass therefore reports each block's natural single-line width.
    /// </remarks>
    private const int _unboundedWidth = 1_000_000;

    private DocumentLayout _layout = new();
    private readonly DocumentPresenter _presenter;
    private readonly DocumentSelectionGesture _selectionGesture;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private readonly LayoutStack _stack;
    private readonly StyleSlot<DocumentStyle> _style;
    private readonly DocumentSurface _surface;

    private Ambiguous _layoutAmbiguousWidth;
    private DocumentLink? _activeLink;
    private DocumentGlyphs _layoutGlyphs;
    private bool _layoutValid;
    private int _layoutWidth = -1;
    private Selection _selection;
    private bool _selectionCaretEstablished;
    private int? _selectionDesiredColumn;
    private int? _selectionDesiredRow;
    private Rect? _selectionCaretGeometryAffinity;
    private TextSelectionMap? _selectionCaretGeometryAffinityMap;
    private int _selectionCaretGeometryAffinityOffset;
    private TextSelectionMap _selectionSemanticMap = TextSelectionMap.Empty;

    /// <summary>Initializes an empty scrollable document.</summary>
    public Document()
    {
        Blocks = new DocumentBlockCollection(this);

        // The surface exists before the style slot because the slot's change callback invalidates it,
        // and a slot can publish its first resolved value while it is still being initialized.
        _surface = new DocumentSurface(this);
        _presenter = new DocumentPresenter(this, _surface);
        _selectionGesture = new DocumentSelectionGesture(this);
        _style = InitializeStyle(DocumentStyle.Definition, OnStyleChanged);
        _stack = new LayoutStack
        {
            Orientation = Orientation.Vertical,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Children = { _presenter }
        };
        _stack.ScrollChanged += OnStackScrollChanged;
        FocusEntered += OnDocumentFocusBoundaryChanged;
        FocusLeft += OnDocumentFocusBoundaryChanged;
        _ = AddHandler(Events.Key, OnKeyRouted, handledEventsToo: true);
        _ = AddHandler(Events.Pointer, OnPointerRouted, handledEventsToo: true);
        _ = AddHandler(Events.TerminalFocusChanged, OnTerminalFocusRouted, handledEventsToo: true);
        InitializeContent(_stack);

        _scrollBarStyle = InitializePartStyle(ScrollBarStyle.ForwardingDefinition, nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _stack, nameof(ScrollBarStyle));

        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsTextSelectionEnabled = true;
    }

    #region Content

    /// <summary>Gets the owned ordered block content.</summary>
    public DocumentBlockCollection Blocks { get; }

    /// <summary>Reads serialized content and replaces the current block tree after parsing succeeds.</summary>
    /// <param name="source">The non-null serialized source.</param>
    /// <param name="reader">The non-null format reader.</param>
    /// <param name="options">Optional general read limits.</param>
    /// <returns>The applied read result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException">The reader result is no longer a detached tree.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The source exceeds an enabled reader limit.</exception>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public DocumentReadResult Load(
        string source,
        IDocumentFormatReader reader,
        DocumentReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reader);
        VerifyMutable();
        var result = reader.Read(source, options);
        ArgumentNullException.ThrowIfNull(result);

        foreach (var block in result.Blocks)
        {
            if (block.IsAttached)
            {
                throw new ArgumentException(
                    "A reader result can be loaded only while every block remains detached.",
                    nameof(reader));
            }

            DocumentEmbeddedControlCollector.ValidateInsertion(block, ownerNode: null, ownerDocument: null);
        }

        Blocks.Clear();

        foreach (var block in result.Blocks)
        {
            Blocks.Add(block);
        }

        return result;
    }

    /// <summary>Reads a bounded text stream asynchronously, leaves it open, and replaces the current tree.</summary>
    /// <param name="source">The non-null readable stream.</param>
    /// <param name="reader">The non-null format reader.</param>
    /// <param name="options">Optional general read limits.</param>
    /// <param name="encoding">The optional source encoding; UTF-8 is used when null.</param>
    /// <param name="cancellationToken">Cancels asynchronous stream reads before mutation.</param>
    /// <returns>The applied result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Decoded content exceeds the configured limit.</exception>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document or source stream is disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public async ValueTask<DocumentReadResult> LoadAsync(
        Stream source,
        IDocumentFormatReader reader,
        DocumentReadOptions? options = null,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reader);

        if (!source.CanRead)
        {
            throw new ArgumentException("The document source stream must be readable.", nameof(source));
        }

        options ??= new DocumentReadOptions();
        encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        using var textReader = new StreamReader(
            source,
            encoding,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);
        var builder = new StringBuilder(Math.Min(options.MaximumCharacters, 4096));
        var buffer = ArrayPool<char>.Shared.Rent(Math.Min(options.MaximumCharacters, 4096));

        try
        {
            while (true)
            {
                var count = await textReader.ReadAsync(buffer.AsMemory(), cancellationToken);

                if (count == 0)
                {
                    break;
                }

                if ((long) builder.Length + count > options.MaximumCharacters)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(source),
                        builder.Length + count,
                        "The document exceeds the configured maximum character count.");
                }

                _ = builder.Append(buffer, 0, count);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer, clearArray: true);
        }

        return Load(builder.ToString(), reader, options);
    }

    /// <summary>Invalidates the projected content after the node tree structurally changes.</summary>
    internal void InvalidateContent()
    {
        _layoutValid = false;
        _presenter.ReconcileControls();
        InvalidateRetainedDescendant(_surface, InvalidationImpact.Measure);
    }

    /// <summary>Verifies that semantic content may be mutated on the current thread.</summary>
    internal void VerifyContentMutable() => VerifyMutable();

    /// <summary>Clears selection immediately when its selected semantic link becomes unavailable.</summary>
    /// <param name="link">The link whose availability changed.</param>
    internal void OnLinkAvailabilityChanged(DocumentLink link)
    {
        Debug.Assert(link is not null, "A link availability notification identifies its link.");

        if (link.IsEnabled || !ReferenceEquals(link, _activeLink))
        {
            return;
        }

        _activeLink = null;
        ActiveLinkIndex = -1;
        InvalidateActiveLinkAppearance();
    }

    /// <summary>Resolves one document style color through a possibly absent theme.</summary>
    internal static Color ResolveDocumentColor(ControlColor value, Theme? theme) => ResolveColor(value, theme);

    /// <summary>Gets projected embedded-control rectangles for the private presenter.</summary>
    internal IReadOnlyList<DocumentControlPlacement> ControlPlacements => _layout.ControlPlacements;

    /// <summary>Refreshes embedded selection geometry after the private presenter arranges children.</summary>
    /// <param name="contentOrigin">The absolute origin of document content coordinates.</param>
    internal void RefreshSelectionGeometry(Point contentOrigin) =>
        RefreshSelectionGeometryCore(contentOrigin);

    private void RefreshSelectionGeometryCore(Point contentOrigin)
    {
        if (_layout.RefreshSelectionGeometry(contentOrigin))
        {
            AdoptSelectionMap(_layout.SelectionMap);
            return;
        }

        // A source may commit new text as part of its own ArrangeOverride without raising another
        // invalidation. Rebuild once against the now-arranged subtree so the semantic transaction,
        // source versions, and final geometry all commit before the outer arrange returns.
        var layout = new DocumentLayout();
        layout.Build(Blocks, _layoutWidth, CellPolicy.AmbiguousWidth, ActualStyle.Glyphs);
        _ = layout.RefreshSelectionGeometry(contentOrigin);
        CommitSelectionProjection(layout);
        AdoptSelectionMap(layout.SelectionMap);
    }

    /// <summary>Gets the current semantic selection map for behavioral invariant tests.</summary>
    /// <remarks>
    /// This internal seam proves that logical text, grapheme geometry, source identity, and row hit
    /// testing are rebuilt together without prematurely exposing Document's public selection API.
    /// </remarks>
    internal TextSelectionMap SelectionMap => _layout.SelectionMap;

    /// <summary>Gets the pointer-selection phase for mounted cleanup invariant tests.</summary>
    /// <remarks>
    /// This internal seam proves handled releases and unavailability cannot strand capture-backed
    /// gesture state. It does not expose selection arbitration as public control state.
    /// </remarks>
    internal DocumentSelectionGesturePhase SelectionGesturePhase => _selectionGesture.Phase;

    /// <summary>Gets whether keyboard navigation currently retains exact visual-boundary geometry.</summary>
    /// <remarks>This internal seam proves geometry affinity is discarded across projection identity changes.</remarks>
    internal bool HasSelectionCaretGeometryAffinity => _selectionCaretGeometryAffinity is not null;

    #endregion

    #region Selection

    /// <summary>Creates an authoritative snapshot of the complete normalized semantic stream and
    /// the currently visible document glyphs.</summary>
    /// <returns>
    /// An independently owned snapshot whose rectangles use document-local terminal-cell
    /// coordinates. Semantic separators and clipped graphemes remain in its text without geometry.
    /// </returns>
    /// <remarks>
    /// The projection is independent of this document's local selection. Reading an invalidated
    /// projection may synchronously rebuild semantic layout and clear a selection made stale by
    /// content mutation; it does not otherwise change selection state.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached document is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public override SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();
        var map = EnsureSelectionProjection();
        var clip = GetDescendantSelectableTextInheritedClip(_surface);
        var contentBounds = _surface.ContentBounds;
        var glyphs = new List<SelectableTextGlyph>(map.Glyphs.Count);

        foreach (var glyph in map.Glyphs)
        {
            var absolute = new Rect(
                AddCoordinates(contentBounds.X, glyph.Bounds.X),
                AddCoordinates(contentBounds.Y, glyph.Bounds.Y),
                glyph.Bounds.Width,
                glyph.Bounds.Height);

            if (!ContainsCompleteSelectionGlyph(clip, absolute))
            {
                continue;
            }

            glyphs.Add(new SelectableTextGlyph(
                glyph.Range,
                new Rect(
                    Difference(absolute.X, Bounds.X),
                    Difference(absolute.Y, Bounds.Y),
                    absolute.Width,
                    absolute.Height)));
        }

        return new SelectableTextSnapshot(map.Text, glyphs, isAuthoritative: true);
    }

    /// <inheritdoc/>
    public Rect SelectableTextViewport
    {
        get
        {
            VerifyMutable();
            var viewport = SelectionViewportBounds();
            return new Rect(
                Difference(viewport.X, Bounds.X),
                Difference(viewport.Y, Bounds.Y),
                viewport.Width,
                viewport.Height);
        }
    }

    /// <inheritdoc/>
    public bool RevealSelectableTextOffset(int offset)
    {
        VerifyMutable();
        var projection = PrepareSelectionProjection();
        Edit.Validate(projection.Map.Text, new Selection(offset, offset));
        CommitSelectionProjection(projection.Layout);
        AdoptSelectionMap(projection.Map);

        if (!projection.Map.TryGetCaretGeometry(offset, out var bounds, out _))
        {
            return false;
        }

        var target = VerticalOffset;

        if (bounds.Y < target)
        {
            target = bounds.Y;
        }
        else if (Viewport.Height > 0 && bounds.Y >= AddCoordinates(target, Viewport.Height))
        {
            target = bounds.Y - Viewport.Height + 1;
        }

        return Apply(target, ScrollCause.Programmatic);
    }

    /// <inheritdoc/>
    public bool ScrollSelectableTextViewport(int horizontal, int vertical)
    {
        VerifyMutable();
        return _stack.ScrollBy(horizontal, vertical, ScrollCause.Pointer);
    }

    [Pure]
    private static bool ContainsCompleteSelectionGlyph(Rect clip, Rect candidate) =>
        candidate.X >= clip.X && candidate.Y >= clip.Y &&
        (long) candidate.X + candidate.Width <= (long) clip.X + clip.Width &&
        (long) candidate.Y + candidate.Height <= (long) clip.Y + clip.Height;

    /// <summary>Gets the current directional selection over the normalized semantic document stream.</summary>
    /// <remarks>
    /// Endpoints are UTF-16 grapheme boundaries. Reading an invalidated document projection rebuilds
    /// semantic layout synchronously before returning, so content mutation cannot expose stale offsets.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached document is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public Selection Selection
    {
        get
        {
            VerifyMutable();
            _ = EnsureSelectionProjection();
            return _selection;
        }
        private set => _selection = value;
    }

    /// <inheritdoc/>
    public override Selection TextSelection => Selection;

    /// <summary>Gets an owned copy of the selected normalized semantic text, or an empty string.</summary>
    /// <exception cref="InvalidOperationException">The attached document is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public override string SelectedText
    {
        get
        {
            VerifyMutable();
            var map = EnsureSelectionProjection();
            return _selection.IsEmpty
                ? string.Empty
                : map.Text.Substring(_selection.Start, _selection.Length);
        }
    }

    /// <summary>Raised after the directional semantic selection commits to a different value.</summary>
    /// <remarks>The event runs synchronously on the owning dispatcher after state commits.</remarks>
    public event EventHandler? SelectionChanged;

    /// <summary>Replaces the selection with validated UTF-16 grapheme-boundary endpoints.</summary>
    /// <param name="selection">The proposed directional semantic selection.</param>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the semantic text length.</exception>
    /// <exception cref="ArgumentException">An endpoint splits an extended grapheme cluster.</exception>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public void SetSelection(Selection selection)
    {
        VerifyMutable();
        var projection = PrepareSelectionProjection();
        Edit.Validate(projection.Map.Text, selection);
        CommitSelectionProjection(projection.Layout);
        AdoptSelectionMap(projection.Map);
        ResetSelectionDesiredColumn();
        _selectionCaretEstablished = true;
        CommitSelection(selection);
    }

    /// <inheritdoc/>
    public override void SetTextSelection(Selection selection)
    {
        VerifyTextSelectionEnabled();
        SetSelection(selection);
    }

    /// <summary>Selects the complete normalized semantic document stream.</summary>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public void SelectAll()
    {
        VerifyMutable();
        var projection = PrepareSelectionProjection();
        CommitSelectionProjection(projection.Layout);
        AdoptSelectionMap(projection.Map);
        ResetSelectionDesiredColumn();
        _selectionCaretEstablished = true;
        CommitSelection(new Selection(0, projection.Map.Text.Length));
    }

    /// <inheritdoc/>
    public override void SelectAllText()
    {
        VerifyTextSelectionEnabled();
        SelectAll();
    }

    /// <summary>Collapses the selection to its current active caret endpoint.</summary>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public void ClearSelection()
    {
        VerifyMutable();
        _ = EnsureSelectionProjection();
        ResetSelectionDesiredColumn();
        _selectionCaretEstablished = true;
        CommitSelection(new Selection(_selection.Caret, _selection.Caret));
    }

    /// <inheritdoc/>
    public override void ClearTextSelection()
    {
        VerifyTextSelectionEnabled();
        ClearSelection();
    }

    /// <summary>Copies selected semantic text without publishing clipboard or terminal state.</summary>
    /// <returns>An independently owned string, or empty when the selection is collapsed.</returns>
    /// <exception cref="InvalidOperationException">The attached document is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    [Pure]
    public string CopySelection() => SelectedText;

    /// <inheritdoc/>
    [Pure]
    public override string CopySelectedText() => CopySelection();

    /// <inheritdoc/>
    protected override bool UsesTextSelectionController => false;

    /// <inheritdoc/>
    protected override void OnTextSelectionEnabledChanged(bool enabled)
    {
        if (!enabled && _selection != default)
        {
            ClearSelection();
        }
    }

    private TextSelectionMap EnsureSelectionProjection()
    {
        var projection = PrepareSelectionProjection();
        CommitSelectionProjection(projection.Layout);
        AdoptSelectionMap(projection.Map);

        return projection.Map;
    }

    private (DocumentLayout? Layout, TextSelectionMap Map) PrepareSelectionProjection()
    {
        if (_layoutValid && SelectionSourcesAreCurrent())
        {
            return (null, _layout.SelectionMap);
        }

        var layout = new DocumentLayout();
        layout.Build(
            Blocks,
            _layoutWidth >= 0 ? _layoutWidth : _unboundedWidth,
            CellPolicy.AmbiguousWidth,
            ActualStyle.Glyphs);
        return (layout, layout.SelectionMap);
    }

    private bool SelectionSourcesAreCurrent()
    {
        foreach (var source in _layout.SelectionMap.Sources)
        {
            if (source.Source is not ControlBase control ||
                control.IsDisposed ||
                source.Source.SelectableTextVersion != source.InvalidationVersion)
            {
                return false;
            }
        }

        return true;
    }

    private void CommitSelectionProjection(DocumentLayout? layout)
    {
        if (layout is null)
        {
            return;
        }

        var activeLink = ActiveLink;
        _layout = layout;
        _layoutValid = true;
        _layoutWidth = _layoutWidth >= 0 ? _layoutWidth : _unboundedWidth;
        _layoutGlyphs = ActualStyle.Glyphs;
        _layoutAmbiguousWidth = CellPolicy.AmbiguousWidth;
        RestoreActiveLink(activeLink);
    }

    private void AdoptSelectionMap(TextSelectionMap map)
    {
        Debug.Assert(map is not null, "A committed document layout always has a selection map.");

        if (_selectionSemanticMap.Fingerprint == map.Fingerprint &&
            SemanticallyEquals(_selectionSemanticMap, map))
        {
            _selectionSemanticMap = map;
            return;
        }

        _selectionSemanticMap = map;
        _selectionCaretEstablished = false;
        ResetSelectionDesiredColumn();

        if (_selection != default)
        {
            CommitSelection(default);
        }
    }

    private static bool SemanticallyEquals(TextSelectionMap previous, TextSelectionMap current)
    {
        if (!string.Equals(previous.Text, current.Text, StringComparison.Ordinal) ||
            previous.Sources.Count != current.Sources.Count)
        {
            return false;
        }

        for (var index = 0; index < previous.Sources.Count; index++)
        {
            var left = previous.Sources[index];
            var right = current.Sources[index];

            if (!ReferenceEquals(left.Source, right.Source) ||
                left.Range != right.Range ||
                !string.Equals(left.Text, right.Text, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void CommitSelection(Selection selection)
    {
        if (_selection == selection)
        {
            return;
        }

        var previous = _selection;
        _selection = selection;
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        PublishTextSelectionChanged(previous, selection);
    }

    #endregion

    #region Style

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public DocumentStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public DocumentStyle ActualStyle => _style.Actual;

    /// <summary>Gets or sets the complete local generated-bar style, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _scrollBarStyle.Local;
        set => _scrollBarStyle.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned resolved generated-bar style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => _scrollBarStyle.Actual;

    // Faces resolve during the paint pass, so a face replacement needs nothing but the repaint the
    // style slot already schedules. A glyph replacement is different: it can change how many cells a
    // marker or bar occupies, which moves the text beside it, and the projection caches its glyph
    // family behind the private surface's measure. Invalidating only this control's measure leaves
    // that surface's cached desired size in place, its MeasureOverride unrun, and the previous
    // glyphs on screen - so the document has to invalidate the projection itself.
    private void OnStyleChanged(DocumentStyle previous, DocumentStyle current)
    {
        if (previous.Glyphs != current.Glyphs)
        {
            InvalidateContent();
            return;
        }

        InvalidateRetainedDescendant(_surface, InvalidationImpact.Render);
    }

    /// <summary>Calculates the document owner's theme impact for its projected render surface.</summary>
    internal InvalidationImpact GetProjectedThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        GetThemeChangeImpact(
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace);

    #endregion

    #region Scrolling

    /// <summary>Raised after the vertical offset commits.</summary>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    /// <summary>Gets the committed non-negative content extent.</summary>
    public Size Extent => _stack.Extent;

    /// <summary>Gets the committed non-negative visible extent.</summary>
    public Size Viewport => _stack.Viewport;

    /// <summary>Gets or sets the valid vertical content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public int VerticalOffset
    {
        get => _stack.VerticalOffset;
        set => _stack.VerticalOffset = value;
    }

    /// <summary>Gets or sets the non-negative number of lines one arrow key or wheel notch scrolls.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    [NonNegativeValue]
    public int LineSize
    {
        get => _stack.LineSize;
        set => _stack.LineSize = value;
    }

    /// <summary>Gets or sets the non-negative number of lines a page scroll keeps in view.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    [NonNegativeValue]
    public int PageOverlap
    {
        get => _stack.PageOverlap;
        set => _stack.PageOverlap = value;
    }

    /// <summary>Gets or sets when the generated vertical scrollbar is shown.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => _stack.ShowScrollBars;
        set => _stack.ShowScrollBars = value;
    }

    /// <summary>Adds a signed line delta with saturation and endpoint clamping.</summary>
    /// <param name="lines">The requested signed line delta.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when the offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached document is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public bool ScrollBy(int lines, ScrollCause cause = ScrollCause.Programmatic) =>
        _stack.ScrollBy(0, lines, cause);

    /// <summary>Scrolls to the first line.</summary>
    /// <returns>True when the offset changed.</returns>
    /// <exception cref="InvalidOperationException">The attached document is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public bool ScrollToTop() => Apply(0, ScrollCause.Programmatic);

    /// <summary>Scrolls to the last line.</summary>
    /// <returns>True when the offset changed.</returns>
    /// <exception cref="InvalidOperationException">The attached document is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public bool ScrollToEnd() => Apply(MaximumOffset, ScrollCause.Programmatic);

    private int MaximumOffset => Math.Max(0, Extent.Height - Viewport.Height);

    private bool Apply(int offset, ScrollCause cause)
    {
        var target = Math.Clamp(offset, 0, MaximumOffset);
        return target != VerticalOffset && _stack.ScrollBy(0, target - VerticalOffset, cause);
    }

    private void OnStackScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        _ = sender;
        ScrollChanged?.Invoke(this, eventArgs);
    }

    #endregion

    #region Links

    /// <summary>Raised after any link is activated, following that link's own
    /// <see cref="DocumentLink.Clicked"/> event.</summary>
    public event EventHandler<DocumentLinkEventArgs>? LinkClicked;

    /// <summary>Gets or sets the focused link, or null when no link is focused.</summary>
    /// <remarks>
    /// Setting a link that is not in this document, or is disabled, clears the selection instead.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public DocumentLink? ActiveLink
    {
        get => _activeLink is { IsEnabled: true } ? _activeLink : null;
        set
        {
            VerifyMutable();
            var index = -1;

            if (value is { IsEnabled: true } && ReferenceEquals(value.OwnerDocument, this))
            {
                for (var candidate = 0; candidate < _layout.Links.Count; candidate++)
                {
                    if (ReferenceEquals(_layout.Links[candidate], value))
                    {
                        index = candidate;
                        break;
                    }
                }
            }

            var selected = value is { IsEnabled: true } &&
                           ReferenceEquals(value.OwnerDocument, this) &&
                           (!_layoutValid || index >= 0)
                ? value
                : null;

            if (ReferenceEquals(selected, _activeLink) && index == ActiveLinkIndex)
            {
                return;
            }

            _activeLink = selected;
            ActiveLinkIndex = index;
            RevealActiveLink();
            InvalidateActiveLinkAppearance();
        }
    }

    /// <summary>Gets the focused link's index in document order, or -1 when none is focused.</summary>
    /// <remarks>
    /// Exposed to prove link navigation, reveal, and clamping without reaching into the projection.
    /// </remarks>
    internal int ActiveLinkIndex { get; private set; } = -1;

    private bool MoveInteractiveFocus(ControlBase? source, bool forward)
    {
        var candidates = new List<(int Line, int Column, int Sequence, object Item)>();
        var sequence = 0;

        for (var linkIndex = 0; linkIndex < _layout.Links.Count; linkIndex++)
        {
            var link = _layout.Links[linkIndex];

            if (!link.IsEnabled)
            {
                continue;
            }

            foreach (var region in _layout.LinkRegions)
            {
                if (region.LinkIndex != linkIndex)
                {
                    continue;
                }

                candidates.Add((region.Line, region.Column, sequence++, link));
                break;
            }
        }

        foreach (var placement in _layout.ControlPlacements)
        {
            if (placement.Control.CanTabStop)
            {
                candidates.Add((
                    placement.Bounds.Y,
                    placement.Bounds.X,
                    sequence++,
                    placement.Control));
            }
        }

        candidates.Sort(static (left, right) =>
        {
            var line = left.Line.CompareTo(right.Line);

            if (line != 0)
            {
                return line;
            }

            var column = left.Column.CompareTo(right.Column);
            return column != 0 ? column : left.Sequence.CompareTo(right.Sequence);
        });

        object? current = IsFocused ? ActiveLink : null;

        if (current is null && source is not null && !ReferenceEquals(source, this))
        {
            current = candidates
                .Select(static candidate => candidate.Item)
                .OfType<ControlBase>()
                .FirstOrDefault(control => ContainsSource(control, source));
        }

        var index = current is null
            ? forward ? -1 : candidates.Count
            : candidates.FindIndex(candidate => ReferenceEquals(candidate.Item, current));

        while (true)
        {
            index += forward ? 1 : -1;

            if (index < 0 || index >= candidates.Count)
            {
                return false;
            }

            if (candidates[index].Item is DocumentLink link)
            {
                if (!IsFocused && !Focus())
                {
                    continue;
                }

                ActiveLink = link;
            }
            else if (candidates[index].Item is ControlBase control && !control.Focus())
            {
                continue;
            }

            InvalidateActiveLinkAppearance();
            return true;
        }
    }

    private static bool ContainsSource(ControlBase control, ControlBase source)
    {
        for (var candidate = (ControlBase?) source; candidate is not null; candidate = candidate.Parent)
        {
            if (ReferenceEquals(candidate, control))
            {
                return true;
            }
        }

        return false;
    }

    private bool ActivateLink()
    {
        if (ActiveLink is not { IsEnabled: true } link)
        {
            return false;
        }

        link.Activate();
        LinkClicked?.Invoke(this, new DocumentLinkEventArgs(link));
        return true;
    }

    private void RestoreActiveLink(DocumentLink? activeLink)
    {
        _activeLink = null;
        ActiveLinkIndex = -1;

        if (activeLink is not { IsEnabled: true })
        {
            return;
        }

        for (var index = 0; index < _layout.Links.Count; index++)
        {
            if (ReferenceEquals(_layout.Links[index], activeLink))
            {
                _activeLink = activeLink;
                ActiveLinkIndex = index;
                return;
            }
        }
    }

    private void RevealActiveLink()
    {
        if (ActiveLinkIndex < 0)
        {
            return;
        }

        foreach (var region in _layout.LinkRegions)
        {
            if (region.LinkIndex != ActiveLinkIndex)
            {
                continue;
            }

            var viewport = Viewport.Height;

            if (viewport <= 0)
            {
                return;
            }

            if (region.Line < VerticalOffset)
            {
                _ = Apply(region.Line, ScrollCause.Programmatic);
            }
            else if (region.Line >= VerticalOffset + viewport)
            {
                _ = Apply(region.Line - viewport + 1, ScrollCause.Programmatic);
            }

            return;
        }
    }

    // The active link's highlight is painted by the private render surface several levels below
    // this control in the retained tree, not by this control itself. A render-only Invalidate()
    // here would mark only this control's own dirty bit; the surface's bit would stay clean, and
    // the renderer's clean-subtree fast path would keep copying its previous frame's cells
    // forever, leaving the highlight stuck until an unrelated layout pass elsewhere happened to
    // force a full repaint. Invalidating the surface directly is what actually schedules its next
    // paint.
    private void InvalidateActiveLinkAppearance() =>
        InvalidateRetainedDescendant(_surface, InvalidationImpact.Render);

    private void OnDocumentFocusBoundaryChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        InvalidateActiveLinkAppearance();
    }

    #endregion

    #region Input

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.IsHandled ||
            eventArgs.Phase != RoutingPhase.Preview ||
            eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat) ||
            eventArgs.Stroke.Code != Code.Tab ||
            (eventArgs.Stroke.Modifiers & ~Modifiers.Shift) != 0 ||
            !ContainsFocus)
        {
            return;
        }

        eventArgs.IsHandled = MoveInteractiveFocus(
            eventArgs.OriginalSource,
            (eventArgs.Stroke.Modifiers & Modifiers.Shift) == 0);
    }

    private void OnPointerRouted(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Preview)
        {
            return;
        }

        if (eventArgs.IsHandled)
        {
            _selectionGesture.HandleHandledPreview(eventArgs);
            return;
        }

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            _selectionGesture.HandlePreview(eventArgs);
        }
    }

    private void OnTerminalFocusRouted(object? sender, TerminalFocusEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase == RoutingPhase.Preview && !eventArgs.Focus.Gained)
        {
            _selectionGesture.Cancel(releaseCapture: false);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            switch (eventArgs)
            {
                case KeyEventArgs key:
                    HandleKey(key);
                    break;
                case PointerEventArgs pointer:
                    HandlePointer(pointer);
                    break;
                default:
                    break;
            }
        }

        if (!eventArgs.IsHandled)
        {
            base.OnEvent(eventArgs);
        }
    }

    private void HandleKey(KeyEventArgs eventArgs)
    {
        if (eventArgs.IsHandled)
        {
            return;
        }

        var stroke = eventArgs.Stroke;

        if (stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        if (IsFocused && TryHandleSelectionKey(stroke))
        {
            eventArgs.IsHandled = true;
            return;
        }

        if (IsFocused &&
            (stroke.Code == Code.Enter ||
             (stroke.Code == Code.Character && stroke.Character == new Rune(' '))))
        {
            eventArgs.IsHandled = stroke.Modifiers.IsActivationEligible() && ActivateLink();
            return;
        }

        var page = Math.Max(1, Viewport.Height - PageOverlap);
        var handled = false;

        if (stroke.Code == Code.Up)
        {
            handled = Scroll(-LineSize);
        }
        else if (stroke.Code == Code.Down)
        {
            handled = Scroll(LineSize);
        }
        else if (stroke.Code == Code.PageUp)
        {
            handled = Scroll(-page);
        }
        else if (stroke.Code == Code.PageDown)
        {
            handled = Scroll(page);
        }
        else if (stroke.Code == Code.Home)
        {
            handled = Endpoint(0);
        }
        else if (stroke.Code == Code.End)
        {
            handled = Endpoint(MaximumOffset);
        }

        if (handled)
        {
            eventArgs.IsHandled = true;
        }
    }

    private bool TryHandleSelectionKey(Stroke stroke)
    {
        var modifiers = stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock);

        if (stroke.Action == KeyAction.Press &&
            stroke.Code == Code.Character &&
            stroke.Character is { } character &&
            Rune.ToLowerInvariant(character) == new Rune('a') &&
            modifiers == Modifiers.Control)
        {
            SelectAll();
            RevealSelectionCaret();
            return true;
        }

        if (!_selectionCaretEstablished || modifiers != Modifiers.Shift)
        {
            return false;
        }

        var map = EnsureSelectionProjection();
        var caret = _selection.Caret;
        int target;

        if (stroke.Code == Code.Left)
        {
            target = map.PreviousBoundary(caret);
            ResetSelectionDesiredColumn();
        }
        else if (stroke.Code == Code.Right)
        {
            target = map.NextBoundary(caret);
            ResetSelectionDesiredColumn();
        }
        else if (stroke.Code == Code.Up)
        {
            target = MoveSelectionVertically(map, caret, -1);
        }
        else if (stroke.Code == Code.Down)
        {
            target = MoveSelectionVertically(map, caret, 1);
        }
        else if (stroke.Code == Code.Home)
        {
            ResetSelectionDesiredColumn();
            _ = map.TryGetVisualLineBoundary(caret, end: false, out target, out var bounds, out _);
            SetSelectionCaretGeometryAffinity(map, target, bounds);
        }
        else if (stroke.Code == Code.End)
        {
            ResetSelectionDesiredColumn();
            _ = map.TryGetVisualLineBoundary(caret, end: true, out target, out var bounds, out _);
            SetSelectionCaretGeometryAffinity(map, target, bounds);
        }
        else if (stroke.Code == Code.PageUp)
        {
            target = MoveSelectionVertically(map, caret, -Math.Max(1, Viewport.Height - PageOverlap));
        }
        else if (stroke.Code == Code.PageDown)
        {
            target = MoveSelectionVertically(map, caret, Math.Max(1, Viewport.Height - PageOverlap));
        }
        else
        {
            return false;
        }

        var fingerprint = map.Fingerprint;
        var next = new Selection(_selection.Anchor, target);
        CommitSelection(next);

        // SelectionChanged and every scrolling callback are synchronous extension points. Never
        // continue revealing geometry when either callback replaced the projection or selection.
        if (CanContinueKeyboardReveal() &&
            _selection == next && EnsureSelectionProjection().Fingerprint == fingerprint)
        {
            RevealSelectionCaret();
        }

        return true;
    }

    private int MoveSelectionVertically(TextSelectionMap map, int caret, int rows)
    {
        if (map.VisualRowCount == 0)
        {
            return caret;
        }

        if (!_selectionDesiredRow.HasValue)
        {
            if (!map.TryGetVisualPosition(caret, out var row, out var column))
            {
                return caret;
            }

            _selectionDesiredRow = row;
            _selectionDesiredColumn = column;
        }

        var targetRow = (int) Math.Clamp((long) _selectionDesiredRow.Value + rows, 0, map.VisualRowCount - 1);
        _selectionDesiredRow = targetRow;
        return map.OffsetAtVisualColumn(targetRow, _selectionDesiredColumn.GetValueOrDefault());
    }

    private void RevealSelectionCaret()
    {
        if (!CanContinueKeyboardReveal())
        {
            return;
        }

        var expectedSelection = _selection;
        var map = EnsureSelectionProjection();
        DiscardStaleSelectionCaretGeometryAffinity(map);
        var expectedFingerprint = map.Fingerprint;
        var caret = expectedSelection.Caret;

        _ = map.TryGetCaretGeometry(caret, out _, out var source);

        if (source is { Viewport: { } viewport } && IsSelectionSourceEligible(source))
        {
            var localOffset = Math.Clamp(caret - source.Range.Start, 0, source.Text.Length);
            _ = viewport.RevealSelectableTextOffset(localOffset);

            if (!TryContinueKeyboardReveal(expectedSelection, expectedFingerprint, out map))
            {
                return;
            }
        }

        Rect caretBounds;
        if (_selectionCaretGeometryAffinity is { } affinity &&
            _selectionCaretGeometryAffinityOffset == caret &&
            ReferenceEquals(_selectionCaretGeometryAffinityMap, map))
        {
            caretBounds = affinity;
        }
        else if (!map.TryGetCaretGeometry(caret, out caretBounds, out _))
        {
            return;
        }

        var previousOffset = VerticalOffset;
        var row = caretBounds.Y;

        if (row < VerticalOffset)
        {
            _ = Apply(row, ScrollCause.Keyboard);
        }
        else if (Viewport.Height > 0 && row >= AddCoordinates(VerticalOffset, Viewport.Height))
        {
            _ = Apply(row - Viewport.Height + 1, ScrollCause.Keyboard);
        }

        if (!TryContinueKeyboardReveal(expectedSelection, expectedFingerprint, out map))
        {
            return;
        }

        if (_selectionCaretGeometryAffinity is { } refreshedAffinity &&
            _selectionCaretGeometryAffinityOffset == caret &&
            ReferenceEquals(_selectionCaretGeometryAffinityMap, map))
        {
            caretBounds = refreshedAffinity;
        }
        else if (!map.TryGetCaretGeometry(caret, out caretBounds, out _))
        {
            return;
        }

        var screenBounds = new Rect(
            AddCoordinates(_surface.ContentBounds.X, caretBounds.X),
            AddCoordinates(AddCoordinates(_surface.ContentBounds.Y, caretBounds.Y), previousOffset - VerticalOffset),
            caretBounds.Width,
            caretBounds.Height);
        RevealSelectionCaretThroughAncestors(screenBounds, expectedSelection, expectedFingerprint);
    }

    private bool TryContinueKeyboardReveal(
        Selection expectedSelection,
        ulong expectedFingerprint,
        out TextSelectionMap map)
    {
        map = EnsureSelectionProjection();
        DiscardStaleSelectionCaretGeometryAffinity(map);
        return CanContinueKeyboardReveal() &&
               _selection == expectedSelection &&
               map.Fingerprint == expectedFingerprint;
    }

    private bool CanContinueKeyboardReveal() =>
        IsFocused && !IsDisposed && EffectiveIsEnabled && EffectiveIsVisible;

    private void RevealSelectionCaretThroughAncestors(
        Rect screenBounds,
        Selection expectedSelection,
        ulong expectedFingerprint)
    {
        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (!AllowsModalAncestor(current))
            {
                break;
            }

            if (current is not Container
                {
                    AutoScroll: true,
                    EffectiveIsEnabled: true,
                    EffectiveIsVisible: true
                } container)
            {
                continue;
            }

            var viewport = new Rect(
                container.ContentBounds.X,
                container.ContentBounds.Y,
                container.Viewport.Width,
                container.Viewport.Height);
            var horizontal = RevealDelta(screenBounds.X, screenBounds.Width, viewport.X, viewport.Width);
            var vertical = RevealDelta(screenBounds.Y, screenBounds.Height, viewport.Y, viewport.Height);
            horizontal = (container.ScrollBars & ScrollBars.Horizontal) != 0 ? horizontal : 0;
            vertical = (container.ScrollBars & ScrollBars.Vertical) != 0 ? vertical : 0;

            if (horizontal == 0 && vertical == 0)
            {
                continue;
            }

            var previousHorizontal = container.HorizontalOffset;
            var previousVertical = container.VerticalOffset;
            _ = container.ScrollBy(horizontal, vertical, ScrollCause.Keyboard);
            screenBounds = new Rect(
                AddCoordinates(screenBounds.X, previousHorizontal - container.HorizontalOffset),
                AddCoordinates(screenBounds.Y, previousVertical - container.VerticalOffset),
                screenBounds.Width,
                screenBounds.Height);

            if (!TryContinueKeyboardReveal(expectedSelection, expectedFingerprint, out _))
            {
                return;
            }
        }
    }

    [Pure]
    private static int RevealDelta(int start, int length, int viewportStart, int viewportLength)
    {
        if (viewportLength <= 0 || start < viewportStart)
        {
            return start - viewportStart;
        }

        var end = (long) start + length;
        var viewportEnd = (long) viewportStart + viewportLength;
        return end > viewportEnd ? (int) Math.Clamp(end - viewportEnd, int.MinValue, int.MaxValue) : 0;
    }

    private void ResetSelectionDesiredColumn()
    {
        _selectionDesiredColumn = null;
        _selectionDesiredRow = null;
        _selectionCaretGeometryAffinity = null;
        _selectionCaretGeometryAffinityMap = null;
    }

    private void SetSelectionCaretGeometryAffinity(TextSelectionMap map, int offset, Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _selectionCaretGeometryAffinity = bounds;
        _selectionCaretGeometryAffinityMap = map;
        _selectionCaretGeometryAffinityOffset = offset;
    }

    private void DiscardStaleSelectionCaretGeometryAffinity(TextSelectionMap map)
    {
        if (_selectionCaretGeometryAffinity is not null &&
            !ReferenceEquals(_selectionCaretGeometryAffinityMap, map))
        {
            _selectionCaretGeometryAffinity = null;
            _selectionCaretGeometryAffinityMap = null;
        }
    }

    // Reported handled whenever the document has anything to scroll, even when already at the
    // boundary, so the keystroke cannot escape and page an enclosing scrollable container out from
    // under the still-focused document.
    private bool Scroll(int lines)
    {
        if (MaximumOffset <= 0)
        {
            return false;
        }

        _ = _stack.ScrollBy(0, lines, ScrollCause.Keyboard);
        return true;
    }

    private bool Endpoint(int offset)
    {
        if (MaximumOffset <= 0)
        {
            return false;
        }

        _ = Apply(offset, ScrollCause.Keyboard);
        return true;
    }

    private void HandlePointer(PointerEventArgs eventArgs)
    {
        if (eventArgs.Pointer.Action != PointerAction.Release ||
            _selectionGesture.TakeReleasedLink(eventArgs) is not { IsEnabled: true } link)
        {
            return;
        }

        for (var index = 0; index < _layout.Links.Count; index++)
        {
            if (!ReferenceEquals(_layout.Links[index], link))
            {
                continue;
            }

            ActiveLinkIndex = index;
            _activeLink = link;
            _ = Focus();
            InvalidateActiveLinkAppearance();
            _ = ActivateLink();
            eventArgs.IsHandled = true;
            return;
        }
    }

    /// <summary>Gets whether one routed source belongs to the projected document content.</summary>
    /// <param name="source">The original routed pointer source.</param>
    /// <returns>True for the private surface or an embedded retained descendant.</returns>
    internal bool IsSelectionContentSource(ControlBase? source)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, this); current = current.Parent)
        {
            if (ReferenceEquals(current, _presenter))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Hit-tests one screen cell against the current semantic selection map.</summary>
    /// <param name="cells">The pointer's screen-cell coordinate.</param>
    /// <returns>A grapheme-aligned semantic endpoint.</returns>
    internal int HitTestSelection(Point cells)
    {
        var map = EnsureSelectionProjection();
        var bounds = _surface.ContentBounds;
        return map.HitTest(new Point(Difference(cells.X, bounds.X), Difference(cells.Y, bounds.Y)));
    }

    /// <summary>Gets the semantic and ordered-source identity of the current selection projection.</summary>
    internal ulong SelectionFingerprint => EnsureSelectionProjection().Fingerprint;

    /// <summary>Hit-tests a drag coordinate clamped to the currently visible document viewport.</summary>
    /// <param name="cells">The retained screen-cell coordinate, which may lie outside the viewport.</param>
    /// <returns>A grapheme-aligned semantic endpoint at the visible edge.</returns>
    internal int HitTestSelectionForDrag(Point cells)
    {
        var map = EnsureSelectionProjection();
        var viewport = SelectionViewportBounds();
        var x = ClampToViewportAxis(cells.X, viewport.X, viewport.Width);
        var y = ClampToViewportAxis(cells.Y, viewport.Y, viewport.Height);
        return map.HitTest(new Point(
            Difference(x, viewport.X),
            AddCoordinates(Difference(y, viewport.Y), VerticalOffset)));
    }

    /// <summary>Resolves the embedded selection source containing an original routed descendant.</summary>
    /// <param name="originalSource">The original routed control.</param>
    /// <param name="cells">The pointer coordinate used as a geometry fallback.</param>
    /// <returns>The nearest embedded source, or null for document-owned text.</returns>
    internal TextSelectionSource? SelectionSourceFor(ControlBase? originalSource, Point cells)
    {
        var map = EnsureSelectionProjection();

        for (var current = originalSource; current is not null && !ReferenceEquals(current, this); current = current.Parent)
        {
            foreach (var source in map.Sources)
            {
                if (ReferenceEquals(source.Source, current) && IsSelectionSourceEligible(source))
                {
                    return source;
                }
            }
        }

        return SelectionSourceAt(cells);
    }

    /// <summary>Finds the innermost selectable viewport currently containing one pointer cell.</summary>
    /// <param name="cells">The pointer's screen-cell coordinate.</param>
    /// <returns>The matching source, or null.</returns>
    internal TextSelectionSource? SelectionSourceAt(Point cells)
    {
        var map = EnsureSelectionProjection();

        for (var index = map.Sources.Count - 1; index >= 0; index--)
        {
            var source = map.Sources[index];

            if (IsSelectionSourceEligible(source) &&
                TrySourceViewportBounds(source, out var bounds) &&
                bounds.Contains(cells))
            {
                return source;
            }
        }

        return null;
    }

    /// <summary>Reconciles one captured source occurrence against the current semantic projection.</summary>
    /// <param name="source">The previously associated source occurrence, or null.</param>
    /// <param name="cells">The retained pointer cell used when no prior occurrence remains.</param>
    /// <returns>The current eligible exact occurrence, a source under the pointer, or null.</returns>
    internal TextSelectionSource? ResolveSelectionSource(TextSelectionSource? source, Point cells)
    {
        if (source is null)
        {
            return SelectionSourceAt(cells);
        }

        var map = EnsureSelectionProjection();
        var candidate = map.ResolveSourceOccurrence(source);
        return candidate is not null && IsSelectionSourceEligible(candidate)
            ? candidate
            : SelectionSourceAt(cells);
    }

    /// <summary>Gets whether an active drag lies beyond an eligible nested, document, or ancestor viewport edge.</summary>
    /// <param name="cells">The retained pointer cell.</param>
    /// <param name="associatedSource">The nearest nested selectable source, or null.</param>
    /// <returns>True when a deterministic timer should remain armed.</returns>
    internal bool HasSelectionAutoScrollRequest(Point cells, TextSelectionSource? associatedSource) =>
        ResolveSelectionAutoScroll(cells, associatedSource, apply: false, out _);

    /// <summary>Offers one edge-scroll attempt from the innermost selectable viewport outward.</summary>
    /// <param name="cells">The retained pointer cell.</param>
    /// <param name="associatedSource">The nearest nested selectable source, or null.</param>
    /// <param name="hitAdjustment">Receives the cell translation needed before deferred ancestor arrangement.</param>
    /// <returns>True when one viewport offset changed.</returns>
    internal bool AutoScrollSelection(
        Point cells,
        TextSelectionSource? associatedSource,
        out Point hitAdjustment)
        => ResolveSelectionAutoScroll(cells, associatedSource, apply: true, out hitAdjustment);

    private bool ResolveSelectionAutoScroll(
        Point cells,
        TextSelectionSource? associatedSource,
        bool apply,
        out Point hitAdjustment)
    {
        hitAdjustment = default;
        var documentBounds = SelectionViewportBounds();
        var (_, vertical) = AutoScrollDelta(cells, documentBounds);
        var horizontal = 0;
        var hasPropagatedRequest = false;

        if (associatedSource is { Viewport: { } sourceViewport } &&
            TrySourceViewportBounds(associatedSource, out var sourceBounds))
        {
            (horizontal, vertical) = AutoScrollDelta(cells, sourceBounds);
            hasPropagatedRequest = horizontal != 0 || vertical != 0;

            if (hasPropagatedRequest && (!apply || sourceViewport.ScrollSelectableTextViewport(horizontal, vertical)))
            {
                return true;
            }
        }

        if (vertical != 0)
        {
            if (!apply)
            {
                return true;
            }

            var previousVertical = VerticalOffset;

            if (ScrollBy(vertical, ScrollCause.Pointer))
            {
                // The stack commits its offset before the translated surface is arranged. Apply
                // the logical delta to this tick's pointer hit so upward and downward movement
                // both observe the newly exposed edge immediately.
                hitAdjustment = new Point(0, Difference(VerticalOffset, previousVertical));
                return true;
            }

            hasPropagatedRequest = true;
        }

        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (!AllowsModalAncestor(current))
            {
                break;
            }

            if (current is not Container
                {
                    AutoScroll: true,
                    EffectiveIsEnabled: true,
                    EffectiveIsVisible: true
                } container)
            {
                continue;
            }

            if (!hasPropagatedRequest)
            {
                (horizontal, vertical) = AutoScrollDelta(
                    cells,
                    new Rect(container.ContentBounds.X, container.ContentBounds.Y,
                        container.Viewport.Width, container.Viewport.Height));
            }

            horizontal = (container.ScrollBars & ScrollBars.Horizontal) != 0 ? horizontal : 0;
            vertical = (container.ScrollBars & ScrollBars.Vertical) != 0 ? vertical : 0;

            if (horizontal == 0 && vertical == 0)
            {
                continue;
            }

            hasPropagatedRequest = true;

            if (!apply)
            {
                return true;
            }

            var previousHorizontal = container.HorizontalOffset;
            var previousVertical = container.VerticalOffset;

            if (container.ScrollBy(horizontal, vertical, ScrollCause.Pointer))
            {
                // Container offsets commit before translated descendant arrangement. Adjust this
                // tick's retained screen coordinate by the committed logical delta so the caret
                // observes the newly exposed cells immediately rather than one period later.
                hitAdjustment = new Point(
                    Difference(container.HorizontalOffset, previousHorizontal),
                    Difference(container.VerticalOffset, previousVertical));
                return true;
            }
        }

        return hasPropagatedRequest && !apply;
    }

    private Rect SelectionViewportBounds()
    {
        var content = _surface.ContentBounds;
        return new Rect(
            content.X,
            AddCoordinates(content.Y, VerticalOffset),
            Viewport.Width,
            Viewport.Height);
    }

    private bool IsSelectionSourceEligible(TextSelectionSource source) =>
        source.Viewport is not null &&
        source.Source is ControlBase
        {
            IsDisposed: false,
            EffectiveIsEnabled: true,
            EffectiveIsVisible: true
        } control &&
        IsSelectionContentSource(control);

    private bool TrySourceViewportBounds(TextSelectionSource source, out Rect bounds)
    {
        if (source.Viewport is null || source.Source is not ControlBase { IsDisposed: false } control)
        {
            bounds = default;
            return false;
        }

        var local = source.Viewport.SelectableTextViewport;
        var raw = new Rect(
            AddCoordinates(control.Bounds.X, local.X),
            AddCoordinates(control.Bounds.Y, local.Y),
            local.Width,
            local.Height);
        bounds = raw.Intersect(GetDescendantSelectableTextInheritedClip(control));
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static (int Horizontal, int Vertical) AutoScrollDelta(Point cells, Rect viewport) =>
        (AutoScrollDelta(cells.X, viewport.X, viewport.Width),
         AutoScrollDelta(cells.Y, viewport.Y, viewport.Height));

    [Pure]
    private static int AutoScrollDelta(int coordinate, int origin, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        if (coordinate < origin)
        {
            long distance = origin;
            distance -= coordinate;
            return -(int) Math.Clamp(distance, 1, 8);
        }

        var end = (long) origin + length;
        return coordinate >= end
            ? (int) Math.Clamp(coordinate - end + 1, 1, 8)
            : 0;
    }

    [Pure]
    private static int ClampToViewportAxis(int coordinate, int origin, int length)
    {
        long value = coordinate;
        return length <= 0
            ? origin
            : (int) Math.Clamp(value, origin, (long) origin + length - 1);
    }

    /// <summary>Finds the enabled semantic link at one screen cell.</summary>
    /// <param name="cells">The pointer's screen-cell coordinate.</param>
    /// <returns>The enabled link under the cell, or null.</returns>
    internal DocumentLink? LinkAt(Point cells)
    {
        var bounds = _surface.ContentBounds;
        var line = Difference(cells.Y, bounds.Y);
        var column = Difference(cells.X, bounds.X);

        foreach (var region in _layout.LinkRegions)
        {
            if (region.Line == line &&
                region.Contains(column) &&
                _layout.Links[region.LinkIndex] is { IsEnabled: true } link)
            {
                return link;
            }
        }

        return null;
    }

    /// <summary>Transfers exclusive pointer capture to this document.</summary>
    /// <returns>True when capture is owned after the request.</returns>
    internal bool CaptureSelectionPointer() => CapturePointer();

    /// <summary>Releases exclusive capture only when this document owns it.</summary>
    internal void ReleaseSelectionPointerCapture()
    {
        if (HasPointerCapture)
        {
            ReleasePointerCapture();
        }
    }

    /// <summary>Commits a hit-tested directional selection without repeating public validation.</summary>
    /// <param name="anchor">The grapheme-aligned anchor endpoint.</param>
    /// <param name="caret">The grapheme-aligned active endpoint.</param>
    internal void CommitPointerSelection(int anchor, int caret)
    {
        _selectionCaretEstablished = true;
        ResetSelectionDesiredColumn();
        CommitSelection(new Selection(anchor, caret));
    }

    private static int Difference(int left, int right) =>
        (int) Math.Clamp((long) left - right, int.MinValue, int.MaxValue);

    #endregion

    #region Measurement

    /// <summary>Rebuilds the projection when needed and reports the content extent.</summary>
    /// <param name="width">The bounded content width, or null when measuring unbounded.</param>
    /// <param name="force">Whether a presenter-side embedded-control remeasure requires a fresh projection.</param>
    /// <returns>The full content extent in cells.</returns>
    /// <remarks>
    /// Idempotent and free of side effects beyond the cached projection, because the scrolling host
    /// calls it more than once per pass while deciding whether a scrollbar is needed. The glyph family
    /// and cell policy participate in the cache key, which is what makes a theme swap or a terminal
    /// capability change rebuild the layout without any change-notification plumbing.
    /// </remarks>
    internal Size MeasureContent(int? width, bool force = false)
    {
        var effective = width ?? _unboundedWidth;
        var glyphs = ActualStyle.Glyphs;
        var ambiguousWidth = CellPolicy.AmbiguousWidth;

        if (force || !_layoutValid ||
            _layoutWidth != effective ||
            _layoutGlyphs != glyphs ||
            _layoutAmbiguousWidth != ambiguousWidth)
        {
            var activeLink = ActiveLink;
            _layout.Build(Blocks, effective, ambiguousWidth, glyphs);
            _layoutValid = true;
            _layoutWidth = effective;
            _layoutGlyphs = glyphs;
            _layoutAmbiguousWidth = ambiguousWidth;
            AdoptSelectionMap(_layout.SelectionMap);
            RestoreActiveLink(activeLink);
        }

        return new Size(_layout.MaxCells, _layout.Lines.Count);
    }

    #endregion

    #region Rendering

    /// <summary>Paints the projected lines intersecting the clipped viewport.</summary>
    /// <param name="canvas">The viewport-clipped canvas.</param>
    /// <param name="bounds">The surface's scroll-translated content bounds.</param>
    internal void RenderProjectedContent(TerminalCanvas canvas, Rect bounds)
    {
        var first = Math.Max(0, canvas.Bounds.Y - bounds.Y);
        var last = Math.Min(_layout.Lines.Count, canvas.Bounds.Bottom - bounds.Y);

        for (var index = first; index < last; index++)
        {
            RenderLine(canvas, bounds, index);
        }

        RenderQuoteBars(canvas, bounds, first, last);
        RenderMarkers(canvas, bounds, first, last);
    }

    /// <inheritdoc/>
    protected override void OnRenderAdornment(TerminalCanvas canvas)
    {
        base.OnRenderAdornment(canvas);

        if (_selection.IsEmpty)
        {
            return;
        }

        var background = BackgroundMode.Transparent;
        var selectionStyle = Apply(ActualStyle.SelectionFace, ref background);
        var contentBounds = _surface.ContentBounds;

        foreach (var glyph in _layout.SelectionMap.Glyphs)
        {
            if (glyph.Range.Start < _selection.Start || glyph.Range.End > _selection.End)
            {
                continue;
            }

            canvas.ApplyCellStyle(
                new Rect(
                    AddCoordinates(contentBounds.X, glyph.Bounds.X),
                    AddCoordinates(contentBounds.Y, glyph.Bounds.Y),
                    glyph.Bounds.Width,
                    glyph.Bounds.Height),
                (_, current) => new TerminalStyle(
                    selectionStyle.Foreground,
                    selectionStyle.Background,
                    selectionStyle.Attributes,
                    current.Hyperlink,
                    selectionStyle.Underline,
                    selectionStyle.UnderlineColor));
        }
    }

    private void RenderLine(TerminalCanvas canvas, Rect bounds, int lineIndex)
    {
        var y = bounds.Y + lineIndex;

        foreach (var run in _layout.RunsOf(_layout.Lines[lineIndex]))
        {
            if (run.Kind == DocumentRunKind.Control)
            {
                continue;
            }

            var origin = new Point(bounds.X + run.Column, y);
            var style = StyleFor(
                run.Face,
                run.LinkIndex,
                run.ForegroundOverride,
                out var background);

            if (run.Kind == DocumentRunKind.Repeat)
            {
                canvas.Fill(new Rect(origin.X, origin.Y, run.Cells, 1), run.Glyph, style);
                continue;
            }

            var lockForeground = IsForegroundLocked(run);
            DrawText(
                canvas,
                _layout.ParsedRunOf(run),
                run,
                origin,
                style,
                background,
                lockForeground,
                lockUnderlineColor: run.ForegroundOverride is not null || IsStandardCalloutFace(run.Face));
        }
    }

    private void RenderQuoteBars(TerminalCanvas canvas, Rect bounds, int first, int last)
    {
        var glyph = ActualStyle.Glyphs.QuoteBarGlyph;
        var bar = glyph.Value.Resolve(glyph.Fallback, CellPolicy.AmbiguousWidth);

        foreach (var quote in _layout.QuoteBars)
        {
            var from = Math.Max(first, quote.FirstLine);
            var to = Math.Min(last, quote.LastLine + 1);
            var style = StyleFor(quote.Face, linkIndex: -1, quote.ForegroundOverride, out _);

            for (var line = from; line < to; line++)
            {
                canvas.DrawRune(bar, new Point(bounds.X + quote.Column, bounds.Y + line), style, BackgroundMode.Transparent);
            }
        }
    }

    private void RenderMarkers(TerminalCanvas canvas, Rect bounds, int first, int last)
    {
        foreach (var marker in _layout.Markers)
        {
            if (marker.Line < first || marker.Line >= last)
            {
                continue;
            }

            var style = StyleFor(
                DocumentFaceKind.Marker,
                linkIndex: -1,
                marker.ForegroundOverride,
                out _);
            _ = canvas.Draw(
                marker.Text,
                new Point(bounds.X + marker.Column, bounds.Y + marker.Line),
                style,
                background: BackgroundMode.Transparent);
        }
    }

    // Markup spans change at exact character boundaries, which need not align with the whitespace
    // boundaries a run was tokenized on. Flushing a fresh Draw at every span change keeps a tag that
    // opens or closes mid-word rendering exactly as Text would render the same markup.
    private static void DrawText(
        TerminalCanvas canvas,
        DocumentParsedRun parsed,
        DocumentVisualRun run,
        Point origin,
        TerminalStyle style,
        BackgroundMode background,
        bool lockForeground,
        bool lockUnderlineColor)
    {
        var spans = parsed.Spans;

        if (spans.Length == 0)
        {
            _ = canvas.Draw(parsed.Display.AsSpan(run.Offset, run.Length), origin, style, background: background);
            return;
        }

        var cells = 0;
        var runOffset = run.Offset;
        var runLength = 0;
        var spanIndex = SpanIndexAt(spans, run.Offset);
        var flushSpanIndex = spanIndex;

        void Flush()
        {
            if (runLength == 0)
            {
                return;
            }

            var span = flushSpanIndex >= 0 ? spans[flushSpanIndex] : default;
            var position = new Point(origin.X + cells, origin.Y);
            var merged = Merge(style, span);

            if (lockForeground)
            {
                merged = new TerminalStyle(
                    style.Foreground,
                    merged.Background,
                    merged.Attributes,
                    merged.Hyperlink,
                    merged.Underline,
                    lockUnderlineColor && merged.Underline != Underline.None
                        ? style.Foreground
                        : merged.UnderlineColor);
            }

            var result = canvas.Draw(
                parsed.Display.AsSpan(runOffset, runLength),
                position,
                merged,
                background: span.Background.HasValue ? BackgroundMode.Opaque : background);
            cells += result.Final.X - position.X;
        }

        foreach (var grapheme in Graphemes.Enumerate(parsed.Display.AsSpan(run.Offset, run.Length)))
        {
            var offset = run.Offset + grapheme.Offset;
            var nextSpanIndex = AdvanceSpan(spans, spanIndex, offset);

            if (nextSpanIndex != flushSpanIndex)
            {
                Flush();
                runOffset = offset;
                flushSpanIndex = nextSpanIndex;
            }

            runLength = offset + grapheme.Length - runOffset;
            spanIndex = nextSpanIndex;
        }

        Flush();
    }

    private bool IsForegroundLocked(DocumentVisualRun run) =>
        !EffectiveIsEnabled ||
        run.ForegroundOverride is not null ||
        IsStandardCalloutFace(run.Face) ||
        (run.LinkIndex >= 0 &&
         run.LinkIndex < _layout.Links.Count &&
         !_layout.Links[run.LinkIndex].IsEnabled);

    private static bool IsStandardCalloutFace(DocumentFaceKind face) => face is
        DocumentFaceKind.CalloutNote or
        DocumentFaceKind.CalloutNoteTitle or
        DocumentFaceKind.CalloutTip or
        DocumentFaceKind.CalloutTipTitle or
        DocumentFaceKind.CalloutImportant or
        DocumentFaceKind.CalloutImportantTitle or
        DocumentFaceKind.CalloutWarning or
        DocumentFaceKind.CalloutWarningTitle or
        DocumentFaceKind.CalloutCaution or
        DocumentFaceKind.CalloutCautionTitle;

    #endregion

    #region Style resolution

    private TerminalStyle StyleFor(
        DocumentFaceKind face,
        int linkIndex,
        DocumentFaceKind? foregroundOverride,
        out BackgroundMode background)
    {
        background = BackgroundMode.Transparent;
        var resolved = ResolvedStyle;

        // A disabled document dims uniformly. Resolving every face to the disabled body style is what
        // stops a heading, marker, or link from staying bright while the paragraphs around it fade.
        if (!EffectiveIsEnabled)
        {
            return resolved;
        }

        var style = ActualStyle;

        var result = face switch
        {
            DocumentFaceKind.Body => resolved,
            DocumentFaceKind.Heading => Apply(style.HeadingFace, ref background),
            DocumentFaceKind.MinorHeading => WithBold(resolved),
            DocumentFaceKind.Marker => Apply(style.MarkerFace, ref background),
            DocumentFaceKind.Quote => Apply(style.QuoteFace, ref background),
            DocumentFaceKind.Code => Apply(style.CodeFace, ref background),
            DocumentFaceKind.Rule => Apply(style.RuleFace, ref background),
            DocumentFaceKind.Callout => Apply(style.CalloutFace, ref background),
            DocumentFaceKind.CalloutTitle => Apply(style.CalloutTitleFace, ref background),
            DocumentFaceKind.CalloutNote => ApplyCallout(style.CalloutFace, SemanticColor.Info, ref background),
            DocumentFaceKind.CalloutNoteTitle =>
                ApplyCallout(style.CalloutTitleFace, SemanticColor.Info, ref background),
            DocumentFaceKind.CalloutTip => ApplyCallout(style.CalloutFace, SemanticColor.Success, ref background),
            DocumentFaceKind.CalloutTipTitle =>
                ApplyCallout(style.CalloutTitleFace, SemanticColor.Success, ref background),
            DocumentFaceKind.CalloutImportant =>
                ApplyCallout(style.CalloutFace, SemanticColor.Accent, ref background),
            DocumentFaceKind.CalloutImportantTitle =>
                ApplyCallout(style.CalloutTitleFace, SemanticColor.Accent, ref background),
            DocumentFaceKind.CalloutWarning =>
                ApplyCallout(style.CalloutFace, SemanticColor.Warning, ref background),
            DocumentFaceKind.CalloutWarningTitle =>
                ApplyCallout(style.CalloutTitleFace, SemanticColor.Warning, ref background),
            DocumentFaceKind.CalloutCaution => ApplyCallout(style.CalloutFace, SemanticColor.Error, ref background),
            DocumentFaceKind.CalloutCautionTitle =>
                ApplyCallout(style.CalloutTitleFace, SemanticColor.Error, ref background),
            DocumentFaceKind.Table => Apply(style.TableFace, ref background),
            DocumentFaceKind.TableHeader => Apply(style.TableHeaderFace, ref background),
            DocumentFaceKind.Link => Apply(LinkFace(style, linkIndex), ref background),
            _ => resolved
        };

        if (foregroundOverride is null)
        {
            return result;
        }

        var foreground = StyleFor(foregroundOverride.Value, linkIndex: -1, foregroundOverride: null, out _)
            .Foreground;
        return result.WithForeground(foreground);
    }

    private TerminalStyle ApplyCallout(Face face, SemanticColor color, ref BackgroundMode background) =>
        Apply(face with { Foreground = new ControlColor(color) }, ref background);

    private Face LinkFace(DocumentStyle style, int linkIndex)
    {
        if (linkIndex < 0 || linkIndex >= _layout.Links.Count)
        {
            return style.LinkFace;
        }

        var link = _layout.Links[linkIndex];

        // Disabled looks the same regardless of emphasis: graying a link out already communicates
        // its state without a distinct chip appearance to gray out.
        if (!link.IsEnabled)
        {
            return style.DisabledLinkFace;
        }

        var isActive = linkIndex == ActiveLinkIndex && IsFocused;

        return link.Emphasis switch
        {
            DocumentLinkEmphasis.Action => isActive ? style.ActiveActionLinkFace : style.ActionLinkFace,
            DocumentLinkEmphasis.Standard => isActive ? style.ActiveLinkFace : style.LinkFace,
            _ => isActive ? style.ActiveLinkFace : style.LinkFace
        };
    }

    private TerminalStyle Apply(Face face, ref BackgroundMode background)
    {
        var inherited = ResolvedStyle;
        var theme = Theme;
        var foreground = ResolveColor(face.Foreground, theme);
        var resolvedBackground = ResolveColor(face.Background, theme);
        var attributes = face.Attributes.IsLiteral
            ? face.Attributes.Literal
            : theme?.ResolveAttributes(face.Attributes.SemanticDecoration) ?? TerminalAttributes.None;
        var underlineColor = ResolveColor(face.UnderlineColor, theme);

        if (resolvedBackground != Color.Transparent)
        {
            background = BackgroundMode.Opaque;
        }

        var (resolvedAttributes, resolvedUnderline, resolvedUnderlineColor) = DecorationResolver.Resolve(
            inherited,
            attributes,
            face.Underline == Underline.None ? null : face.Underline,
            underlineColor == Color.Default ? null : underlineColor);

        return new TerminalStyle(
            foreground == Color.Default ? inherited.Foreground : foreground,
            resolvedBackground == Color.Transparent ? inherited.Background : resolvedBackground,
            resolvedAttributes,
            inherited.Hyperlink,
            resolvedUnderline,
            resolvedUnderlineColor);
    }

    private static TerminalStyle WithBold(TerminalStyle style) => new(
        style.Foreground,
        style.Background,
        style.Attributes | TerminalAttributes.Bold,
        style.Hyperlink,
        style.Underline,
        style.UnderlineColor);

    // Blink and rapid blink are mutually exclusive in a cell style, so a span asking for either must
    // clear both inherited bits before contributing its own.
    private static TerminalStyle Merge(TerminalStyle style, StyleSpan span)
    {
        var attributes = style.Attributes;

        if ((span.Attributes & (TerminalAttributes.Blink | TerminalAttributes.RapidBlink)) != 0)
        {
            attributes &= ~(TerminalAttributes.Blink | TerminalAttributes.RapidBlink);
        }

        attributes |= span.Attributes;
        Underline? underline = null;

        if (span.Underline != Underline.None)
        {
            attributes &= ~TerminalAttributes.Underline;
            underline = span.Underline;
        }

        var (resolvedAttributes, resolvedUnderline, resolvedUnderlineColor) = DecorationResolver.Resolve(
            style,
            attributes,
            underline,
            span.UnderlineColor);

        return new TerminalStyle(
            span.Foreground ?? style.Foreground,
            span.Background ?? style.Background,
            resolvedAttributes,
            span.Link ?? style.Hyperlink,
            resolvedUnderline,
            resolvedUnderlineColor);
    }

    [Pure]
    private static int SpanIndexAt(StyleSpan[] spans, int offset)
    {
        for (var index = 0; index < spans.Length; index++)
        {
            if (offset >= spans[index].Offset && offset < spans[index].Offset + spans[index].Length)
            {
                return index;
            }
        }

        return -1;
    }

    [Pure]
    private static int AdvanceSpan(StyleSpan[] spans, int index, int offset)
    {
        if (index < 0)
        {
            return SpanIndexAt(spans, offset);
        }

        while (index + 1 < spans.Length && offset >= spans[index].Offset + spans[index].Length)
        {
            index++;
        }

        return index;
    }

    [Pure]
    private static int AddCoordinates(int left, int right) =>
        (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _selectionGesture.Cancel(releaseCapture: false);
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (!focused && _selectionGesture.Phase == DocumentSelectionGesturePhase.Selecting)
        {
            _selectionGesture.Cancel(releaseCapture: true);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _selectionGesture.Cancel(releaseCapture: false);

        if (reason == ReleaseReason.Disposed)
        {
            ScrollChanged = null;
            LinkClicked = null;
            SelectionChanged = null;
        }
    }

    #endregion
}
