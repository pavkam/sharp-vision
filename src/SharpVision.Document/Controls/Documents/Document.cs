// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

using System.Runtime.ExceptionServices;

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
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private readonly LayoutStack _stack;
    private readonly StyleSlot<DocumentStyle> _style;
    private readonly DocumentSurface _surface;

    private Ambiguous _layoutAmbiguousWidth;
    private DocumentLink? _activeLink;
    private DocumentGlyphs _layoutGlyphs;
    private bool _layoutValid;
    private int _layoutWidth = -1;
    private int _horizontalOffset;
    private TextSelectionMap _selectionSemanticMap = TextSelectionMap.Empty;
    private ulong _scrollTransitionVersion;
    private ulong _linkTransitionVersion;

    /// <summary>Initializes an empty scrollable document.</summary>
    public Document()
    {
        Blocks = new DocumentBlockCollection(this);

        // The surface exists before the style slot because the slot's change callback invalidates it,
        // and a slot can publish its first resolved value while it is still being initialized.
        _surface = new DocumentSurface(this);
        _presenter = new DocumentPresenter(this, _surface);
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

    /// <summary>Reads serialized content and atomically replaces the current block tree after parsing succeeds.</summary>
    /// <param name="source">The non-null serialized source.</param>
    /// <param name="reader">The non-null format reader.</param>
    /// <param name="options">Optional general read limits.</param>
    /// <returns>The consumed read result whose exact blocks now belong to this document.</returns>
    /// <remarks>
    /// The complete mutable result tree is revalidated before replacement. A rejected result leaves
    /// the current tree unchanged; a successful load transfers every result root into this document,
    /// so the same result cannot be loaded again.
    /// </remarks>
    /// <remarks>
    /// Replacement is transactional. A subscriber notified synchronously while the previous tree is
    /// detached or the new one is attached - for example a focused embedded control's
    /// <c>FocusLeft</c> - can throw or attempt its own reentrant mutation without turning a failed
    /// load into content loss: this document's blocks are restored to what they held before the call
    /// began, and the triggering exception then propagates.
    /// </remarks>
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
        return LoadCore(source, reader, options, observeCancellation: false, cancellationToken: default);
    }

    /// <summary>Reads a bounded text stream asynchronously, leaves it open, and replaces the current tree.</summary>
    /// <param name="source">The non-null readable stream.</param>
    /// <param name="reader">The non-null format reader.</param>
    /// <param name="options">Optional general read limits.</param>
    /// <param name="encoding">The optional source encoding; UTF-8 is used when null.</param>
    /// <param name="cancellationToken">Cancels asynchronous stream reads before mutation.</param>
    /// <returns>The consumed result whose exact blocks now belong to this document.</returns>
    /// <remarks>
    /// Lifecycle validation completes before the first read. If <paramref name="source"/> supports
    /// seeking, any failure restores its original byte position. A non-seekable source remains at
    /// the position reached by decoding because consumed bytes cannot be restored.
    /// </remarks>
    /// <remarks>
    /// The block structure this call started against is recorded before the first asynchronous read.
    /// If other dispatcher-scheduled work structurally mutates <see cref="Blocks"/> while this call is
    /// suspended awaiting the stream, replacement is rejected instead of silently discarding that
    /// already-committed mutation.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable, or the reader
    /// result is no longer one complete detached tree.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Decoded content exceeds the configured limit.</exception>
    /// <exception cref="DecoderFallbackException">The source contains malformed data for its
    /// explicit or byte-order-mark-selected Unicode encoding.</exception>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher, or
    /// other dispatcher-scheduled work structurally mutated it while this call was suspended.</exception>
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
        VerifyMutable();

        if (!source.CanRead)
        {
            throw new ArgumentException("The document source stream must be readable.", nameof(source));
        }

        options ??= new DocumentReadOptions();
        encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var initialPosition = source.CanSeek ? source.Position : (long?) null;

        // Captured before the first await: the dispatcher resumes a suspended continuation by
        // re-posting it to the back of its queue, so other dispatcher-scheduled work - a plain
        // Blocks edit, or a second overlapping LoadAsync - can run and commit while this method is
        // suspended on a stream read below. LoadCore rechecks this against the current version before
        // touching Blocks, so a race is reported instead of silently discarding the interloper's
        // already-committed mutation.
        var expectedVersion = StructureVersion;

        try
        {
            var encodingPrefix = new byte[4];
            var encodingPrefixLength = 0;

            while (encodingPrefixLength < encodingPrefix.Length)
            {
                var count = await source.ReadAsync(
                    encodingPrefix.AsMemory(encodingPrefixLength),
                    cancellationToken);

                if (count == 0)
                {
                    break;
                }

                encodingPrefixLength += count;
            }

            encoding = ResolveStreamEncoding(
                encodingPrefix.AsSpan(0, encodingPrefixLength),
                encoding,
                out var preambleLength);
            using var decodedSource = new DocumentPrefixedReadStream(
                source,
                encodingPrefix,
                preambleLength,
                encodingPrefixLength - preambleLength);
            using var textReader = new StreamReader(
                decodedSource,
                encoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);
            var builder = new StringBuilder(Math.Min(options.MaximumCharacters, 4096));
            var buffer = ArrayPool<char>.Shared.Rent(Math.Min(options.MaximumCharacters, 4096));

            try
            {
                while (true)
                {
                    var remaining = options.MaximumCharacters - builder.Length;
                    var requested = (int) Math.Min(buffer.Length, (long) remaining + 1);
                    var count = await textReader.ReadAsync(buffer.AsMemory(0, requested), cancellationToken);

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

            cancellationToken.ThrowIfCancellationRequested();
            return LoadCore(
                builder.ToString(),
                reader,
                options,
                observeCancellation: true,
                cancellationToken,
                expectedVersion);
        }
        catch
        {
            if (initialPosition is { } position)
            {
                _ = source.Seek(position, SeekOrigin.Begin);
            }

            throw;
        }
    }

    private DocumentReadResult LoadCore(
        string source,
        IDocumentFormatReader reader,
        DocumentReadOptions? options,
        bool observeCancellation,
        CancellationToken cancellationToken,
        ulong? expectedVersion = null)
    {
        VerifyMutable();

        // An asynchronous load captures the structure version before its first await. Checking it
        // here, before anything else in this method runs, catches other dispatcher-scheduled work
        // that mutated Blocks while this load's stream reads were suspended - the read result no
        // longer describes what would be replaced, so proceeding would silently discard that
        // intervening, already-committed mutation. This must run before the snapshot/rollback logic
        // below even begins: that logic's own Clear+Add will itself bump the version, but only after
        // this check has already passed.
        if (expectedVersion is { } expected && expected != StructureVersion)
        {
            throw new InvalidOperationException(
                "The document's block structure was mutated by other dispatcher-scheduled work while " +
                "this asynchronous load was in flight. The read content no longer corresponds to the " +
                "tree it would replace, so it was discarded instead of silently overwriting that change.");
        }

        if (observeCancellation)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        var result = reader.Read(source, options);
        ArgumentNullException.ThrowIfNull(result);

        if (observeCancellation)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        result.ValidateForConsumption(nameof(reader));

        if (observeCancellation)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Snapshotted before mutation starts so a callback that interrupts the replacement - fired
        // synchronously from Blocks.Clear() or Blocks.Add() reconciling retained-control membership -
        // can be rolled back to the exact tree this load found, whether it threw its own exception or
        // tripped the owned-control reentrancy guard partway through.
        var oldBlocks = Blocks.ToList();

        try
        {
            Blocks.Clear();

            foreach (var block in result.Blocks)
            {
                Blocks.Add(block);
            }
        }
        catch (Exception exception)
        {
            var failure = ExceptionDispatchInfo.Capture(exception);
            ExceptionAggregation.Capture(() => RestoreBlocks(oldBlocks), ref failure);
            failure!.Throw();
            throw;
        }

        return result;
    }

    /// <summary>Restores the block collection to a previously captured snapshot after a failed
    /// replacement.</summary>
    /// <param name="oldBlocks">The exact roots owned before the failed replacement began.</param>
    private void RestoreBlocks(List<DocumentBlock> oldBlocks)
    {
        Blocks.Clear();

        foreach (var block in oldBlocks)
        {
            Blocks.Add(block);
        }
    }

    /// <summary>Gets how many structural reconciliations have run, exposing the invariant that
    /// non-structural mutations never rescan retained-control membership.</summary>
    internal int ControlReconciliationCount => _presenter.ReconciliationCount;

    /// <summary>Invalidates measured and rendered content without changing retained-control
    /// membership.</summary>
    internal void InvalidateContent()
    {
        _layoutValid = false;
        InvalidateRetainedDescendant(_surface, InvalidationImpact.Measure);
    }

    /// <summary>Gets how many times the block structure has actually changed, so in-flight
    /// asynchronous work can detect a mutation that happened while it was suspended.</summary>
    /// <remarks>
    /// Every structural mutation - a direct <see cref="Blocks"/> edit, a nested collection edit
    /// anywhere in the tree, or a <see cref="Load"/>/<see cref="LoadAsync"/> replacement - bumps this
    /// counter exactly once through the single <see cref="InvalidateStructure"/> choke point.
    /// </remarks>
    internal ulong StructureVersion { get; private set; }

    /// <summary>Reconciles retained-control membership after the semantic tree changes, then
    /// invalidates measured and rendered content.</summary>
    internal void InvalidateStructure()
    {
        // Bumped before reconciliation runs, because reconciliation can synchronously drive focus
        // changes whose handlers observe or race this version - the structural edit that triggered
        // this call already committed to Blocks by the time any caller reaches here.
        unchecked
        {
            StructureVersion++;
        }

        _presenter.ReconcileControls();
        InvalidateContent();
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

    /// <summary>Reconciles semantic state before a subtree loses the ancestry needed to identify
    /// the document state it owns.</summary>
    /// <param name="subtree">The attached subtree about to be detached.</param>
    internal void OnNodeDetaching(DocumentNode subtree)
    {
        Debug.Assert(subtree is not null, "A detachment notification identifies its subtree.");

        for (DocumentNode? ancestor = _activeLink; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            if (!ReferenceEquals(ancestor, subtree))
            {
                continue;
            }

            _activeLink = null;
            ActiveLinkIndex = -1;
            InvalidateActiveLinkAppearance();
            return;
        }
    }

    /// <summary>Resolves one document style color through a possibly absent theme.</summary>
    internal static Color ResolveDocumentColor(ControlColor value, Theme? theme) => ResolveColor(value, theme);

    /// <summary>Gets projected embedded-control rectangles for the private presenter.</summary>
    internal IReadOnlyList<DocumentControlPlacement> ControlPlacements => _layout.ControlPlacements;

    /// <summary>Projects the arranged document content through its bounded horizontal viewport.</summary>
    /// <param name="bounds">The unshifted presenter content bounds.</param>
    /// <returns>The horizontally translated bounds spanning the complete projected content width.</returns>
    internal Rect ProjectContentBounds(Rect bounds)
    {
        _ = ApplyHorizontal(_horizontalOffset, ScrollCause.Resize);
        return new Rect(
            Difference(bounds.X, _horizontalOffset),
            bounds.Y,
            Math.Max(bounds.Width, _layout.MaxCells),
            bounds.Height);
    }

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
    internal TextSelectionGesturePhase SelectionGesturePhase => TextSelectionPhase;

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

    private Rect SelectionViewportBounds()
    {
        var content = _surface.ContentBounds;
        return new Rect(
            AddCoordinates(content.X, _horizontalOffset),
            AddCoordinates(content.Y, VerticalOffset),
            Viewport.Width,
            Viewport.Height);
    }

    /// <inheritdoc/>
    public bool RevealSelectableTextOffset(int offset)
    {
        VerifyMutable();
        var projection = PrepareSelectionProjection();
        Edit.Validate(projection.Map.Text, new Selection(offset, offset));
        CommitSelectionProjection(projection.Layout);
        AdoptSelectionMap(projection.Map);

        return projection.Map.TryGetCaretGeometry(offset, out var bounds, out _) &&
               RevealViewportCell(bounds.X, bounds.Y, ScrollCause.Programmatic);
    }

    /// <inheritdoc/>
    public bool ScrollSelectableTextViewport(int horizontal, int vertical)
    {
        VerifyMutable();
        var movedHorizontal = ApplyHorizontal(
            AddCoordinates(_horizontalOffset, horizontal),
            ScrollCause.Pointer);

        if (IsDisposed)
        {
            return movedHorizontal;
        }

        var movedVertical = _stack.ScrollBy(0, vertical, ScrollCause.Pointer);
        return movedHorizontal || movedVertical;
    }

    /// <inheritdoc/>
    protected override bool ScrollTextSelectionViewport(int horizontal, int vertical, out Point hitAdjustment)
    {
        var previousHorizontal = _horizontalOffset;
        var previousVertical = VerticalOffset;
        var changed = ScrollSelectableTextViewport(horizontal, vertical);

        if (IsDisposed)
        {
            hitAdjustment = default;
            return changed;
        }

        hitAdjustment = new Point(
            Difference(_horizontalOffset, previousHorizontal),
            Difference(VerticalOffset, previousVertical));
        return changed;
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
    public Selection Selection => TextSelection;

    /// <summary>Gets an owned copy of the selected normalized semantic text, or an empty string.</summary>
    /// <exception cref="InvalidOperationException">The attached document is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    [Pure]
    public string CopySelection() => CopySelectedText();

    /// <summary>Raised after the directional semantic selection commits to a different value.</summary>
    /// <remarks>The event runs synchronously on the owning dispatcher after state commits.</remarks>
    public event EventHandler? SelectionChanged;

    /// <summary>Replaces the selection with validated UTF-16 grapheme-boundary endpoints.</summary>
    /// <param name="selection">The proposed directional semantic selection.</param>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the semantic text length.</exception>
    /// <exception cref="ArgumentException">An endpoint splits an extended grapheme cluster.</exception>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public void SetSelection(Selection selection) => SetTextSelection(selection);

    /// <inheritdoc/>
    public override void SetTextSelection(Selection selection)
    {
        VerifyTextSelectionEnabled();
        var projection = PrepareSelectionProjection();
        Edit.Validate(projection.Map.Text, selection);
        CommitSelectionProjection(projection.Layout);

        // Validation completed before adoption, so an invalid request cannot publish the stale
        // selection's reconciliation. A valid request preserves the documented clear-then-select
        // sequence when the semantic stream changed underneath the prior range.
        AdoptSelectionMap(projection.Map);
        base.SetTextSelection(selection);
    }

    /// <summary>Selects the complete normalized semantic document stream.</summary>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public void SelectAll() => SelectAllText();

    /// <summary>Collapses the selection to its current active caret endpoint.</summary>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public void ClearSelection() => ClearTextSelection();

    /// <inheritdoc/>
    protected override bool HasAuthoritativeTextSelectionProjection => true;

    /// <inheritdoc/>
    internal override TextSelectionMap GetTextSelectionMap() => EnsureSelectionProjection();

    /// <inheritdoc/>
    protected override void OnTextSelectionCommitted(TextSelectionChangedEventArgs eventArgs, ulong transitionVersion)
    {
        _ = eventArgs;
        RaiseTextSelectionCompatibilityEvent(SelectionChanged, this, transitionVersion);
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
        if (CommittedTextSelection != default)
        {
            _ = CommitTextSelection(default);
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

    /// <summary>Raised after either document viewport offset commits.</summary>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    /// <summary>Gets the committed non-negative content extent.</summary>
    public Size Extent => new(Math.Max(_stack.Extent.Width, _layout.MaxCells), _stack.Extent.Height);

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

    private int MaximumHorizontalOffset => Math.Max(0, Extent.Width - Viewport.Width);

    private bool Apply(int offset, ScrollCause cause)
    {
        var target = Math.Clamp(offset, 0, MaximumOffset);
        return target != VerticalOffset && _stack.ScrollBy(0, target - VerticalOffset, cause);
    }

    private bool RevealViewportCell(int column, int line, ScrollCause cause)
    {
        var targetHorizontal = _horizontalOffset;
        var targetVertical = VerticalOffset;

        if (column < targetHorizontal)
        {
            targetHorizontal = column;
        }
        else if (Viewport.Width > 0 && column >= AddCoordinates(targetHorizontal, Viewport.Width))
        {
            targetHorizontal = Math.Min(
                MaximumHorizontalOffset,
                column - Viewport.Width + 1);
        }

        if (line < targetVertical)
        {
            targetVertical = line;
        }
        else if (Viewport.Height > 0 && line >= AddCoordinates(targetVertical, Viewport.Height))
        {
            targetVertical = line - Viewport.Height + 1;
        }

        var movedHorizontal = ApplyHorizontal(targetHorizontal, cause);
        var movedVertical = Apply(targetVertical, cause);
        return movedHorizontal || movedVertical;
    }

    private bool ApplyHorizontal(int offset, ScrollCause cause)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause, nameof(cause), "The scroll cause is unknown.");
        var target = Math.Clamp(offset, 0, MaximumHorizontalOffset);

        if (target == _horizontalOffset)
        {
            return false;
        }

        var previous = new Point(_horizontalOffset, VerticalOffset);
        _horizontalOffset = target;
        InvalidateRetainedDescendant(_presenter, InvalidationImpact.Arrange);

        unchecked
        {
            _scrollTransitionVersion++;
        }

        RaiseScrollChanged(
            new ScrollChangedEventArgs(
                previous,
                new Point(_horizontalOffset, VerticalOffset),
                Extent,
                Viewport,
                cause),
            _scrollTransitionVersion);
        return true;
    }

    private void OnStackScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        _ = sender;

        unchecked
        {
            _scrollTransitionVersion++;
        }

        RaiseScrollChanged(
            new ScrollChangedEventArgs(
                new Point(_horizontalOffset, eventArgs.PreviousOffset.Y),
                new Point(_horizontalOffset, eventArgs.Offset.Y),
                Extent,
                Viewport,
                eventArgs.Cause),
            _scrollTransitionVersion);
    }

    /// <summary>Delivers <see cref="ScrollChanged"/> to each subscriber only while this transition is
    /// still the newest one. <see cref="ApplyHorizontal"/> and <see cref="OnStackScrollChanged"/> are two
    /// independent paths that both forward into this single event, and both share the same version
    /// field, so a subscriber that reentrantly triggers another scroll change through either path
    /// supersedes delivery to later subscribers rather than letting a stale transition reach
    /// them.</summary>
    /// <param name="eventArgs">The immutable transition being delivered.</param>
    /// <param name="transitionVersion">The version captured when this transition was raised.</param>
    private void RaiseScrollChanged(ScrollChangedEventArgs eventArgs, ulong transitionVersion)
    {
        var handlers = ScrollChanged;

        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList())
        {
            if (_scrollTransitionVersion != transitionVersion)
            {
                break;
            }

            var handler = (EventHandler<ScrollChangedEventArgs>) subscriber;
            handler(this, eventArgs);
        }
    }

    #endregion

    #region Links

    /// <summary>Raised after any link is activated, following that link's own
    /// <see cref="DocumentLink.Clicked"/> event.</summary>
    public event EventHandler<DocumentLinkEventArgs>? LinkClicked;

    /// <summary>Gets or sets the focused link, or null when no link is focused.</summary>
    /// <remarks>
    /// Selection resolves against the links in the most recent layout projection. Setting a link
    /// that is unprojected, not in this document, or disabled clears the selection instead.
    /// Disabling or detaching the focused link also clears the selection synchronously.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached document is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public DocumentLink? ActiveLink
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _activeLink is { IsEnabled: true } link && ReferenceEquals(link.OwnerDocument, this)
                ? link
                : null;
        }
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
                           index >= 0
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

            if (!link.IsEnabled || !ReferenceEquals(link.OwnerDocument, this))
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

        unchecked
        {
            _linkTransitionVersion++;
        }

        RaiseLinkClicked(new DocumentLinkEventArgs(link), _linkTransitionVersion);
        return true;
    }

    /// <summary>Delivers <see cref="LinkClicked"/> to each subscriber only while this transition is still
    /// the newest one, so a subscriber that reentrantly triggers another link activation supersedes
    /// delivery to later subscribers rather than letting a stale transition reach them.</summary>
    /// <param name="eventArgs">The immutable transition being delivered.</param>
    /// <param name="transitionVersion">The version captured when this transition was raised.</param>
    private void RaiseLinkClicked(DocumentLinkEventArgs eventArgs, ulong transitionVersion)
    {
        var handlers = LinkClicked;

        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList())
        {
            if (_linkTransitionVersion != transitionVersion)
            {
                break;
            }

            var handler = (EventHandler<DocumentLinkEventArgs>) subscriber;
            handler(this, eventArgs);
        }
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

            _ = RevealViewportCell(region.Column, region.Line, ScrollCause.Programmatic);
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

        if (IsFocused &&
            (stroke.Code == Code.Enter ||
             (stroke.Code == Code.Character && stroke.Character == new Rune(' '))))
        {
            eventArgs.IsHandled =
                stroke.Modifiers.IsActivationEligible() &&
                (!eventArgs.IsInitialKeyDown || ActivateLink());
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

    /// <inheritdoc/>
    protected override bool IsTextSelectionPointerTarget(ControlBase? originalSource, Point cells)
    {
        _ = cells;
        return IsSelectionContentSource(originalSource);
    }

    /// <inheritdoc/>
    protected override int HitTestTextSelectionCore(Point cells) => HitTestSelection(cells);

    /// <inheritdoc/>
    protected override Rect GetTextSelectionAdornmentBounds(Rect bounds)
    {
        var content = _surface.ContentBounds;
        return new Rect(
            AddCoordinates(content.X, bounds.X),
            AddCoordinates(content.Y, bounds.Y),
            bounds.Width,
            bounds.Height);
    }

    /// <inheritdoc/>
    protected override TerminalStyle ApplyTextSelectionStyle(TerminalStyle current)
    {
        var background = BackgroundMode.Transparent;
        var style = Apply(ActualStyle.SelectionFace, ref background);
        return new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes,
            current.Hyperlink,
            style.Underline,
            style.UnderlineColor);
    }

    /// <inheritdoc/>
    protected override int TextSelectionPageDistance() => Math.Max(1, Viewport.Height - PageOverlap);

    /// <inheritdoc/>
    protected override int NormalizeTextSelectionClickCount(ControlBase? originalSource, int clickCount)
    {
        var map = EnsureSelectionProjection();

        for (var current = originalSource; current is not null && !ReferenceEquals(current, this); current = current.Parent)
        {
            foreach (var source in map.Sources)
            {
                if (ReferenceEquals(source.Source, current))
                {
                    return 1;
                }
            }
        }

        return clickCount;
    }

    /// <inheritdoc/>
    protected override void OnTextSelectionClickCompleted(
        ControlBase? originalSource,
        Point pressCells,
        Point releaseCells,
        int clickCount,
        PointerEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        _ = originalSource;

        if (clickCount != 1 ||
            LinkAt(pressCells) is not { IsEnabled: true } link ||
            !ReferenceEquals(link, LinkAt(releaseCells)))
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

    /// <inheritdoc/>
    internal override TextSelectionSource? GetTextSelectionSource(ControlBase? originalSource, Point cells)
    {
        var map = EnsureSelectionProjection();

        for (var current = originalSource; current is not null && !ReferenceEquals(current, this); current = current.Parent)
        {
            foreach (var source in map.Sources)
            {
                if (ReferenceEquals(source.Source, current))
                {
                    return source;
                }
            }
        }

        return base.GetTextSelectionSource(originalSource, cells);
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
                _layout.Links[region.LinkIndex] is { IsEnabled: true } link &&
                ReferenceEquals(link.OwnerDocument, this))
            {
                return link;
            }
        }

        return null;
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
                if (run.ParsedRunIndex >= 0)
                {
                    var parsed = _layout.ParsedRunOf(run);
                    var spanIndex = SpanIndexAt(parsed.Spans, run.Offset);

                    if (spanIndex >= 0)
                    {
                        var inheritedStyle = style;
                        style = Merge(style, parsed.Spans[spanIndex]);

                        if (IsForegroundLocked(run))
                        {
                            style = new TerminalStyle(
                                inheritedStyle.Foreground,
                                style.Background,
                                style.Attributes,
                                style.Hyperlink,
                                style.Underline,
                                (run.ForegroundOverride is not null || IsStandardCalloutFace(run.Face)) &&
                                style.Underline != Underline.None
                                    ? inheritedStyle.Foreground
                                    : style.UnderlineColor);
                        }
                    }
                }

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

    [Pure]
    private static Encoding ResolveStreamEncoding(
        ReadOnlySpan<byte> prefix,
        Encoding fallback,
        out int preambleLength)
    {
        if (prefix.Length >= 4 &&
            prefix[0] == 0xff && prefix[1] == 0xfe && prefix[2] == 0x00 && prefix[3] == 0x00)
        {
            preambleLength = 4;
            return new UTF32Encoding(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true);
        }

        if (prefix.Length >= 4 &&
            prefix[0] == 0x00 && prefix[1] == 0x00 && prefix[2] == 0xfe && prefix[3] == 0xff)
        {
            preambleLength = 4;
            return new UTF32Encoding(bigEndian: true, byteOrderMark: false, throwOnInvalidCharacters: true);
        }

        if (prefix.Length >= 3 && prefix[0] == 0xef && prefix[1] == 0xbb && prefix[2] == 0xbf)
        {
            preambleLength = 3;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        }

        if (prefix.Length >= 2 && prefix[0] == 0xff && prefix[1] == 0xfe)
        {
            preambleLength = 2;
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
        }

        if (prefix.Length >= 2 && prefix[0] == 0xfe && prefix[1] == 0xff)
        {
            preambleLength = 2;
            return new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
        }

        preambleLength = 0;
        return fallback;
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            _activeLink = null;
            ActiveLinkIndex = -1;
        }

        base.OnUnavailable(reason);
        if (reason == ReleaseReason.Disposed)
        {
            ScrollChanged = null;
            LinkClicked = null;
            SelectionChanged = null;
        }
    }

    #endregion
}
