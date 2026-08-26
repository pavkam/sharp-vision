// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Runtime.ExceptionServices;

using SharpVision.Controls.Input;
using SharpVision.Runtime;
using SharpVision.Terminal.Input;

/// <summary>Defines one selectable, optionally expandable entry in a <see cref="TreeView"/>.</summary>
[PublicAPI]
public sealed class TreeViewItem: ControlBase, IDispatcherAttachmentObserver
{
    // These dimensions are terminal-cell invariants for the compact tree row chrome.
    private const int _rowHeightCells = 1;
    private const int _disclosureWidthCells = 1;
    // A checkable row separates its disclosure glyph from its mark, so the two never read as one
    // cluster. Reserved only when a mark is drawn.
    private const int _checkGapCells = 1;
    // A row reserves one terminal cell between its chrome and header text.
    private const int _headerLeadingSpaceCells = 1;
    private const int _defaultIndentCells = 2;
    // Rows are not negotiated across siblings the way Menu's shared shortcut column is - each
    // row measures and renders its own affixes against its own live bounds, so this gap has no
    // shared style to come from; TreeViewItem owns it the same way it owns _checkGapCells above.
    private const int _affixGapCells = 1;
    // The indent span is filled on the stack up to this many cells; a deeper nesting than this
    // falls back to a heap array instead, so an adversarially deep tree cannot blow the stack.
    private const int _indentStackAllocLimitCells = 256;

    private bool _isSelected;
#pragma warning disable IDE0032 // Propagation assigns this field across instances.
    private bool? _isChecked = false;
#pragma warning restore IDE0032

    private CancellationTokenSource? _loadCancellation;
    private long _expandedVersion;
    private long _childStateVersion;
    private long _loadGeneration;
    private TreeViewChildState _priorChildStateBeforeLoad;
    private TreeViewStatusRow? _statusRow;
    private TaskCompletionSource? _slotWait;

    /// <summary>Gets this item's own stored check state, ignoring any descendant aggregate.</summary>
    internal bool? OwnCheckState => _isChecked;

    /// <summary>Gets or sets the stable key this item was materialized from, or null when the item
    /// was authored directly by the caller rather than by an async load.</summary>
    internal object? RemoteKey { get; set; }

    /// <summary>Initializes a tree view item with a fixed one-cell height.</summary>
    public TreeViewItem()
    {
        Height = Length.Cells(_rowHeightCells);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsFocusable = false;
        IsTabStop = false;
        Children = new TreeViewItemCollection(this);
        EnabledChanged += OnEnabledChanged;
        VisibilityChanged += OnVisibilityChanged;

        // No bundled theme authors "input.current" (see themes.md), and real keyboard focus stays
        // on the owning TreeView rather than this item (IsFocusable is false above), so
        // TreeView.MoveCurrent - the same CurrentItemNavigator mechanism ListView uses - would
        // otherwise move Current with no visible cue at all under TreeSelectionMode.None, where
        // CommitCurrent's call to ApplyInputSelection returns false before anything else renders a
        // change. A code-owned Current contribution here, mirroring ListItem's identical fix,
        // guarantees that cue independently of both theme authoring and real focus. Selected
        // (later in the fixed state-precedence order) still wins the same member if a theme ever
        // authors one, and Underline composes without disturbing Attributes.
        SetAppearance(VisualState.Current, new AppearanceOverlay(face: new FaceOverlay(underline: Underline.Straight)));
    }

    /// <summary>Initializes a tree view item with the specified header text.</summary>
    /// <param name="header">The non-null display text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is null.</exception>
    public TreeViewItem(string header)
        : this()
    {
        ArgumentNullException.ThrowIfNull(header);
        Header = header;
    }

    /// <summary>Raised after keyboard or pointer activation requests invocation.</summary>
    public event EventHandler<ActivationEventArgs>? Invoked;

    /// <summary>Raised after the <see cref="IsExpanded"/> state changes.</summary>
    public event EventHandler<ItemExpandedChangedEventArgs>? ExpandedChanged;

    /// <summary>Raised after this item or one of its descendants changes check state.</summary>
    public event EventHandler<CheckChangedEventArgs>? CheckStateChanged;

