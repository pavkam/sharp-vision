// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Scrolling;

using SharpVision.Terminal.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Defines a composite component whose private presentation host provides bounded
/// two-axis scrolling.</summary>
/// <remarks>
/// A derived constructor creates its retained implementation tree, commits it through
/// <see cref="CompositeControlBase.InitializeContent"/>, then installs one detached scrolling host -
/// either that same content root or a retained descendant of it - through
/// <see cref="InitializeScrollableContent(Container, bool)"/>. The shared contract deliberately exposes semantic
/// offsets, extent, policy, styling, and events on the component while keeping the mutable host and
/// its realized presentation private.
/// </remarks>
[PublicAPI]
public abstract class ScrollableCompositeControlBase: CompositeControlBase
{
    private RetainedScrollPart? _scrollPart;
    private StyleSlot<ScrollBarStyle>? _scrollBarStyle;
    private Container? _scrollHost;
    private bool _forwardsScrollEvent;
    private ProjectionSurface? _projectionSurface;
    private WidthDependentViewportCoordinator? _projectionCoordinator;

    /// <summary>Initializes a scrollable component awaiting constructor-time host initialization.</summary>
    protected ScrollableCompositeControlBase()
    {
    }

    /// <summary>Raised after the private scrolling host commits one or both offsets.</summary>
    /// <remarks>The event sender is always this semantic component, never its private host.</remarks>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged
    {
        add => AddScrollChangedHandler(value);
        remove => RemoveScrollChangedHandler(value);
    }

    /// <summary>Gets the committed non-negative scrolling content extent.</summary>
    public virtual Size Extent => GetScrollPart().Extent;

    /// <summary>Gets the committed non-negative scrolling viewport extent.</summary>
    public virtual Size Viewport => GetScrollPart().Viewport;

    /// <summary>Gets or sets the scrollable axes of the private scrolling host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached component is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    public virtual ScrollBars ScrollBars
    {
        get => GetScrollPart().ScrollBars;
        set => GetScrollPart().ScrollBars = value;
    }

