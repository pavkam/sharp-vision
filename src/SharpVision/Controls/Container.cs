// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Scrolling;

using SharpVision.Terminal.Input;

using SharpVision.Text;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Defines a mutable control that owns an ordered child collection.</summary>
[PublicAPI]
public abstract class Container: ControlBase
{
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;

    /// <summary>Initializes an empty ordered child collection.</summary>
    protected Container() : this(int.MaxValue)
    {
    }

    /// <summary>Initializes an empty ordered child collection with a finite capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    protected Container([NonNegativeValue] int capacity)
    {
        Children = new ControlCollection(this, capacity);
        Children.Changed += OnChildrenChangedCore;
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.PartDefinition,
            nameof(ScrollBarStyle));
    }

    /// <summary>Gets the owned ordered children.</summary>
    public ControlCollection Children { get; }

    /// <inheritdoc/>
    public override SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();
        return SelectableTextAggregation.Create(this);
    }

    /// <inheritdoc/>
    internal override bool AddSelectableTextChildren(List<ControlBase> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        children.AddRange(Children);
        return true;
    }

    /// <inheritdoc/>
    internal override Rect ResolveSelectableTextDescendantClip(Rect inheritedClip)
    {
        var clip = base.ResolveSelectableTextDescendantClip(inheritedClip);
        return AutoScroll ? clip.Intersect(_scroll.ViewportBounds) : clip;
    }

    /// <summary>Responds after one complete <see cref="Children"/> mutation structurally commits.</summary>
    /// <remarks>
    /// The callback also runs after child-initiated disposal. It runs during guarded structural
    /// publication, so reentrant ownership mutation is rejected. Throwing does not roll back the
    /// committed mutation. This mirrors <see cref="ItemsControl.OnItemControlsChanged"/>, which
    /// consumes the identical internal notification on its own private presentation host.
    /// </remarks>
    protected virtual void OnChildrenChanged()
    {
    }

    private void OnChildrenChangedCore() => OnChildrenChanged();

    /// <summary>Measures the concrete container's public children within the supplied content constraint.</summary>
    /// <param name="constraint">The non-negative content-box constraint.</param>
    /// <returns>The non-negative intrinsic content size.</returns>
    protected abstract override Size MeasureOverride(Constraint constraint);

    /// <summary>Arranges the concrete container's public children within the committed content box.</summary>
    /// <param name="bounds">The non-negative content-box rectangle.</param>
    protected abstract override void ArrangeOverride(Rect bounds);

    /// <inheritdoc/>
    internal override bool ClipsDescendantVisualOverflow => AutoScroll;

    /// <inheritdoc/>
    internal override ControlBase? HitTest(Point point)
    {
        if (!CanHitTestSelf(point, requireContainment: false))
        {
            return null;
        }

        if (HitTestPopup(point) is { } popup)
        {
            return popup;
        }

        var contains = Bounds.Contains(point);

        if (!contains && (AutoScroll || ClipsChildren))
        {
            return null;
        }

        if (AutoScroll)
        {
            var bar = _scroll.Bars is not null
                ? _scroll.Vertical!.HitTest(point) ?? _scroll.Horizontal!.HitTest(point)
                : null;

            return bar ?? (_scroll.ViewportBounds.Contains(point) ? HitTestChildren(point) : null) ?? this;
        }

        return HitTestChildren(point) ?? (contains ? this : null);
    }

    private ControlBase? HitTestChildren(Point point)
    {
        for (var index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].HitTest(point) is { } child)
            {
                return child;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas, Rect contentClip)
    {
        if (!AutoScroll)
        {
            RenderContent(canvas, contentClip);
            return;
        }

        var viewportCanvas = canvas.Clip(_scroll.ViewportBounds);
        var viewportClip = contentClip.Intersect(_scroll.ViewportBounds);
        RenderContent(viewportCanvas, viewportClip);
        _scroll.Horizontal?.Render(canvas, contentClip);
        _scroll.Vertical?.Render(canvas, contentClip);
    }

    /// <inheritdoc/>
    internal override void RenderContent(TerminalCanvas canvas, Rect contentClip)
    {
        foreach (var child in Children)
        {
            if (child.RendersInNormalLayer)
            {
                child.Render(canvas, contentClip);
            }
        }
    }

    #region Grow and shrink

    /// <summary>Gets or sets whether this container sizes its border box to its content, overriding stretch and star sizing.</summary>
    /// <remarks>Honors <see cref="ControlBase.MinWidth"/>/<see cref="ControlBase.MaxWidth"/> and the height equivalents. See <see cref="AutoSizeMode"/>.</remarks>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool AutoSize
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets whether an auto-sizing axis may shrink below its explicit fixed-cell size.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public AutoSizeMode AutoSizeMode
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The auto-size mode is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = AutoSizeMode.GrowAndShrink;

    /// <inheritdoc/>
    internal override bool ShrinkWrapsWidth => AutoSize;

    /// <inheritdoc/>
    internal override bool ShrinkWrapsHeight => AutoSize;

    /// <summary>The corrected content-box size from the second AutoSize measure pass, or null
    /// when no axis was clamped below its natural size and the first, unbounded pass's
    /// <see cref="ControlBase.ContentExtent"/> remains authoritative. That property itself
    /// always retains the first pass's value - <see cref="ControlBase.Measure"/> commits it
    /// immediately after <see cref="ControlBase.MeasureOverride"/> returns, before
    /// <see cref="OnMeasuredDesired"/> ever runs - so this field is the only place the corrected
    /// value can live for <see cref="ResolveContentSlot"/> to consult afterward.
    /// Reset at the start of every measure pass.</summary>
    private Size? _autoSizeCorrectedContentExtent;

    // AutoSize sizes to content on both axes, so content is measured unbounded
    // (unclamped by an explicit Width/Height) to discover its natural size.
    /// <inheritdoc/>
    internal override Constraint OnMeasuringContent(Constraint content)
    {
        _autoSizeCorrectedContentExtent = null;

        if (AutoSize)
        {
            return new Constraint(null, null);
        }

        if (!AutoScroll)
        {
            return content;
        }

        var scrollsHorizontally = (ScrollBars & ScrollBars.Horizontal) != 0;
        var scrollsVertically = (ScrollBars & ScrollBars.Vertical) != 0;

        // Eligible axes measure unbounded so children report natural extent
        // (ResolveMeasureAxis clamps DesiredSize, which would otherwise hide overflow). A bounded
        // cross axis must also account for an always-visible reserved bar cell on the other axis,
        // or width-dependent content (wrapped text) measures at the full padded width and the
        // surplus is silently clipped once the bar narrows the committed viewport at arrange time.
        // Auto-visibility bars are handled separately by Resolve's own re-measure loop.
        var width = scrollsHorizontally
            ? null
            : ReserveBarCell(content.Width, scrollsVertically && VerticalBarVisibility == ScrollBarVisibility.Always);
        var height = scrollsVertically
            ? null
            : ReserveBarCell(content.Height, scrollsHorizontally && HorizontalBarVisibility == ScrollBarVisibility.Always);
        return new Constraint(width, height);
    }

    private static int? ReserveBarCell(int? bound, bool reserve) =>
        reserve && bound.HasValue ? Math.Max(0, bound.Value - 1) : bound;

    /// <inheritdoc/>
    internal override Size OnMeasuredDesired(Size desired)
    {
        var horizontalInset = Padding.Horizontal.Add(BorderInset.Horizontal);
        var verticalInset = Padding.Vertical.Add(BorderInset.Vertical);
        var result = !AutoSize
            ? desired
            : ResolveAutoSizeDesired(horizontalInset, verticalInset);

        if (!AutoScroll)
        {
            return result;
        }

        // ContentExtent is a content-box measurement, but result is the border-box size the bar
        // cell gets added to. Comparing the two directly under-detects overflow by exactly the
        // padding and border inset, so the content-box extent of result is computed here instead
        // Trust the AutoSize-corrected extent over the stale first-pass ContentExtent
        // once a width-driven re-measure discovered one.
        var extent = _autoSizeCorrectedContentExtent ?? ContentExtent;
        var verticalEligible = (Width.Kind == LengthKind.Auto || AutoSize) && (ScrollBars & ScrollBars.Vertical) != 0;
        var horizontalEligible = (Height.Kind == LengthKind.Auto || AutoSize) && (ScrollBars & ScrollBars.Horizontal) != 0;
        var needsVertical = verticalEligible && VerticalBarVisibility == ScrollBarVisibility.Always;
        var needsHorizontal = horizontalEligible && HorizontalBarVisibility == ScrollBarVisibility.Always;

        // Automatic bars are added monotonically because one reserved axis can induce overflow on
        // the other - see Resolve's identical two-probe rationale, which this mirrors. Two
        // additions are the finite maximum. Unlike Resolve, no content re-measure happens between
        // probes: this is only re-checking an already-final extent against a narrower viewport, not
        // revisiting content, so folding this into Resolve's own loop would need a re-measure hook
        // it never uses plus the AutoSize-or-Auto-Length eligibility gate Resolve has no notion of -
        // not a clean shared extraction.
        for (var probe = 0; probe < 2; probe++)
        {
            var viewportWidth = Math.Max(0, result.Width - horizontalInset - (needsVertical ? 1 : 0));
            var viewportHeight = Math.Max(0, result.Height - verticalInset - (needsHorizontal ? 1 : 0));
            var addVertical = verticalEligible &&
                              VerticalBarVisibility == ScrollBarVisibility.Auto &&
                              extent.Height > viewportHeight;
            var addHorizontal = horizontalEligible &&
                                HorizontalBarVisibility == ScrollBarVisibility.Auto &&
                                extent.Width > viewportWidth;
            var nextVertical = needsVertical || addVertical;
            var nextHorizontal = needsHorizontal || addHorizontal;

            if (nextVertical == needsVertical && nextHorizontal == needsHorizontal)
            {
                break;
            }

            needsVertical = nextVertical;
            needsHorizontal = nextHorizontal;
        }

        return new Size(
            needsVertical ? Math.Clamp(result.Width.Add(1), MinWidth, MaxWidth) : result.Width,
            needsHorizontal ? Math.Clamp(result.Height.Add(1), MinHeight, MaxHeight) : result.Height);
    }

    /// <summary>Resolves the AutoSize border-box size, re-measuring content once at a clamped
    /// width when Min/Max or the incoming slot shrinks it below the natural width the first,
    /// unbounded measure pass discovered.</summary>
    /// <remarks>
    /// The first pass measures unbounded specifically to discover natural size, so a wrap-capable
    /// child never reports its true wrapped height along a clamped width - it reports the single
    /// natural line MeasureOverride saw with nothing to wrap against. Clamping only the reported
    /// DesiredSize afterward, without ever re-measuring, leaves ContentExtent (the single input
    /// the scroll-extent calculation and the bar-reservation check in <see cref="OnMeasuredDesired"/>
    /// both trust) permanently describing content that was never actually arranged - content past
    /// the clamp is not merely mis-sized, it is unreachable, because AutoScroll's own overflow
    /// detection compares against that same wrong, too-small ContentExtent.
    /// <para/>
    /// Only a clamped width triggers the re-measure, mirroring Grid's own row-after-column
    /// remeasure: height depends on width through wrapping in this framework, never the reverse,
    /// so a clamped height alone (typically a scrollable axis's own MaxHeight, capping how tall
    /// the container itself gets while content simply scrolls) needs no correction. The re-measure
    /// height constraint stays unbounded regardless of MaxHeight for the same reason - bounding it
    /// would clamp the child's own reported size to that bound too
    /// (<see cref="ControlBase.MeasureOverride"/>'s slot already does this), which is
    /// exactly the artificially small figure this fix exists to avoid; only the final reported
    /// border-box height, not the measured content, is capped by MaxHeight.
    /// </remarks>
    private Size ResolveAutoSizeDesired(int horizontalInset, int verticalInset)
    {
        var natural = ContentExtent;
        var width = AutoSizeAxis(natural.Width, horizontalInset, Width, MinWidth, MaxWidth);
        var height = AutoSizeAxis(natural.Height, verticalInset, Height, MinHeight, MaxHeight);
        var clampedContentWidth = Math.Max(0, width - horizontalInset);

        if (clampedContentWidth >= natural.Width)
        {
            return new Size(width, height);
        }

        var corrected = MeasureOverride(new Constraint(clampedContentWidth, null));
        _autoSizeCorrectedContentExtent = corrected;

        return new Size(width, AutoSizeAxis(corrected.Height, verticalInset, Height, MinHeight, MaxHeight));
    }

    // GrowAndShrink fits content exactly; GrowOnly never shrinks below an explicit
    // fixed-cell size. Both honor Min/Max.
    private int AutoSizeAxis(int contentExtent, int inset, Length length, int minimum, int maximum)
    {
        Debug.Assert(contentExtent >= 0 && inset >= 0, "Auto-size inputs are non-negative cell extents.");
        Debug.Assert(minimum >= 0 && maximum >= minimum, "Auto-size limits are validated and ordered.");

        var content = (long) contentExtent + inset;
        var floor = AutoSizeMode == AutoSizeMode.GrowOnly && length.Kind == LengthKind.Cells
            ? (int) length.Value
            : 0;
        var requested = Math.Max(content, floor);
        return (int) Math.Clamp(requested, minimum, maximum);
    }

    #endregion

    #region Scrolling

    private readonly ContainerScrollController _scroll = new();
    private ulong _scrollTransitionVersion;

    /// <summary>Gets the private generated scrollbar parts for specialized container layout.</summary>
    private protected ControlCollection? Bars => _scroll.Bars;

    /// <summary>Gets the committed viewport bounds for specialized container clipping.</summary>
    private protected Rect ViewportBounds => _scroll.ViewportBounds;

    /// <summary>Raised after one or both offsets commit.</summary>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    /// <summary>Gets or sets whether this container clips and offsets overflowing content along enabled axes.</summary>
    /// <remarks>Dependent offset and generated-bar state is synchronized from the live committed
    /// value after property observers return. A reentrant observer's newer value therefore owns the
    /// complete scrolling policy.</remarks>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool AutoScroll
    {
        get;
        set => _ = SetPropertyAndContinue(ref field, value, InvalidationImpact.Measure, ApplyAutoScrollPolicy);
    }

    private void ApplyAutoScrollPolicy()
    {
        // Bars are created when scrolling is armed rather than lazily in
        // ResolveContentSlot. Lazy creation there added children
        // mid-arrange, which invalidates this container's own measure and
        // can prevent nested armed containers from ever converging to a
        // settled layout.
        if (AutoScroll)
        {
            EnsureBars();
        }
        else
        {
            // Routed through Apply rather than writing the internal offset fields directly,
            // so this reset honors the same clamp/notify/synchronize contract every other
            // offset change goes through — a subscriber tracking ScrollChanged must not
            // silently miss this change, and the generated ScrollBar parts must not go stale.
            _ = Apply(0, 0, ScrollCause.Programmatic);

            if (_scroll.Bars is not null)
            {
                SetVisibility(_scroll.Horizontal!, visible: false);
                SetVisibility(_scroll.Vertical!, visible: false);
            }
        }
    }

    /// <summary>Gets or sets the axes that may scroll within this container.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfUndefinedFlags(value, ScrollBars.Both, nameof(value), "The scrollbar axes contain unknown flags.");

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = ScrollBars.Vertical;

    /// <summary>Gets or sets the common chrome reservation policy for enabled scroll axes.</summary>
    /// <remarks>Both axis policies are synchronized from the live committed value after property
    /// observers return. A reentrant observer's newer common policy therefore owns both axes.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return field;
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The enum value is unknown.");

            _ = SetPropertyAndContinue(ref field, value, InvalidationImpact.Measure, ApplyShowScrollBarsPolicy);
        }
    } = ShowScrollBars.WhenNeeded;

    private void ApplyShowScrollBarsPolicy()
    {
        var visibility = ShowScrollBars switch
        {
            ShowScrollBars.Never => ScrollBarVisibility.Hidden,
            ShowScrollBars.WhenNeeded => ScrollBarVisibility.Auto,
            ShowScrollBars.Always => ScrollBarVisibility.Always,
            _ => throw new UnreachableException()
        };
        HorizontalBarVisibility = visibility;
        VerticalBarVisibility = visibility;
    }

    /// <summary>Gets or sets horizontal bar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ScrollBarVisibility HorizontalBarVisibility
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The enum value is unknown.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = ScrollBarVisibility.Auto;

    /// <summary>Gets or sets vertical bar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ScrollBarVisibility VerticalBarVisibility
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The enum value is unknown.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = ScrollBarVisibility.Auto;

    /// <summary>Gets or sets the complete local style shared by both generated bars.</summary>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _scrollBarStyle.Local;
        set => _scrollBarStyle.Local = value;
    }

    /// <summary>Gets the complete local or theme-resolved generated-bar style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => _scrollBarStyle.Actual;

    /// <summary>Gets or sets the non-negative arrow and wheel change in cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    [NonNegativeValue]
    public int LineSize
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return field;
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (SetProperty(ref field, value, InvalidationImpact.None))
            {
                Synchronize();
            }
        }
    } = 1;

    /// <summary>Gets or sets the non-negative cells retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    [NonNegativeValue]
    public int PageOverlap
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return field;
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (SetProperty(ref field, value, InvalidationImpact.None))
            {
                Synchronize();
            }
        }
    }

    /// <summary>Gets the committed non-negative content extent.</summary>
    public Size Extent => _scroll.Extent;

    /// <summary>Gets the committed non-negative visible extent.</summary>
    public Size Viewport => _scroll.Viewport;

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public int HorizontalOffset
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _scroll.HorizontalOffset;
        }
        set
        {
            ValidateOffset(value, MaximumX(), nameof(value));
            _ = Apply(value, VerticalOffset, ScrollCause.Programmatic);
        }
    }

    /// <summary>Gets or sets the valid vertical content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public int VerticalOffset
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _scroll.VerticalOffset;
        }
        set
        {
            ValidateOffset(value, MaximumY(), nameof(value));
            _ = Apply(HorizontalOffset, value, ScrollCause.Programmatic);
        }
    }

    /// <summary>Adds signed axis deltas with saturation and endpoint clamping.</summary>
    /// <param name="x">The requested horizontal delta.</param>
    /// <param name="y">The requested vertical delta.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool ScrollBy(int x, int y, ScrollCause cause = ScrollCause.Programmatic)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause, nameof(cause), "The enum value is unknown.");
        VerifyMutable();
        return Apply(HorizontalOffset.Add(x), VerticalOffset.Add(y), cause);
    }

    /// <summary>Scrolls vertically by a signed delta, clamped against a caller-supplied maximum
    /// instead of the committed <see cref="Extent"/>.</summary>
    /// <remarks>
    /// <see cref="Extent"/> only refreshes on a real layout pass, so a caller that mutates content
    /// and must compensate the offset before that pass ever runs - a fixed-row-height virtualized
    /// panel reacting to an item count change, for instance - would otherwise have its compensation
    /// clamped against a stale, pre-mutation bound. This lets such a caller supply the true
    /// post-mutation maximum it can already compute arithmetically, bypassing the stale <see
    /// cref="Extent"/> entirely for this one call.
    /// </remarks>
    /// <param name="y">The signed vertical delta to add to the current offset before clamping.</param>
    /// <param name="maximumY">The caller-computed non-negative upper bound for the result.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when the vertical offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    internal bool ScrollByKnownMaximum(int y, int maximumY, ScrollCause cause)
    {
        Debug.Assert(maximumY >= 0, "A known maximum is a non-negative offset bound.");
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause, nameof(cause), "The enum value is unknown.");
        VerifyMutable();
        return Apply(HorizontalOffset, VerticalOffset.Add(y), cause, maximumY);
    }

    /// <summary>Scrolls minimally to expose one descendant of this container, walking and
    /// revealing through any intervening armed container along the way.</summary>
    /// <param name="descendant">The non-null descendant control.</param>
    /// <returns>
    /// True when the descendant's complete arranged bounds end up contained within this
    /// container's viewport; false when clamping at an extent boundary - here or in an
    /// intervening armed container - leaves any part of it still outside.
    /// </returns>
    /// <remarks>An oversized descendant exposes the nearest edge when wholly outside, then keeps
    /// an already-visible slice stable across later arranged passes.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="descendant"/> is null.</exception>
    /// <exception cref="ArgumentException">The control is not a descendant of this container.</exception>
    /// <exception cref="InvalidOperationException">The attached container is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool BringIntoView(ControlBase descendant)
    {
        ArgumentNullException.ThrowIfNull(descendant);
        VerifyMutable();

        if (!IsContentDescendant(descendant))
        {
            throw new ArgumentException("The control must be a descendant of this container.", nameof(descendant));
        }

        // descendant.Bounds is already translated by every intervening armed container's own
        // scroll offset (ResolveContentSlot shifts each arranged child by -Offset), so mixing
        // that composed position directly into this container's own logical math would combine
        // two unrelated coordinate systems. Each intervening Container { AutoScroll: true } is
        // asked to reveal first, innermost to outermost, and the tracked bounds are adjusted by
        // the exact delta each commits - offsets take effect immediately even though the
        // resulting Arrange is deferred, so Bounds itself cannot be re-read mid-walk.
        var bounds = descendant.Bounds;

        for (var ancestor = descendant.Parent; ancestor is not null && !ReferenceEquals(ancestor, this);
             ancestor = ancestor.Parent)
        {
            // Matches PropagateScroll's own ancestor walk (see Ancestor(ControlBase)): a modal
            // boundary between descendant and this container must stop the walk here too, so an
            // intervening AutoScroll container outside descendant's modal scope is never
            // auto-scrolled on its behalf.
            if (descendant.ModalityOwner?.Allows(ancestor) == false)
            {
                break;
            }

            if (ancestor is not Container { AutoScroll: true } scrollable)
            {
                continue;
            }

            var beforeX = scrollable.HorizontalOffset;
            var beforeY = scrollable.VerticalOffset;
            var scrollableLogicalX = bounds.X.SaturatingSubtract(scrollable.ViewportBounds.X).Add(beforeX);
            var scrollableLogicalY = bounds.Y.SaturatingSubtract(scrollable.ViewportBounds.Y).Add(beforeY);
            var revealX = Reveal(beforeX, scrollable.Viewport.Width, scrollableLogicalX, bounds.Width);
            var revealY = Reveal(beforeY, scrollable.Viewport.Height, scrollableLogicalY, bounds.Height);
            _ = scrollable.Apply(revealX, revealY, ScrollCause.BringIntoView);

            bounds = new Rect(
                bounds.X.Add(beforeX - scrollable.HorizontalOffset),
                bounds.Y.Add(beforeY - scrollable.VerticalOffset),
                bounds.Width,
                bounds.Height);
        }

        var logicalX = bounds.X.SaturatingSubtract(_scroll.ViewportBounds.X).Add(HorizontalOffset);
        var logicalY = bounds.Y.SaturatingSubtract(_scroll.ViewportBounds.Y).Add(VerticalOffset);
        var x = Reveal(HorizontalOffset, Viewport.Width, logicalX, bounds.Width);
        var y = Reveal(VerticalOffset, Viewport.Height, logicalY, bounds.Height);
        _ = Apply(x, y, ScrollCause.BringIntoView);

        return logicalX >= HorizontalOffset && logicalX.Add(bounds.Width) <= HorizontalOffset.Add(Viewport.Width) &&
               logicalY >= VerticalOffset && logicalY.Add(bounds.Height) <= VerticalOffset.Add(Viewport.Height);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!AutoScroll || !EffectiveIsEnabled || !EffectiveIsVisible)
        {
            base.OnEvent(eventArgs);
            return;
        }

        if (eventArgs is KeyEventArgs key)
        {
            Handle(key);
        }
        else if (eventArgs is PointerEventArgs pointer)
        {
            Handle(pointer);
        }

        if (!eventArgs.IsHandled)
        {
            base.OnEvent(eventArgs);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            ScrollChanged = null;
        }
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (!eventArgs.IsKeyDown ||
            !KeyboardModifierPolicy.MatchesCommand(eventArgs.Stroke.Modifiers, Modifiers.None))
        {
            return;
        }

        var delta = ComputeKeyScrollDelta(eventArgs.Stroke.Code);

        if (delta is null)
        {
            return;
        }

        eventArgs.IsHandled = PropagateScroll(delta.Value.X, delta.Value.Y, ScrollCause.Keyboard);
    }

    /// <summary>Maps a navigation key code to the axis-aware scroll delta <see
    /// cref="Handle(KeyEventArgs)"/> would apply, or null when the code has no scroll mapping.</summary>
    /// <remarks>
    /// Factored out so a caller that must forward a key into this container from outside normal
    /// event routing - <c>MessageBox</c>'s message area is the motivating case, since it sits as a
    /// sibling of the focused action Button rather than an ancestor on the routed path - can reuse
    /// the exact same PageUp/PageDown/Home/End/arrow-key math instead of maintaining a second copy
    /// that could drift out of sync with this one.
    /// </remarks>
    /// <param name="code">The non-null key code from the originating stroke.</param>
    /// <returns>The signed (x, y) delta, or null when <paramref name="code"/> has no mapping.</returns>
    internal (int X, int Y)? ComputeKeyScrollDelta(Code code)
    {
        // PageUp/PageDown and Home/End prefer the vertical axis - matching the pre-existing
        // vertical-only mapping for the common case - and fall back to horizontal only when this
        // container cannot scroll vertically at all, so a horizontal-only container still has a
        // fast-travel key instead of consuming all four for nothing.
        var pageAxisIsVertical = (ScrollBars & ScrollBars.Vertical) != 0 || (ScrollBars & ScrollBars.Horizontal) == 0;

        if (code == Code.Left)
        {
            return (-LineSize, 0);
        }

        if (code == Code.Right)
        {
            return (LineSize, 0);
        }

        if (code == Code.Up)
        {
            return (0, -LineSize);
        }

        if (code == Code.Down)
        {
            return (0, LineSize);
        }

        if (code == Code.PageUp)
        {
            var page = PageStep(pageAxisIsVertical ? Viewport.Height : Viewport.Width);
            return pageAxisIsVertical ? (0, -page) : (-page, 0);
        }

        if (code == Code.PageDown)
        {
            var page = PageStep(pageAxisIsVertical ? Viewport.Height : Viewport.Width);
            return pageAxisIsVertical ? (0, page) : (page, 0);
        }

        if (code == Code.Home)
        {
            return pageAxisIsVertical ? (0, -VerticalOffset) : (-HorizontalOffset, 0);
        }

        return code == Code.End
            ? pageAxisIsVertical
                ? (0, MaximumY().SaturatingSubtract(VerticalOffset))
                : (MaximumX().SaturatingSubtract(HorizontalOffset), 0)
            : null;
    }

    // Clamps the retained overlap to strictly less than the page axis extent, and floors the
    // overall result at one cell, so PageUp/PageDown always advance by at least one cell.
    // PageOverlap has no configured upper bound tying it to Viewport (only non-negativity is
    // validated), so a PageOverlap at or above the viewport extent would otherwise compute a page
    // step of exactly zero. The outer floor also covers a page axis whose Viewport extent is
    // itself zero (e.g. the only available row claimed by an always-visible cross-axis bar), which
    // the overlap clamp alone cannot fix since there is no overlap left to reduce. Either case
    // would otherwise permanently turn PageUp and PageDown into silent no-ops for that axis
    // instead of degrading to a smaller step.
    [Pure]
    private int PageStep(int extent) => Math.Max(1, extent - Math.Min(PageOverlap, Math.Max(0, extent - 1)));

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Wheel)
        {
            return;
        }

        var x = pointer.WheelX.Multiply(LineSize);
        var y = MultiplyNegative(pointer.WheelY, LineSize);

        // A wheel record is this container's to keep only when it actually moved an offset -
        // AutoScroll only decided whether the loop below ran at all, not what it accomplished; using
        // it here marked every record handled unconditionally, defeating the outside Ignore/Dismiss
        // policy documented for a scrollable leaf that changed nothing.
        eventArgs.IsHandled = PropagateScroll(x, y, ScrollCause.Wheel);
    }

    /// <summary>Offers the full scroll delta to this container, then - only when it moved no
    /// offset at all, not merely less than requested - the same full delta to each enclosing
    /// armed ancestor in turn, stopping at the modal plane. A container that moves any amount
    /// keeps the whole record for itself instead of handing a partial remainder outward within
    /// the same event: latching, not chaining, is the documented contract (see
    /// docs/concepts/scrolling.md) - an ancestor only ever sees a record whose delta
    /// produced zero movement here, whether because this axis is already at its endpoint or
    /// because nothing on it is scrollable. Shared by wheel and keyboard input.</summary>
    /// <returns>True when at least one offset changed anywhere along the walk.</returns>
    private bool PropagateScroll(int x, int y, ScrollCause cause)
    {
        for (var current = this; current is not null; current = Ancestor(current))
        {
            var previousX = current.HorizontalOffset;
            var previousY = current.VerticalOffset;
            _ = current.ScrollBy(x, y, cause);

            if (current.HorizontalOffset != previousX || current.VerticalOffset != previousY)
            {
                return true;
            }
        }

        return false;
    }

    [Pure]
    private static Container? Ancestor(ControlBase control)
    {
        Debug.Assert(control is not null, "Scrollable ancestor lookup requires a control.");

        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            if (control.ModalityOwner?.Allows(current) == false)
            {
                return null;
            }

            if (current is Container { AutoScroll: true } container)
            {
                return container;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    internal override Rect ResolveContentSlot(Rect padded)
    {
        if (!AutoScroll)
        {
            var box = new Size(padded.Width, padded.Height);
            _ = SetProperty(ref _scroll.Extent, box, InvalidationImpact.None, nameof(Extent));
            _ = SetProperty(ref _scroll.Viewport, box, InvalidationImpact.None, nameof(Viewport));
            _scroll.ViewportBounds = padded;
            _ = Apply(0, 0, ScrollCause.Programmatic);
            return padded;
        }

        if (HorizontalBarVisibility != ScrollBarVisibility.Hidden ||
            VerticalBarVisibility != ScrollBarVisibility.Hidden)
        {
            EnsureBars();
        }

        // Trust the AutoSize-corrected content extent over the stale first-pass ContentExtent
        // once a re-measure discovered one; otherwise wrapped content past a Max clamp is
        // unreachable regardless of ArrangeOverride, since the scroll extent itself never grew
        // to include it.
        var extent = Resolve(
            new Size(padded.Width, padded.Height),
            _autoSizeCorrectedContentExtent ?? ContentExtent,
            out var horizontal,
            out var vertical,
            out var viewport);
        _scroll.ViewportBounds = new Rect(padded.X, padded.Y, viewport.Width, viewport.Height);
        var extentChanged = _scroll.Extent != extent;
        _ = SetProperty(ref _scroll.Extent, extent, InvalidationImpact.None, nameof(Extent));
        _ = SetProperty(ref _scroll.Viewport, viewport, InvalidationImpact.None, nameof(Viewport));
        _scroll.ReserveHorizontal = horizontal;
        _scroll.ReserveVertical = vertical;
        _ = Apply(
            Math.Min(HorizontalOffset, MaximumX()),
            Math.Min(VerticalOffset, MaximumY()),
            extentChanged ? ScrollCause.Content : ScrollCause.Resize);

        var scrollsHorizontally = (ScrollBars & ScrollBars.Horizontal) != 0;
        var scrollsVertically = (ScrollBars & ScrollBars.Vertical) != 0;

        return new Rect(
            padded.X.SaturatingSubtract(HorizontalOffset),
            padded.Y.SaturatingSubtract(VerticalOffset),
            scrollsHorizontally ? Math.Max(Extent.Width, viewport.Width) : viewport.Width,
            scrollsVertically ? Math.Max(Extent.Height, viewport.Height) : viewport.Height);
    }

    /// <inheritdoc/>
    internal override void ArrangeOverlays(Rect padded)
    {
        if (!AutoScroll || _scroll.Bars is null)
        {
            return;
        }

        Debug.Assert(_scroll.Horizontal is not null && _scroll.Vertical is not null,
            "Created scrollbar chrome owns both axes.");

        SetVisibility(_scroll.Horizontal, _scroll.ReserveHorizontal);
        SetVisibility(_scroll.Vertical, _scroll.ReserveVertical);
        _scroll.Horizontal.Arrange(
            new Rect(padded.X, padded.Y + _scroll.ViewportBounds.Height,
                _scroll.ReserveVertical ? padded.Width : _scroll.ViewportBounds.Width,
                _scroll.ReserveHorizontal ? 1 : 0),
            widthResolved: true,
            heightResolved: true);
        _scroll.Vertical.Arrange(
            new Rect(padded.X + _scroll.ViewportBounds.Width, padded.Y, _scroll.ReserveVertical ? 1 : 0,
                _scroll.ViewportBounds.Height),
            widthResolved: true,
            heightResolved: true);
        Synchronize();
    }

    private void EnsureBars()
    {
        if (_scroll.Bars is not null)
        {
            return;
        }

        _scroll.Horizontal = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            IsFocusable = false,
            IsTabStop = false
        };
        _scroll.Vertical = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            IsFocusable = false,
            IsTabStop = false
        };
        _scroll.Horizontal.ValueChanged += OnHorizontalChanged;
        _scroll.Vertical.ValueChanged += OnVerticalChanged;
        _scroll.Bars = new ControlCollection(
            this,
            capacity: 2,
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "scroll-bars",
            InvalidationImpact.Measure)) { _scroll.Horizontal, _scroll.Vertical };

        BindStyle(_scrollBarStyle, _scroll.Horizontal);
        BindStyle(_scrollBarStyle, _scroll.Vertical);

        Debug.Assert(_scroll.Bars.Count == 2, "Scrollbar chrome owns exactly one control per axis.");
    }

    private void Synchronize(int? maximumYOverride = null)
    {
        if (_scroll.Synchronizing || _scroll.Bars is null)
        {
            return;
        }

        Debug.Assert(_scroll.Horizontal is not null && _scroll.Vertical is not null,
            "Scrollbar synchronization requires both axes.");

        _scroll.Synchronizing = true;

        try
        {
            Configure(_scroll.Horizontal, MaximumX(), Viewport.Width, HorizontalOffset);
            Configure(_scroll.Vertical, maximumYOverride ?? MaximumY(), Viewport.Height, VerticalOffset);
        }
        finally
        {
            _scroll.Synchronizing = false;
        }
    }

    private void Configure(ScrollBar bar, int maximum, int viewport, int value)
    {
        Debug.Assert(bar is not null, "Scrollbar configuration requires an owned bar.");
        Debug.Assert(maximum >= 0 && viewport >= 0, "Scrollbar geometry is non-negative.");
        Debug.Assert(value >= 0 && value <= maximum, "Scrollbar value is clamped before synchronization.");

        // ScrollBar's Maximum setter throws rather than mutate when shrinking would leave the
        // current Value outside the range, so Value is pre-clamped into the incoming maximum
        // here before Maximum itself is assigned below.
        if (bar.Value > maximum)
        {
            bar.Value = maximum;
        }

        bar.Maximum = maximum;
        bar.ViewportSize = viewport;
        bar.SmallChange = LineSize;
        bar.LargeChange = PageStep(viewport);
        bar.Value = value;
    }

    private void OnHorizontalChanged(object? sender, ScrollEventArgs eventArgs)
    {
        _ = sender;

        if (!_scroll.Synchronizing)
        {
            _ = Apply(eventArgs.Value, VerticalOffset, eventArgs.Cause);
        }
    }

    private void OnVerticalChanged(object? sender, ScrollEventArgs eventArgs)
    {
        _ = sender;

        if (!_scroll.Synchronizing)
        {
            _ = Apply(HorizontalOffset, eventArgs.Value, eventArgs.Cause);
        }
    }

    private static void SetVisibility(ControlBase control, bool visible) =>
        control.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private bool Apply(int x, int y, ScrollCause cause, int? maximumYOverride = null)
    {
        Debug.Assert(Enum.IsDefined(cause), "Scroll changes require a defined cause.");

        x = Math.Clamp(x, 0, MaximumX());
        y = Math.Clamp(y, 0, maximumYOverride ?? MaximumY());
        var previous = new Point(HorizontalOffset, VerticalOffset);

        // ResolveContentSlot (Arrange-time only) calls here to clamp a now-stale offset before
        // this pass's own ArrangeOverride ever runs - the clamped value it reads back is the one
        // that already lands in the Rect it returns, so this pass's own output already reflects
        // it. Requesting Arrange again would only ask a later pass to redo work this one already
        // did; Render is still needed so the corrected offset actually repaints. Outside Arrange
        // (a caller-driven ScrollBy/ScrollTo/BringIntoView, or a scrollbar drag), nothing is
        // mid-flight to incorporate the change, so the normal Arrange-impact path applies.
        var impact = IsArranging ? InvalidationImpact.Render : InvalidationImpact.Arrange;
        var changedX = SetProperty(ref _scroll.HorizontalOffset, x, impact, nameof(HorizontalOffset));
        var changedY = SetProperty(ref _scroll.VerticalOffset, y, impact, nameof(VerticalOffset));

        if (!changedX && !changedY)
        {
            return false;
        }

        // Synchronize's own MaximumY() read would otherwise re-derive the stale bound the y-clamp
        // above was deliberately overridden to avoid, handing the generated vertical ScrollBar a
        // Maximum inconsistent with the VerticalOffset just committed against maximumYOverride -
        // Configure asserts those two stay consistent, so the same override has to carry through.
        Synchronize(maximumYOverride);

        unchecked
        {
            _scrollTransitionVersion++;
        }

        RaiseScrollChanged(
            new ScrollChangedEventArgs(previous, new Point(x, y), Extent, Viewport, cause),
            _scrollTransitionVersion);
        return true;
    }

    /// <summary>Delivers <see cref="ScrollChanged"/> to each subscriber only while this transition is
    /// still the newest one, so a subscriber that reentrantly triggers another scroll change supersedes
    /// delivery to later subscribers rather than letting a stale transition reach them.</summary>
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

    [Pure]
    private bool IsContentDescendant(ControlBase value)
    {
        Debug.Assert(value is not null, "Descendant checks require a control.");

        for (var current = value; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    [Pure]
    private static int Reveal(int current, int viewport, int start, int length)
    {
        Debug.Assert(current >= 0 && viewport >= 0, "Reveal uses a non-negative viewport.");
        Debug.Assert(start >= 0 && length >= 0, "Reveal uses non-negative content geometry.");

        var end = start.Add(length);

        // A target larger than the viewport cannot be fully contained. Preserve any visible
        // slice instead of alternating between top- and bottom-edge alignment on consecutive
        // arranged passes; when it is wholly outside, expose the nearest edge once.
        return length > viewport
            ? end <= current
                ? Math.Max(0, end - viewport)
                : start >= current.Add(viewport)
                    ? start
                    : current
            : start < current
                ? start
                : end > current.Add(viewport)
                    ? Math.Max(0, end - viewport)
                    : current;
    }

    [Pure]
    private int MaximumX() => AutoScroll && (ScrollBars & ScrollBars.Horizontal) != 0
        ? Math.Max(0, Extent.Width - Viewport.Width)
        : 0;

    [Pure]
    private int MaximumY() => AutoScroll && (ScrollBars & ScrollBars.Vertical) != 0
        ? Math.Max(0, Extent.Height - Viewport.Height)
        : 0;

    private Size Resolve(
        Size available,
        Size extent,
        out bool horizontal,
        out bool vertical,
        out Size viewport)
    {
        Debug.Assert(available is { Width: >= 0, Height: >= 0 }, "Scrollbar resolution uses available cell extents.");
        Debug.Assert(extent is { Width: >= 0, Height: >= 0 },
            "Scrollbar resolution uses non-negative content extents.");

        horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 &&
                     HorizontalBarVisibility == ScrollBarVisibility.Always;
        vertical = (ScrollBars & ScrollBars.Vertical) != 0 &&
                   VerticalBarVisibility == ScrollBarVisibility.Always;

        // Automatic bars are added monotonically because one reserved axis can
        // induce overflow on the other. Two additions are the finite maximum. Each addition
        // claims a cell from the bounded cross axis, which can change how much width-dependent
        // content (wrapped text) reflows, so content is re-measured at the narrower axis before
        // the next probe reads its extent. OnMeasuredDesired runs the same two-probe shape at
        // measure time, minus the re-measure step - see its own comment for why that step doesn't
        // carry over.
        for (var probe = 0; probe < 2; probe++)
        {
            viewport = new Size(
                Math.Max(0, available.Width - (vertical ? 1 : 0)),
                Math.Max(0, available.Height - (horizontal ? 1 : 0)));
            var addHorizontal = (ScrollBars & ScrollBars.Horizontal) != 0 &&
                                HorizontalBarVisibility == ScrollBarVisibility.Auto &&
                                extent.Width > viewport.Width;
            var addVertical = (ScrollBars & ScrollBars.Vertical) != 0 &&
                              VerticalBarVisibility == ScrollBarVisibility.Auto &&
                              extent.Height > viewport.Height;
            var nextHorizontal = horizontal || addHorizontal;
            var nextVertical = vertical || addVertical;

            if (nextHorizontal == horizontal && nextVertical == vertical)
            {
                return extent;
            }

            horizontal = nextHorizontal;
            vertical = nextVertical;
            extent = MeasureContent(available, horizontal, vertical);
        }

        viewport = new Size(
            Math.Max(0, available.Width - (vertical ? 1 : 0)),
            Math.Max(0, available.Height - (horizontal ? 1 : 0)));
        return extent;
    }

    private Size MeasureContent(Size available, bool horizontal, bool vertical)
    {
        var width = (ScrollBars & ScrollBars.Horizontal) != 0 ? null : ReserveBarCell(available.Width, vertical);
        var height = (ScrollBars & ScrollBars.Vertical) != 0 ? null : ReserveBarCell(available.Height, horizontal);
        return MeasureOverride(new Constraint(width, height));
    }

    private static void ValidateOffset(int value, int maximum, string name)
    {
        Debug.Assert(maximum >= 0, "Offset validation uses a non-negative maximum.");
        Debug.Assert(!string.IsNullOrWhiteSpace(name), "Offset validation identifies its public argument.");

        if (value < 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name, value, "Offset must be inside the current extent.");
        }
    }

    [Pure]
    private static int MultiplyNegative(int left, int right) =>
        (int) Math.Clamp(-(long) left * right, int.MinValue, int.MaxValue);

    #endregion
}