    /// <summary>Gets or sets the non-null display text.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentException.ThrowIfContainsControls(value, nameof(value), "A tree view item header cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets whether child items are visible.</summary>
    /// <remarks>
    /// Expanding an item whose <see cref="ChildState"/> is <see cref="TreeViewChildState.Unloaded"/>
    /// starts exactly one background request for its children. Collapsing an item whose
    /// <see cref="ChildState"/> is <see cref="TreeViewChildState.Loading"/> cancels that request and
    /// restores the state it had before the request started. A public observer that commits a newer
    /// expansion state supersedes the outer transition's structural and loading work. Observer
    /// failures do not prevent invariant work for the still-current transition.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public bool IsExpanded
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            var version = ++_expandedVersion;
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(IsExpanded), InvalidationImpact.None),
                ref failure);

            if (IsCurrentExpansion(version, value))
            {
                ExceptionAggregation.Capture(
                    () => ExpandedChanged?.Invoke(this, new ItemExpandedChangedEventArgs(value)),
                    ref failure);
            }

            if (IsCurrentExpansion(version, value))
            {
                ExceptionAggregation.Capture(() => FindTreeView()?.NotifyStructureChanged(), ref failure);
            }

            if (IsCurrentExpansion(version, value))
            {
                ExceptionAggregation.Capture(ApplyExpandedState, ref failure);
            }

            failure?.Throw();
        }
    } = true;

    /// <summary>Gets the child item collection.</summary>
    public TreeViewItemCollection Children { get; }

    /// <summary>Gets or sets whether this item displays and responds to a check mark.</summary>
    public bool IsCheckable
    {
        get;
        set
        {
            var previousStates = new List<(TreeViewItem Item, bool? State)>();
            for (var item = this; item is not null; item = item.ParentCollection?.ParentItem)
            {
                previousStates.Add((item, item.GetEffectiveCheckState()));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                foreach (var (item, previous) in previousStates)
                {
                    var current = item.GetEffectiveCheckState();
                    if (previous != current)
                    {
                        item.RaiseCheckStateChanged(previous, current, ActivationCause.Programmatic);
                    }
                }

                FindTreeView()?.NotifyCheckStateChanged(this);
            }
        }
    }

    /// <summary>Gets or sets the checked, unchecked, or indeterminate state.</summary>
    /// <exception cref="InvalidOperationException">The item is not checkable.</exception>
    public bool? IsChecked
    {
        get => GetEffectiveCheckState();
        set
        {
            if (!IsCheckable)
            {
                throw new InvalidOperationException("Only checkable tree items have a check state.");
            }

            SetCheckState(value, ActivationCause.Programmatic, propagate: true);
        }
    }

    /// <summary>Gets or sets the three one-cell glyphs used for check states.</summary>
    /// <remarks>
    /// A convenience projection over <see cref="ActualCheckMark"/>. Reading reports the resolved
    /// glyphs; assigning keeps the resolved mark layout and replaces only its glyphs, so the two
    /// surfaces cannot disagree about which glyphs a row draws.
    /// </remarks>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public CheckBoxGlyphs CheckGlyphs
    {
        get => ActualCheckMark.Glyphs;
        set
        {
            VerifyMutable();

            if (ActualCheckMark.Glyphs == value)
            {
                return;
            }

            CheckMark = ActualCheckMark.WithGlyphs(value);
            NotifyPropertyChanged(nameof(CheckGlyphs), InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets this item's local check-mark presentation.</summary>
    /// <remarks>
    /// Null defers to the owning <see cref="TreeView.CheckMark"/>, and then to the library default,
    /// giving the precedence local item, owning tree, library default.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public CheckMark? CheckMark
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            var previous = ActualCheckMark;
            field = value;
            var current = ActualCheckMark;

            // A width change moves the header, so it needs measure rather than a repaint.
            var impact = previous.Width != current.Width
                ? InvalidationImpact.Measure
                : previous == current
                    ? InvalidationImpact.None
                    : InvalidationImpact.Render;
            NotifyPropertyChanged(nameof(CheckMark), impact);

            if (previous != current)
            {
                NotifyPropertyChanged(nameof(ActualCheckMark), InvalidationImpact.None);
            }
        }
    }

    /// <summary>Gets the check-mark presentation this item renders.</summary>
    /// <remarks>
    /// The final fallback covers an item not yet attached to a tree, and is theme-resolved for the
    /// same reason the tree's own is: a theme's selected glyph family must reach tree rows.
    /// </remarks>
    public CheckMark ActualCheckMark =>
        CheckMark ?? FindTreeView()?.ActualCheckMark ?? TreeView.ThemedCheckMark(Theme);

    /// <summary>Redraws or remeasures after the owning tree changed the shared mark.</summary>
    /// <param name="impact">The invalidation the shared change requires.</param>
    internal void NotifyCheckMarkChanged(InvalidationImpact impact)
    {
        if (CheckMark is null && IsCheckable)
        {
            NotifyPropertyChanged(nameof(ActualCheckMark), impact);
        }
    }

    /// <summary>Gets whether this item is the tree view's selected item.</summary>
    public bool IsSelected => _isSelected;

    /// <summary>Gets whether this item has any children.</summary>
    /// <remarks>
    /// A <see cref="TreeViewChildState.Leaf"/> item never has children. A
    /// <see cref="TreeViewChildState.Loaded"/> item reports whether <see cref="Children"/> is
    /// non-empty, exactly as before <see cref="ChildSource"/> existed. Every other state -
    /// <see cref="TreeViewChildState.Unloaded"/>, <see cref="TreeViewChildState.Loading"/>, and
    /// <see cref="TreeViewChildState.LoadFailed"/> - reports true, because the disclosure affordance
    /// must stay available while the real answer is still unknown or being retried.
    /// </remarks>
    public bool HasChildren => ChildState switch
    {
        TreeViewChildState.Leaf => false,
        TreeViewChildState.Loaded => Children.Count > 0,
        TreeViewChildState.Unloaded => true,
        TreeViewChildState.Loading => true,
        TreeViewChildState.LoadFailed => true,
        _ => true
    };

    /// <summary>Gets whether this item's children are a committed, non-empty
    /// <see cref="TreeViewChildState.Loaded"/> snapshot. Check-state aggregation treats anything
    /// else - unloaded, loading, failed, or genuinely empty - as a leaf for aggregation purposes,
    /// since there is no real descendant state to aggregate yet.</summary>
    private bool HasLoadedChildren => ChildState == TreeViewChildState.Loaded && Children.Count > 0;

    /// <summary>Gets the nesting depth, set internally by the owning tree view.</summary>
    public int Depth { get; internal set; }

    /// <summary>Gets a stable preorder position among every item the owning tree view currently
    /// owns - including collapsed-but-attached branches - stamped by the owning tree view's own
    /// full-tree walk and left untouched between structural changes.</summary>
    /// <remarks>
    /// Selection bookkeeping sorts by this instead of re-walking the tree to answer "which of
    /// these selected items comes first" or "in what order did this delta occur" on every
    /// keyboard navigation keystroke. It is only meaningful while the item is owned; a detached
    /// item keeps its last stamped value, which callers must not rely on.
    /// </remarks>
    internal int TreeOrder { get; set; }

    /// <summary>Gets or sets the source this item asks for its children the first time it expands.</summary>
    /// <remarks>
    /// Null - the default - is today's eager contract: every child is authored directly through
    /// <see cref="Children"/>, and <see cref="ChildState"/> only ever reports
    /// <see cref="TreeViewChildState.Leaf"/> or <see cref="TreeViewChildState.Loaded"/>. Assigning a
    /// non-null source over children the caller authored directly is rejected; clear
    /// <see cref="Children"/> first. Assigning a different source over children a prior load already
    /// committed cancels any in-flight load and evicts (detaches and disposes) those
    /// engine-materialized children, returning to <see cref="TreeViewChildState.Unloaded"/>.
    /// Materialized non-leaf children inherit the same source instance automatically.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The value is non-null and <see cref="Children"/> already holds items the caller authored
    /// directly rather than a prior load committed, or the attached item is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public ITreeViewChildSource? ChildSource
    {
        get;
        set
        {
            VerifyMutable();

            if (ReferenceEquals(field, value))
            {
                return;
            }

            if (value is not null && Children.Count > 0 && !Children.IsLoaderOwned)
            {
                throw new InvalidOperationException(
                    "A child source cannot be assigned over children the caller authored " +
                    "directly. Clear Children first.");
            }

            if (Children.IsLoaderOwned)
            {
                CancelPendingChildLoadSubtree();
                EvictLoaderChildren();
            }

            field = value;

            if (value is null)
            {
                Children.IsLoaderOwned = false;
                SetChildState(Children.Count > 0 ? TreeViewChildState.Loaded : TreeViewChildState.Leaf);
            }
            else
            {
                Children.IsLoaderOwned = true;
                SetChildState(TreeViewChildState.Unloaded);
            }
        }
    }

    /// <summary>Gets this item's current position in the asynchronous child-loading lifecycle.</summary>
    public TreeViewChildState ChildState { get; private set; } = TreeViewChildState.Leaf;

    /// <summary>Raised after <see cref="ChildState"/> changes.</summary>
    /// <remarks>A callback may replace <see cref="ChildSource"/> or otherwise supersede the active
    /// request. The superseded request does not start or publish later structural work. A callback
    /// failure is rethrown only after the still-current request and status-row invariants complete.</remarks>
    public event EventHandler<TreeViewChildStateChangedEventArgs>? ChildStateChanged;

    /// <summary>Gets the exception from the most recently failed child request, or null.</summary>
    /// <remarks>Cleared the moment a later request succeeds.</remarks>
    public Exception? LastChildLoadError { get; private set; }

    /// <summary>Requests this item's children from <see cref="ChildSource"/> again.</summary>
    /// <remarks>
    /// Legal from any <see cref="ChildState"/>, including <see cref="TreeViewChildState.Leaf"/> and
    /// <see cref="TreeViewChildState.LoadFailed"/> - it is the supported retry entry point after a
    /// failure. The returned task completes once this request's outcome is decided (committed,
    /// failed, or superseded by a newer request) and never faults; a failure is observable only
    /// through <see cref="ChildState"/> and <see cref="LastChildLoadError"/>.
    /// </remarks>
    /// <param name="cancellationToken">A token this caller can use to cancel only this call's wait.</param>
    /// <returns>A task that never faults.</returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="ChildSource"/> is null, or the attached item is accessed off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public async Task ReloadChildrenAsync(CancellationToken cancellationToken = default)
    {
        if (ChildSource is null)
        {
            throw new InvalidOperationException(
                "ReloadChildrenAsync requires a non-null ChildSource.");
        }

        VerifyMutable();

        if (Dispatcher is null || !Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "ReloadChildrenAsync requires an item attached to a running dispatcher.");
        }

        var observation = BeginLoad();

        if (observation is not null)
        {
            await observation.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Commits the visual selected state from the containing tree view.</summary>
    internal void CommitSelection(bool value) =>
        _ = SetVisualStateProperty(ref _isSelected, value, nameof(IsSelected));

    /// <summary>Activates this item on behalf of its focus-owning tree view.</summary>
    internal void ActivateFromOwner(ActivationCause cause) =>
        Invoked?.Invoke(this, new ActivationEventArgs(cause));

    /// <inheritdoc/>
    protected override bool IsSelectedState => _isSelected;

    /// <inheritdoc/>
    protected override bool IsCheckedState => IsChecked == true;

    /// <inheritdoc/>
    protected override bool IsIndeterminateState => IsChecked is null && IsCheckable;

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inboard of the
    /// disclosure and check glyphs and outboard of the header text.</summary>
    /// <remarks>
    /// Rows are not negotiated across siblings the way <c>Menu</c>'s shared shortcut column is -
    /// this item measures and renders its own affixes against its own live bounds only, exactly
    /// as every other per-row detail already does.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved inside the row
    /// and outside the header text's own alignment box.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    private Rune DisclosureGlyph => IsExpanded
        ? FindTreeView()?.ActualStyle.ExpandedGlyph ?? TreeViewStyle.Default.ExpandedGlyph
        : FindTreeView()?.ActualStyle.CollapsedGlyph ?? TreeViewStyle.Default.CollapsedGlyph;

    // The code-owned portable repair value for whichever glyph DisclosureGlyph currently reads -
    // the same registry TreeViewStyle.Complete seeded the themable value from in the first place.
    private Rune DisclosureGlyphFallback => IsExpanded
        ? ControlGlyphs.Disclosure.Expanded.Fallback
        : ControlGlyphs.Disclosure.Collapsed.Fallback;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var indent = FindTreeView()?.Indent ?? _defaultIndentCells;

        // Must match OnRenderContent exactly: indent, one disclosure cell, a gap and the resolved
        // mark when checkable, then one leading space before the header, plus whatever columns
        // the start and end affixes reserve straddling the header.
        var checkWidth = IsCheckable ? _checkGapCells + ActualCheckMark.Width : 0;
        var affixes = MeasureAffixes(StartAffix, EndAffix, _affixGapCells);
        return new Size(
            (int) Math.Min(
                int.MaxValue,
                ((long) Depth * indent) +
                _disclosureWidthCells +
                checkWidth +
                _headerLeadingSpaceCells +
                MeasureCells(Header) +
                affixes.StartCells +
                affixes.EndCells),
            _rowHeightCells);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;

        if (this.HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(Bounds, style);
        }

        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var indent = FindTreeView()?.Indent ?? _defaultIndentCells;
        var indentCells = Depth * indent;
        var clipped = canvas.Clip(bounds);

        var x = bounds.X;

        if (indentCells > 0)
        {
            var indentSpan = indentCells <= _indentStackAllocLimitCells
                ? stackalloc char[indentCells]
                : new char[indentCells];
            indentSpan.Fill(' ');
            var leading = clipped.Draw(
                indentSpan,
                new Point(x, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
            x = leading.Final.X;
        }

        if (HasChildren)
        {
            canvas.DrawRune(
                DisclosureGlyph.Resolve(DisclosureGlyphFallback, CellPolicy.AmbiguousWidth),
                new Point(x, bounds.Y),
                style,
                BackgroundMode.Transparent);
            x += _disclosureWidthCells;
        }
        else
        {
            var leading = clipped.Draw(
                " ".AsSpan(),
                new Point(x, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
            x = leading.Final.X;
        }

        if (IsCheckable)
        {
            // The presenter owns bracket construction and per-state fallback, so a one-cell and a
            // three-cell family degrade identically here and in a standalone CheckBox. The leading
            // space keeps the disclosure glyph and the mark from reading as one cluster.
            var mark = ActualCheckMark;
            var cells = mark.Format(IsChecked, CellPolicy.AmbiguousWidth);
            Span<char> gapSpan = stackalloc char[_checkGapCells];
            gapSpan.Fill(' ');
            var gap = clipped.Draw(
                gapSpan,
                new Point(x, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
            _ = clipped.Draw(
                cells.AsSpan(),
                new Point(gap.Final.X, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
            x += _checkGapCells + mark.Width;
        }

        // Affixes sit strictly inboard of the disclosure/check glyphs and outboard of the header
        // text: [indent][disclosure][check][start][gap] header [gap][end]. Measured and drawn
        // against this row's own live bounds only - rows are not negotiated across siblings the
        // way Menu's shared shortcut column is.
        var affixBox = new Rect(x, bounds.Y, Math.Max(0, bounds.Right - x), _rowHeightCells);
        var affixes = MeasureAffixes(StartAffix, EndAffix, _affixGapCells);
        RenderAffixes(clipped, affixBox, affixes, StartAffix, EndAffix, style);
        var headerBox = DeflateForAffixes(affixBox, affixes);

        if (headerBox.Width > _headerLeadingSpaceCells)
        {
            var headerClipped = canvas.Clip(new Rect(headerBox.X, bounds.Y, headerBox.Width, _rowHeightCells));
            var headerLeading = headerClipped.Draw(
                " ".AsSpan(),
                new Point(headerBox.X, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
            _ = headerClipped.Draw(
                Header.AsSpan(),
                new Point(headerLeading.Final.X, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        base.OnEvent(eventArgs);

        if (eventArgs.IsHandled)
        {
            return;
        }

        if (eventArgs is PointerEventArgs
            {
                Pointer: var pointer,
                Pointer.Action: PointerAction.Press,
                Pointer.Buttons: var buttons,
                Pointer.Cells: { } cells
            } && (buttons & Buttons.Primary) != 0 && Bounds.Contains(cells))
        {
            var indent = FindTreeView()?.Indent ?? _defaultIndentCells;
            var glyphX = ContentBounds.X + (Depth * indent);

            if (HasChildren && cells.X == glyphX)
            {
                IsExpanded = !IsExpanded;
                eventArgs.IsHandled = true;
                return;
            }

            var checkX = glyphX + _disclosureWidthCells + _checkGapCells;

            // A bracket mark occupies three cells, so any cell of the mark toggles it. Testing
            // only the first cell made two thirds of a bracket mark unclickable.
            if (IsCheckable && cells.X >= checkX && cells.X < checkX + ActualCheckMark.Width)
            {
                SetCheckState(IsChecked != true, ActivationCause.Pointer, propagate: true);
                eventArgs.IsHandled = true;
                return;
            }

            LastModifiers = pointer.Modifiers;
            FindTreeView()?.NotifyItemInvoked(this, ActivationCause.Pointer, pointer.Modifiers);
            eventArgs.IsHandled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();

        // BeginLoad no-ops while unattached because there is nowhere to post the completion back
        // to (see BeginLoad's own dispatcher-null guard). An item constructed already IsExpanded -
        // the default - with a ChildSource assigned before it ever reaches a dispatcher would
        // otherwise stay Unloaded forever: nothing else re-evaluates the IsExpanded/ChildState pair
        // once attachment supplies the dispatcher this deferred load needed.
        //
        // The retry itself is posted rather than started inline: OnAttached runs synchronously
        // inside the owning tree's own attachment transaction, and BeginLoad synchronously commits
        // ChildState, which can flatten a synthetic status row into the owned control tree - a
        // structural mutation the transaction that is still publishing this very attachment cannot
        // reenter. Posting defers it to the next dispatcher turn, after that transaction closes.
        if (IsExpanded && ChildState == TreeViewChildState.Unloaded)
        {
            var dispatcher = Dispatcher!;
            var attachmentVersion = AttachmentVersion;

            try
            {
                dispatcher.Post(() =>
                {
                    if (!IsDisposed &&
                        ReferenceEquals(Dispatcher, dispatcher) &&
                        AttachmentVersion == attachmentVersion &&
                        IsExpanded &&
                        ChildState == TreeViewChildState.Unloaded)
                    {
                        _ = BeginLoad();
                    }
                });
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <inheritdoc/>
    void IDispatcherAttachmentObserver.OnDispatcherAttached()
    {
    }

    /// <inheritdoc/>
    void IDispatcherAttachmentObserver.OnDispatcherDetached() =>
        CancelPendingChildLoadSubtree(notifyTree: false);

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);

        if (reason == ReleaseReason.Detached)
        {
            ExceptionAggregation.Capture(
                () => CancelPendingChildLoadSubtree(notifyTree: false),
                ref failure);
        }

        if (reason == ReleaseReason.Disposed)
        {
            Invoked = null;
            ExpandedChanged = null;
            CheckStateChanged = null;
            ChildStateChanged = null;
        }

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        // Disposal does not cascade to owned children in this codebase, so a subtree's pending
        // loads would otherwise keep running against items no longer reachable from any tree.
        // notifyTree: false - this hook runs inside the owning tree's own disposal transaction, so
        // a restored ChildState must not try to flatten a rebuild back into that same transaction.
        CancelPendingChildLoadSubtree(notifyTree: false);
        base.OnDisposing();
    }

    [Pure]
    internal TreeView? FindTreeView() => ParentCollection?.Owner ?? FindAncestor<TreeView>();

    internal TreeViewItemCollection? ParentCollection { get; set; }

    private void OnEnabledChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        FindTreeView()?.NotifyItemEnabledChanged(this);
    }

    private void OnVisibilityChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        FindTreeView()?.NotifyItemVisibilityChanged(this);
    }

    [ThreadStatic]
    private static List<CheckStateFrame>? _checkStateWalk;

    internal Modifiers LastModifiers { get; private set; }

    internal void SetCheckState(bool? value, ActivationCause cause, bool propagate)
    {
        var affected = CollectAffectedCheckStateItems(propagate);

        // One memoized pass per snapshot. Evaluating each affected item independently re-walked its
        // whole subtree, so a chain-shaped tree cost O(n^2) aggregations for a single toggle.
        var previousStates = EvaluateStates(affected);

        SetCheckStateCore(value, propagate);
        var currentStates = EvaluateStates(affected);

        foreach (var item in affected)
        {
            var previous = previousStates[item];
            var current = currentStates[item];

            if (previous == current)
            {
                continue;
            }

            item.RaiseCheckStateChanged(previous, current, cause);
            item.FindTreeView()?.NotifyCheckStateChanged(item);
        }
    }

    [Pure]
    private static Dictionary<TreeViewItem, bool?> EvaluateStates(List<TreeViewItem> affected)
    {
        var memo = new Dictionary<TreeViewItem, bool?>(affected.Count);

        foreach (var item in affected)
        {
            _ = EvaluateEffective(item, memo);
        }

        return memo;
    }

    private static bool? EvaluateEffective(TreeViewItem item, Dictionary<TreeViewItem, bool?> memo)
    {
        if (memo.TryGetValue(item, out var cached))
        {
            return cached;
        }

        if (!item.IsCheckable || !item.HasLoadedChildren)
        {
            memo[item] = item._isChecked;

            return item._isChecked;
        }

        // Iterative post-order sharing one memo across every affected item, so the whole snapshot
        // costs one pass over the touched nodes instead of one pass per node.
        List<CheckStateFrame> walk = [new CheckStateFrame(item)];
        bool? resolved = null;

        while (walk.Count > 0)
        {
            var index = walk.Count - 1;
            var frame = walk[index];

            if (frame.TryTakeNextCheckableChild(out var child))
            {
                walk[index] = frame;

                if (memo.TryGetValue(child, out var known))
                {
                    frame = walk[index];
                    frame.Accumulate(known);
                    walk[index] = frame;
                }
                else if (child.HasLoadedChildren)
                {
                    walk.Add(new CheckStateFrame(child));
                }
                else
                {
                    memo[child] = child._isChecked;
                    frame = walk[index];
                    frame.Accumulate(child._isChecked);
                    walk[index] = frame;
                }

                continue;
            }

            resolved = frame.Resolve();
            memo[frame.Item] = resolved;
            walk.RemoveAt(index);

            if (walk.Count > 0)
            {
                var parent = walk[^1];
                parent.Accumulate(resolved);
                walk[^1] = parent;
            }
        }

        return resolved;
    }

    private void SetCheckStateCore(bool? value, bool propagate)
    {
        _isChecked = value;

        if (!propagate)
        {
            return;
        }

        // Iterative: propagation descends a caller-controlled hierarchy.
        List<TreeViewItem> pending = [this];

        while (pending.Count > 0)
        {
            var current = pending[^1];
            pending.RemoveAt(pending.Count - 1);

            foreach (var child in current.Children)
            {
                if (child.IsCheckable)
                {
                    child._isChecked = value;
                    pending.Add(child);
                }
            }
        }
    }

    [Pure]
    private List<TreeViewItem> CollectAffectedCheckStateItems(bool propagate)
    {
        var affected = new List<TreeViewItem>();
        var seen = new HashSet<TreeViewItem>();
        AddAffectedCheckStateItem(this, affected, seen);

        if (propagate)
        {
            // Iterative: the checkable subtree is caller-controlled and can be arbitrarily deep.
            List<TreeViewItem> pending = [this];

            while (pending.Count > 0)
            {
                var current = pending[^1];
                pending.RemoveAt(pending.Count - 1);

                foreach (var child in current.Children)
                {
                    if (child.IsCheckable)
                    {
                        AddAffectedCheckStateItem(child, affected, seen);
                        pending.Add(child);
                    }
                }
            }
        }

        for (var parent = ParentCollection?.ParentItem; parent is not null; parent = parent.ParentCollection?.ParentItem)
        {
            AddAffectedCheckStateItem(parent, affected, seen);
        }

        return affected;
    }

    private static void AddAffectedCheckStateItem(
        TreeViewItem item,
        List<TreeViewItem> affected,
        HashSet<TreeViewItem> seen)
    {
        // A set instead of List.Contains: the affected set is proportional to the subtree, so the
        // linear scan made propagation quadratic in the number of checkable descendants.
        if (seen.Add(item))
        {
            affected.Add(item);
        }
    }

    private void RaiseCheckStateChanged(bool? previous, bool? current, ActivationCause cause)
    {
        InvalidateVisualState();
        NotifyPropertyChanged(nameof(IsChecked), InvalidationImpact.Render);
        CheckStateChanged?.Invoke(this, new CheckChangedEventArgs(previous, current, cause));
    }

    internal bool? GetEffectiveCheckState()
    {
        // Leaves, non-checkable items, and items whose children are not (yet, or no longer) a
        // committed Loaded snapshot answer without touching the walk buffer at all - an unloaded or
        // mid-load checkable branch has no real descendant state to aggregate, so it reports its own
        // stored state instead of a bogus aggregate over children that do not exist yet.
        if (!IsCheckable || !HasLoadedChildren)
        {
            return _isChecked;
        }

        // Iterative post-order. A checkable chain is exactly the deep case, so recursion here is
        // the same unrecoverable stack hazard as the other traversals. The buffer is thread-static
        // because controls are dispatcher-affine, so aggregation stays allocation-free once warm.
        var walk = _checkStateWalk ??= [];
        var depth = walk.Count;
        walk.Add(new CheckStateFrame(this));

        while (walk.Count > depth)
        {
            var index = walk.Count - 1;
            var frame = walk[index];

            if (frame.TryTakeNextCheckableChild(out var child))
            {
                walk[index] = frame;

                if (child.IsCheckable && child.HasLoadedChildren)
                {
                    walk.Add(new CheckStateFrame(child));
                }
                else
                {
                    frame = walk[index];
                    frame.Accumulate(child._isChecked);
                    walk[index] = frame;
                }

                continue;
            }

            var resolved = frame.Resolve();
            walk.RemoveAt(index);

            if (walk.Count > depth)
            {
                var parent = walk[^1];
                parent.Accumulate(resolved);
                walk[^1] = parent;
            }
            else
            {
                return resolved;
            }
        }

        return _isChecked;
    }

    /// <summary>
    /// Recomputes <see cref="ChildState"/> after a caller-driven structural change to
    /// <see cref="Children"/>. A no-op whenever <see cref="ChildSource"/> is non-null, because such
    /// a change can only reach here through the loader's own privileged path, which manages
    /// <see cref="ChildState"/> itself.
    /// </summary>
    internal void OnChildCollectionStructureChanged()
    {
        if (ChildSource is null)
        {
            SetChildState(Children.Count > 0 ? TreeViewChildState.Loaded : TreeViewChildState.Leaf);
        }
    }

    #region Asynchronous child loading

    /// <summary>Gets the most recently started load-observation task. Exposed only so a test can
    /// await the fire-and-forget request loop directly and prove <see cref="ObjectDisposedException"/>
    /// still guards every completion and slot-release post silently against a genuinely disposed
    /// dispatcher. This method runs off the dispatcher thread and is never awaited by production
    /// code, so a transiently full bounded post queue (<see cref="InvalidOperationException"/>) is
    /// not left to propagate out of it either - doing so would only fault this unobserved task,
    /// never reach <see cref="Dispatcher.UnhandledException"/>. It is bridged instead: the failed
    /// post is retried once with a callback whose only job is to rethrow the caught exception, so
    /// the dispatcher's own callback-failure path picks it up exactly as it would a synchronous
    /// dispatcher-callback failure. A second full queue on that retry is the deliberately accepted
    /// edge - dropped rather than retried indefinitely, leaving this task complete successfully
    /// regardless.</summary>
    internal Task? LastChildLoadObservation { get; private set; }

    /// <summary>Posts <paramref name="action"/>; a full bounded queue
    /// (<see cref="InvalidOperationException"/>) is bridged into the dispatcher's own
    /// callback-failure path by re-posting a callback that rethrows the caught exception, so a
    /// failure originating off the dispatcher thread is reported exactly like one thrown by a
    /// callback already running on it. A second full queue on that retry, or a disposed dispatcher
    /// at either attempt, is dropped silently.</summary>
    /// <param name="dispatcher">The target dispatcher.</param>
    /// <param name="action">The callback to post.</param>
    private void PostOrReportFault(Dispatcher dispatcher, Action action)
    {
        try
        {
            dispatcher.Post(action);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException exception)
        {
            PostRetryHookForTests?.Invoke();

            try
            {
                dispatcher.Post(() => throw exception);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    /// <summary>
    /// Test-only synchronization seam. When set, invoked once by <see cref="PostOrReportFault"/>
    /// immediately after a first <see cref="Dispatcher.Post(Action)"/> attempt is rejected for a
    /// full queue, but before the bridging retry attempt - letting a test deterministically free
    /// the queue slot the retry needs in the otherwise nanosecond-wide window between the two
    /// attempts, rather than racing a genuine drain. Instance-scoped, like the analogous
    /// <c>Application.DrainInputRaceHookForTests</c> seam, so parallel tests on different items
    /// cannot interfere with each other.
    /// </summary>
    internal Action? PostRetryHookForTests { get; set; }

    /// <summary>Gets whether this item currently holds a queued admission-slot wait.</summary>
    internal bool IsAwaitingLoadSlot => _slotWait is not null;

    private void SetChildState(TreeViewChildState value, bool notifyTree = true)
    {
        if (ChildState == value)
        {
            return;
        }

        var previous = ChildState;
        ChildState = value;
        var version = ++_childStateVersion;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(ChildState), InvalidationImpact.Measure),
            ref failure);

        if (IsCurrentChildState(version, value))
        {
            ExceptionAggregation.Capture(
                () => ChildStateChanged?.Invoke(this, new TreeViewChildStateChangedEventArgs(previous, value)),
                ref failure);
        }

        if (notifyTree && IsCurrentChildState(version, value))
        {
            // A status row appears or disappears exactly on a ChildState transition, so every
            // transition must be able to trigger a flatten. Inside a commit's BeginUpdate/EndUpdate
            // bracket this is a deferred no-op, folded into that one rebuild.
            ExceptionAggregation.Capture(() => FindTreeView()?.NotifyStructureChanged(), ref failure);
        }

        failure?.Throw();
    }

    private Task? BeginLoad()
    {
        var dispatcher = Dispatcher;

        if (dispatcher is null)
        {
            // Not attached to a running dispatcher - there is nowhere to post the completion back
            // to. The load is deferred rather than attempted; ChildState stays exactly as it was.
            return null;
        }

        // Cancel only this item's own previous in-flight request, if any. Descendant requests are
        // independent: a reused child keeps its own pending load running, and an evicted child's
        // load is cancelled by its own disposal.
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();

        var generation = ++_loadGeneration;
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _priorChildStateBeforeLoad = ChildState;

        var tree = FindTreeView();
        var context = new TreeViewChildContext(RemoteKey, Header);
        var source = ChildSource ?? throw new InvalidOperationException(
            "BeginLoad requires a non-null ChildSource.");
        var cancellationToken = cancellation.Token;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => SetChildState(TreeViewChildState.Loading), ref failure);
        Task? observation = null;

        if (IsCurrentLoad(dispatcher, source, generation, cancellation))
        {
            observation = RunLoadAsync(dispatcher, tree, context, source, generation, cancellationToken);
            LastChildLoadObservation = observation;
        }

        failure?.Throw();
        return observation;
    }

    private bool IsCurrentExpansion(long version, bool value) =>
        !IsDisposed && _expandedVersion == version && IsExpanded == value;

    private bool IsCurrentChildState(long version, TreeViewChildState value) =>
        !IsDisposed && _childStateVersion == version && ChildState == value;

    private bool IsCurrentLoad(
        Dispatcher dispatcher,
        ITreeViewChildSource source,
        long generation,
        CancellationTokenSource cancellation) =>
        !IsDisposed &&
        ReferenceEquals(Dispatcher, dispatcher) &&
        ReferenceEquals(ChildSource, source) &&
        _loadGeneration == generation &&
        ReferenceEquals(_loadCancellation, cancellation) &&
        ChildState == TreeViewChildState.Loading;

    private void ApplyExpandedState()
    {
        if (IsExpanded && ChildState == TreeViewChildState.Unloaded)
        {
            _ = BeginLoad();
        }
        else if (!IsExpanded && ChildState == TreeViewChildState.Loading)
        {
            CancelOwnLoadAndRestorePriorState();
        }
    }

    private async Task RunLoadAsync(
        Dispatcher dispatcher,
        TreeView? tree,
        TreeViewChildContext context,
        ITreeViewChildSource source,
        long generation,
        CancellationToken cancellationToken)
    {
        var slotAcquired = false;
        TaskCompletionSource? wait = null;

        try
        {
            if (tree is not null)
            {
                if (tree.RequestLoadSlot(this))
                {
                    slotAcquired = true;
                }
                else
                {
                    wait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _slotWait = wait;
                    await wait.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    slotAcquired = true;
                }
            }

            var result = await source.GetChildrenAsync(context, cancellationToken).ConfigureAwait(false);

            PostOrReportFault(dispatcher, () => CommitChildLoad(dispatcher, source, generation, result));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PostOrReportFault(dispatcher, () => CommitChildLoadFailure(dispatcher, source, generation, exception));
        }
        finally
        {
            // Every await above uses ConfigureAwait(false), so this finally block - like the
            // success and failure branches immediately above it - can run on whatever thread the
            // last awaited operation completed on, not the dispatcher. _slotWait is a plain field
            // and ReleaseLoadSlot mutates the owning tree's unsynchronized admission bookkeeping
            // (and, transitively, another item's _slotWait via GrantQueuedLoadSlot), so both must
            // be posted back rather than touched inline here.
            PostOrReportFault(dispatcher, () =>
            {
                // Only clear the field if it still refers to this call's own wait handle. A
                // collapse followed immediately by a re-expand, before this posted cleanup
                // runs, can already have overwritten _slotWait with a newer generation's live
                // handle; unconditionally nulling it here would strand that generation
                // awaiting a TaskCompletionSource nobody will ever complete.
                if (ReferenceEquals(Dispatcher, dispatcher) &&
                    wait is not null &&
                    ReferenceEquals(_slotWait, wait))
                {
                    _slotWait = null;
                }

                if (slotAcquired)
                {
                    tree?.ReleaseLoadSlot();
                }
            });
        }
    }

    /// <summary>Grants a previously queued admission-slot wait, invoked by the owning tree view
    /// once a concurrent load completes and frees capacity.</summary>
    internal void GrantQueuedLoadSlot()
    {
        var wait = _slotWait;
        _slotWait = null;
        _ = wait?.TrySetResult();
    }

    private void CommitChildLoad(
        Dispatcher dispatcher,
        ITreeViewChildSource source,
        long generation,
        IReadOnlyList<TreeViewChildDescription>? result)
    {
        if (generation != _loadGeneration ||
            IsDisposed ||
            !ReferenceEquals(Dispatcher, dispatcher) ||
            !ReferenceEquals(ChildSource, source))
        {
            return;
        }

        if (!TryValidate(result, out var failure))
        {
            CommitChildLoadFailure(dispatcher, source, generation, failure);
            return;
        }

        var tree = FindTreeView();
        tree?.BeginUpdate();

        try
        {
            ApplyCommittedChildren(result);
            // Cleared before the transition publishes, so a ChildStateChanged observer reacting to
            // Loaded never reads the previous request's stale error.
            LastChildLoadError = null;
            SetChildState(TreeViewChildState.Loaded);
        }
        finally
        {
            tree?.EndUpdate();
        }

        ReleaseCompletedLoad();
    }

    private void CommitChildLoadFailure(
        Dispatcher dispatcher,
        ITreeViewChildSource source,
        long generation,
        Exception exception)
    {
        if (generation != _loadGeneration ||
            IsDisposed ||
            !ReferenceEquals(Dispatcher, dispatcher) ||
            !ReferenceEquals(ChildSource, source))
        {
            return;
        }

        // Children are deliberately left untouched: a failed reload of a populated node keeps
        // showing its stale-but-real children, and the error/retry row only appears once
        // Children.Count is zero. The error is published before the transition, matching Loaded's
        // own ordering, so a ChildStateChanged observer reacting to LoadFailed already sees it.
        LastChildLoadError = exception;
        SetChildState(TreeViewChildState.LoadFailed);
        ReleaseCompletedLoad();
    }

    private bool TryValidate(
        [NotNullWhen(true)] IReadOnlyList<TreeViewChildDescription>? result,
        [NotNullWhen(false)] out Exception? failure)
    {
        failure = null;

        if (result is null)
        {
            failure = new InvalidOperationException("A tree view child source returned a null result.");
            return false;
        }

        // Every description's Key is non-null by construction (TreeViewChildDescription's
        // constructor already rejects a null key), so only a null description element itself needs
        // an explicit check here.
        var seenKeys = new HashSet<object>();
        var ancestorKeys = new HashSet<object>();

        for (var ancestor = this; ancestor is not null; ancestor = ancestor.ParentCollection?.ParentItem)
        {
            if (ancestor.RemoteKey is { } key)
            {
                _ = ancestorKeys.Add(key);
            }
        }

        foreach (var description in result)
        {
            if (description is null)
            {
                failure = new InvalidOperationException("A tree view child source returned a null description.");
                return false;
            }

            if (!seenKeys.Add(description.Key))
            {
                failure = new InvalidOperationException(
                    "A tree view child source returned duplicate keys for one request.");
                return false;
            }

            if (ancestorKeys.Contains(description.Key))
            {
                failure = new InvalidOperationException(
                    "A tree view child source returned a key that would create a cycle with an ancestor.");
                return false;
            }

            try
            {
                ArgumentException.ThrowIfContainsControls(description.Header, nameof(description.Header), "A tree view child description header cannot contain terminal controls.");
            }
            catch (ArgumentException exception)
            {
                failure = exception;
                return false;
            }
        }

        return true;
    }

    private void ApplyCommittedChildren(IReadOnlyList<TreeViewChildDescription> result)
    {
        var previousByKey = new Dictionary<object, TreeViewItem>(Children.Count);

        foreach (var child in Children)
        {
            if (child.RemoteKey is { } key)
            {
                previousByKey[key] = child;
            }
        }

        var next = new List<TreeViewItem>(result.Count);
        var reused = new HashSet<TreeViewItem>(ReferenceEqualityComparer.Instance);

        foreach (var description in result)
        {
            if (previousByKey.TryGetValue(description.Key, out var existing))
            {
                // Instance identity is the stable-key mechanism: IsExpanded, checked state, and
                // selection all live on the reused instance and are therefore preserved for free.
                existing.Header = description.Header;
                existing.IsCheckable = description.IsCheckable;

                if (existing.ChildState == TreeViewChildState.Unloaded)
                {
                    ApplyPresence(existing, description.Presence);
                }

                next.Add(existing);
                _ = reused.Add(existing);
            }
            else
            {
                next.Add(MaterializeChild(description));
            }
        }

        List<TreeViewItem>? evicted = null;

        foreach (var child in Children)
        {
            if (!reused.Contains(child))
            {
                (evicted ??= []).Add(child);
            }
        }

        Children.LoaderReplace(next);

        if (evicted is not null)
        {
            foreach (var child in evicted)
            {
                child.Dispose();
            }
        }
    }

    private TreeViewItem MaterializeChild(TreeViewChildDescription description)
    {
        var child = new TreeViewItem(description.Header)
        {
            RemoteKey = description.Key,
            IsCheckable = description.IsCheckable,
            // Overrides TreeViewItem's own IsExpanded-by-default: a freshly materialized non-leaf
            // child inherits the same ChildSource (see ApplyPresence below), and that source's
            // owning item is very often already attached to a running dispatcher - starting
            // IsExpanded would immediately cascade another request through the attach-retry hook,
            // recursively materializing every reachable descendant the moment any ancestor loads,
            // rather than the one level a caller actually asked for.
            IsExpanded = false
        };

        if (description.IsCheckable)
        {
            // Assigned directly rather than through SetCheckState: the child is not attached yet,
            // so there is nothing to notify and no aggregate to recompute.
            child._isChecked = description.InitialCheckState ?? OwnCheckState;
        }

        ApplyPresence(child, description.Presence);
        return child;
    }

    private void ApplyPresence(TreeViewItem child, TreeViewChildPresence presence) =>
        child.ChildSource = presence == TreeViewChildPresence.Leaf ? null : ChildSource;

    private void EvictLoaderChildren()
    {
        if (Children.Count == 0)
        {
            return;
        }

        List<TreeViewItem> evicted = [.. Children];
        Children.LoaderReplace([]);

        foreach (var child in evicted)
        {
            child.Dispose();
        }
    }

    private void CancelOwnLoadAndRestorePriorState(bool notifyTree = true)
    {
        _loadGeneration++;
        var cancellation = _loadCancellation;
        _loadCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
        SetChildState(_priorChildStateBeforeLoad, notifyTree);
    }

    /// <summary>Cancels this item's own pending load, if any, and every descendant's, iteratively.
    /// Disposal does not cascade to owned children in this codebase, so this is the explicit
    /// substitute that keeps a detached or disposed subtree from completing loads no one can
    /// observe.</summary>
    /// <param name="notifyTree">
    /// Whether a cancelled item's restored state may flatten a rebuild through the owning tree.
    /// False from <see cref="OnDisposing"/> only: that call already runs inside the owning tree's
    /// own disposal transaction, which a structural rebuild cannot reenter, and nothing can observe
    /// a disposed item's <see cref="ChildState"/> afterward regardless.
    /// </param>
    internal void CancelPendingChildLoadSubtree(bool notifyTree = true)
    {
        // Iterative: the subtree depth is caller-controlled.
        List<TreeViewItem> pending = [this];

        while (pending.Count > 0)
        {
            var current = pending[^1];
            pending.RemoveAt(pending.Count - 1);

            if (current.ChildState == TreeViewChildState.Loading)
            {
                current.CancelOwnLoadAndRestorePriorState(notifyTree);
            }

            foreach (var child in current.Children)
            {
                pending.Add(child);
            }
        }
    }

    private void ReleaseCompletedLoad()
    {
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    /// <summary>Gets the cached synthetic row standing in for this item's children, or null when
    /// none is needed for the current <see cref="ChildState"/>.</summary>
    internal TreeViewStatusRow? GetStatusRowIfNeeded()
    {
        if (ChildState == TreeViewChildState.Loading)
        {
            var row = _statusRow ??= new TreeViewStatusRow(this);
            row.Kind = TreeViewStatusRowKind.Loading;
            return row;
        }

        if (ChildState == TreeViewChildState.LoadFailed && Children.Count == 0)
        {
            var row = _statusRow ??= new TreeViewStatusRow(this);
            row.Kind = TreeViewStatusRowKind.LoadFailed;
            return row;
        }

        return null;
    }

    #endregion
}