    /// <summary>Gets or sets the scrollbar reservation policy for enabled axes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached component is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => GetScrollPart().ShowScrollBars;
        set => GetScrollPart().ShowScrollBars = value;
    }

    /// <summary>Gets or sets the complete local style for generated scrollbars.</summary>
    /// <exception cref="InvalidOperationException">The attached component is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => GetScrollBarStyle().Local;
        set => GetScrollBarStyle().Local = value;
    }

    /// <summary>Gets the resolved generated-scrollbar style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => GetScrollBarStyle().Actual;

    /// <summary>Gets or sets the non-negative keyboard and wheel scrolling increment in cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached component is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    [NonNegativeValue]
    public int LineSize
    {
        get => GetScrollPart().LineSize;
        set => GetScrollPart().LineSize = value;
    }

    /// <summary>Gets or sets non-negative cells retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached component is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    [NonNegativeValue]
    public int PageOverlap
    {
        get => GetScrollPart().PageOverlap;
        set => GetScrollPart().PageOverlap = value;
    }

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached component is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    [NonNegativeValue]
    public virtual int HorizontalOffset
    {
        get => GetScrollPart().HorizontalOffset;
        set => GetScrollPart().HorizontalOffset = value;
    }

    /// <summary>Gets or sets the valid vertical content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached component is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    [NonNegativeValue]
    public int VerticalOffset
    {
        get => GetScrollPart().VerticalOffset;
        set => GetScrollPart().VerticalOffset = value;
    }

    /// <summary>Adds signed scrolling deltas with saturation and endpoint clamping.</summary>
    /// <param name="x">The requested horizontal delta.</param>
    /// <param name="y">The requested vertical delta.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached component is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    public virtual bool ScrollBy(int x, int y, ScrollCause cause = ScrollCause.Programmatic) =>
        GetScrollHost().ScrollBy(x, y, cause);

    /// <summary>Installs the one private scrolling host and its shared scrolling contract.</summary>
    /// <remarks>
    /// The caller commits <paramref name="host"/> - or a retained ancestor of it - through
    /// <see cref="CompositeControlBase.InitializeContent"/> before calling this method; this method
    /// only registers the scrolling forwarding contract over an already-owned host.
    /// </remarks>
    /// <param name="host">The non-null retained scrolling container, already owned by this component.</param>
    /// <param name="forwardsScrollEvent">
    /// Whether a committed host <see cref="Container.ScrollChanged"/> transition also raises this
    /// component's own <see cref="ScrollChanged"/> directly. A component whose semantic offset is
    /// not a pure republication of the host - for example one that composes the host's own
    /// transition with a second, component-tracked axis, or republishes a settled transition through
    /// a projection coordinator - passes false and raises through <see cref="RaiseScrollChanged"/> or
    /// an overridden <see cref="AddScrollChangedHandler"/>/<see cref="RemoveScrollChangedHandler"/>
    /// pair instead.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// A host was already installed, <paramref name="host"/> is not an owned retained descendant of
    /// this component, or this component is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    protected void InitializeScrollableContent(Container host, bool forwardsScrollEvent = true)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (_scrollPart is not null)
        {
            throw new InvalidOperationException("A scrolling content host was already installed.");
        }

        _scrollHost = host;
        _forwardsScrollEvent = forwardsScrollEvent;
        _scrollPart = RegisterRetainedScrollPart(host, forwardsScrollEvent, OnScrollHostScrollChanged);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, host, nameof(ScrollBarStyle));
    }

    /// <summary>Installs the one private scrolling host together with its shared projection
    /// surface.</summary>
    /// <remarks>
    /// Identical to <see cref="InitializeScrollableContent(Container, bool)"/> except that it also
    /// records <paramref name="surface"/> as the retained descendant a theme swap invalidates
    /// through <see cref="ResolveProjectionThemeChangeImpact"/> and a style change invalidates for
    /// Render (see <see cref="OnPropertyChanged"/>), and as the projection
    /// <see cref="InitializeWidthDependentProjection"/> later reconciles against the host's settled
    /// viewport width.
    /// </remarks>
    /// <param name="host">The non-null retained scrolling container, already owned by this component.</param>
    /// <param name="surface">
    /// The non-null projection surface, already an owned retained descendant of this component -
    /// typically the sole child of <paramref name="host"/>.
    /// </param>
    /// <param name="forwardsScrollEvent">Forwarded to <see cref="InitializeScrollableContent(Container, bool)"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="surface"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// A host was already installed, <paramref name="host"/> or <paramref name="surface"/> is not
    /// an owned retained descendant of this component, or this component is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    protected void InitializeScrollableContent(Container host, ProjectionSurface surface, bool forwardsScrollEvent = true)
    {
        ArgumentNullException.ThrowIfNull(surface);
        InitializeScrollableContent(host, forwardsScrollEvent);

        if (!IsOwnedRetainedDescendant(surface))
        {
            throw new InvalidOperationException(
                "A projection surface must be an owned retained descendant of this component.");
        }

        _projectionSurface = surface;
    }

    /// <summary>Installs shared reconciliation between a width-dependent retained projection and the
    /// private scrolling host's settled viewport width.</summary>
    /// <remarks>
    /// The coordinator subscribes to the host's own <see cref="Container.ScrollChanged"/> after the
    /// retained scrolling bridge installed by <see cref="InitializeScrollableContent(Container, ProjectionSurface, bool)"/>
    /// already did, so that bridge's refresh of this component's cached <see cref="Extent"/>,
    /// <see cref="Viewport"/>, and offsets always runs before a subscriber reached through the
    /// coordinator can observe the transition. Once installed, this base's own overridden
    /// <c>MeasureOverride</c> and <c>ArrangeOverride</c> capture the measure constraint and run
    /// reconciliation through the coordinator, and <see cref="AddScrollChangedHandler"/> and
    /// <see cref="RemoveScrollChangedHandler"/> route subscribers to it - a derived override of
    /// either still wins over this default routing, exactly as it did before this method existed.
    /// </remarks>
    /// <param name="isActive">Returns whether the current projection depends on viewport width.</param>
    /// <param name="projectionWidth">Returns the width used by the current projection, or null.</param>
    /// <param name="reproject">Rebuilds projection state for one positive viewport width.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="InitializeScrollableContent(Container, ProjectionSurface, bool)"/> has not run,
    /// that call passed <c>forwardsScrollEvent: true</c>, a coordinator was already installed, or
    /// this component is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    protected void InitializeWidthDependentProjection(
        Func<bool> isActive,
        Func<int?> projectionWidth,
        Action<int> reproject)
    {
        ArgumentNullException.ThrowIfNull(isActive);
        ArgumentNullException.ThrowIfNull(projectionWidth);
        ArgumentNullException.ThrowIfNull(reproject);
        VerifyMutable();

        if (_projectionSurface is null || _scrollHost is null)
        {
            throw new InvalidOperationException(
                "Width-dependent projection requires InitializeScrollableContent(Container, ProjectionSurface, bool) to run first.");
        }

        if (_forwardsScrollEvent)
        {
            throw new InvalidOperationException(
                "Width-dependent projection requires forwardsScrollEvent: false at InitializeScrollableContent.");
        }

        if (_projectionCoordinator is not null)
        {
            throw new InvalidOperationException("A width-dependent projection was already installed.");
        }

        _projectionCoordinator = new WidthDependentViewportCoordinator(
            this,
            _scrollHost,
            _projectionSurface,
            isActive,
            projectionWidth,
            reproject);
    }

    /// <summary>Adds one <see cref="ScrollChanged"/> subscriber.</summary>
    /// <remarks>
    /// Routes to the installed <see cref="InitializeWidthDependentProjection"/> coordinator when one
    /// is installed, so a subscriber observes one settled transition per reconciled layout pass
    /// instead of every intermediate reconciliation attempt; otherwise forwards to the private
    /// scrolling host's bridge directly. A derived component that republishes a transformed or
    /// settled transition through a different mechanism of its own overrides this together with
    /// <see cref="RemoveScrollChangedHandler"/> instead, typically paired with
    /// <c>forwardsScrollEvent: false</c> at <see cref="InitializeScrollableContent(Container, bool)"/>.
    /// </remarks>
    /// <param name="handler">The subscriber to add, or null (a no-op).</param>
    protected virtual void AddScrollChangedHandler(EventHandler<ScrollChangedEventArgs>? handler)
    {
        if (_projectionCoordinator is { } coordinator)
        {
            coordinator.ScrollChanged += handler;
            return;
        }

        GetScrollPart().AddScrollChanged(handler);
    }

    /// <summary>Removes one <see cref="ScrollChanged"/> subscriber.</summary>
    /// <param name="handler">The subscriber to remove, or null (a no-op).</param>
    protected virtual void RemoveScrollChangedHandler(EventHandler<ScrollChangedEventArgs>? handler)
    {
        if (_projectionCoordinator is { } coordinator)
        {
            coordinator.ScrollChanged -= handler;
            return;
        }

        GetScrollPart().RemoveScrollChanged(handler);
    }

    /// <summary>Publishes one <see cref="ScrollChanged"/> transition directly through the installed
    /// host bridge, independent of <c>forwardsScrollEvent</c>.</summary>
    /// <remarks>
    /// A component that owns a second scrolling axis outside the private host - one the host itself
    /// never reports through its own <see cref="Container.ScrollChanged"/> - calls this to publish
    /// that axis's transitions through the same reentrancy-safe delivery the host's own transitions
    /// use, so every subscriber still observes exactly one settled event per commit regardless of
    /// which axis moved.
    /// </remarks>
    /// <param name="eventArgs">The non-null immutable transition to publish.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    /// <exception cref="InvalidOperationException">No scrolling host is installed.</exception>
    protected void RaiseScrollChanged(ScrollChangedEventArgs eventArgs) =>
        GetScrollPart().RaiseScrollChanged(eventArgs);

    /// <summary>Maps one keyboard navigation stroke through the installed host's scroll mapping and
    /// applies it, for a focus-owning derived component whose private scrolling host never sits on
    /// the routed key path and so never sees the host's own automatic keyboard scrolling.</summary>
    /// <remarks>
    /// Mirrors <c>Container.Handle(KeyEventArgs)</c>'s own modifier and key-down gating exactly, so
    /// a derived component forwarding into this method observes the identical policy a plain
    /// scrollable <see cref="Container"/> would. The delta is applied through the virtual
    /// <see cref="ScrollBy(int, int, ScrollCause)"/> rather than directly against the host, so an
    /// override that composes a second axis - <c>Document</c>'s vertical-only override, for instance
    /// - participates in the same call.
    /// </remarks>
    /// <param name="eventArgs">The non-null routed key event.</param>
    /// <param name="consumeAtBoundary">
    /// Whether a navigation key that maps to an axis this component can scroll at all is reported
    /// handled even when the offset is already at that axis's endpoint, so the keystroke cannot
    /// escape to page an enclosing scrollable ancestor out from under this still-focused component.
    /// When false, only a key that actually moves an offset is reported handled.
    /// </param>
    /// <returns>
    /// True when an offset moved, or when <paramref name="consumeAtBoundary"/> is true and the
    /// mapped delta targets an axis this component can scroll at all. Does not set
    /// <see cref="RoutedEventArgs.IsHandled"/> - the caller decides that from the result.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    protected bool HandleScrollKey(KeyEventArgs eventArgs, bool consumeAtBoundary = true)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!eventArgs.IsKeyDown ||
            !KeyboardModifierPolicy.MatchesCommand(eventArgs.Stroke.Modifiers, Modifiers.None))
        {
            return false;
        }

        var host = GetScrollHost();
        var delta = host.ComputeKeyScrollDelta(eventArgs.Stroke.Code);

        if (delta is null)
        {
            return false;
        }

        var scrollableHorizontally = delta.Value.X != 0 && host.MaximumHorizontalOffset > 0;
        var scrollableVertically = delta.Value.Y != 0 && host.MaximumVerticalOffset > 0;

        if (!scrollableHorizontally && !scrollableVertically)
        {
            return false;
        }

        var moved = ScrollBy(delta.Value.X, delta.Value.Y, ScrollCause.Keyboard);
        return moved || consumeAtBoundary;
    }

    /// <summary>Maps one wheel record through the installed host's line increment and applies it,
    /// for a focus-owning derived component whose private scrolling host is not the pointer's hit
    /// target and so never sees the host's own automatic wheel scrolling.</summary>
    /// <param name="eventArgs">The non-null routed pointer event.</param>
    /// <returns>
    /// True when an offset moved. Does not set <see cref="RoutedEventArgs.IsHandled"/> - the caller
    /// decides that from the result.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    protected bool HandleScrollWheel(PointerEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Wheel)
        {
            return false;
        }

        var lineSize = GetScrollHost().LineSize;
        var x = pointer.WheelX.Multiply(lineSize);
        var y = (int) Math.Clamp(-(long) pointer.WheelY * lineSize, int.MinValue, int.MaxValue);
        return ScrollBy(x, y, ScrollCause.Wheel);
    }

    /// <summary>Responds to one committed offset, extent, or viewport transition on the private
    /// scrolling host.</summary>
    /// <remarks>
    /// The default implementation does nothing. Runs after the retained bridge has refreshed this
    /// component's own cached <see cref="Extent"/>, <see cref="Viewport"/>, <see
    /// cref="HorizontalOffset"/>, and <see cref="VerticalOffset"/> against the host's newly committed
    /// values, and before the bridge forwards the transition through this component's public
    /// <see cref="ScrollChanged"/> when <c>forwardsScrollEvent</c> was true at <see
    /// cref="InitializeScrollableContent(Container, bool)"/>. That ordering is what lets an override synchronously
    /// dispose or hide this component without racing the bridge's own refresh above it.
    /// </remarks>
    /// <param name="eventArgs">The non-null committed transition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    protected virtual void OnScrollHostScrollChanged(ScrollChangedEventArgs eventArgs) =>
        ArgumentNullException.ThrowIfNull(eventArgs);

    /// <inheritdoc/>
    protected override int TextSelectionPageDistance() => _scrollHost is not null
        ? Math.Max(1, Viewport.Height - PageOverlap)
        : base.TextSelectionPageDistance();

    /// <summary>Calculates this component's own theme-change impact for its installed
    /// <see cref="ProjectionSurface"/> to compose alongside the surface's own.</summary>
    /// <remarks>
    /// A projection surface owns no style slot of its own, so nothing about a Theme swap alone
    /// would otherwise ever invalidate it: the framework's own per-control Theme-transition
    /// invalidation is computed against each control's own style, and the surface's own style is
    /// the generic control default. This lets <see cref="ProjectionSurface"/> compose this
    /// component's real impact - computed against whatever style slot this concrete component
    /// actually owns - into its own, purely internal, since only the surface it belongs to ever
    /// calls it.
    /// </remarks>
    /// <param name="previous">The currently inherited Theme, or null.</param>
    /// <param name="current">The prospective inherited Theme, or null.</param>
    /// <param name="previousParentAmbientFace">The explicit parent ambient face before replacement.</param>
    /// <param name="currentParentAmbientFace">The explicit parent ambient face after replacement.</param>
    /// <returns>The strongest affected UI phase.</returns>
    internal InvalidationImpact ResolveProjectionThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        GetThemeChangeImpact(previous, current, previousParentAmbientFace, currentParentAmbientFace);

    /// <inheritdoc/>
    /// <remarks>
    /// Invalidates the installed <see cref="ProjectionSurface"/> for Render whenever a property
    /// change named <c>"ActualStyle"</c> commits - whether from a local style assignment or purely
    /// from an inherited Theme swap - since the surface owns no style slot of its own and so is
    /// never otherwise invalidated by either path. Every concrete <c>IStyled&lt;TStyle&gt;</c>
    /// primary style slot publishes its resolved-value notification under the literal property name
    /// <c>"ActualStyle"</c> regardless of the concrete <c>TStyle</c> a derived sealed component
    /// declares, so this single check - written against that literal name rather than
    /// <c>nameof(ActualStyle)</c>, which this base itself has no such member to name - covers every
    /// derived component's own style.
    /// </remarks>
    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == "ActualStyle" && _projectionSurface is { } surface)
        {
            InvalidateRetainedDescendant(surface, InvalidationImpact.Render);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        // Stashed for the coordinator, which needs to remeasure the composed viewport with the
        // exact same constraint this component itself received - not a constraint it could
        // reconstruct from Bounds, since reconciliation runs inside ArrangeOverride, before any
        // later Measure call would refresh it.
        _projectionCoordinator?.CaptureMeasureConstraint(constraint);
        return base.MeasureOverride(constraint);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (_projectionCoordinator is { } coordinator)
        {
            coordinator.Arrange(bounds, () => base.ArrangeOverride(bounds));
            return;
        }

        base.ArrangeOverride(bounds);
    }

    [Pure]
    private RetainedScrollPart GetScrollPart() => _scrollPart ??
        throw new InvalidOperationException("The scrolling presentation host is not initialized.");

    [Pure]
    private StyleSlot<ScrollBarStyle> GetScrollBarStyle() => _scrollBarStyle ??
        throw new InvalidOperationException("The scrolling presentation host is not initialized.");

    [Pure]
    private Container GetScrollHost() => _scrollHost ??
        throw new InvalidOperationException("The scrolling presentation host is not initialized.");

    [Pure]
    private bool IsOwnedRetainedDescendant(ControlBase target)
    {
        for (var current = target.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }
}
