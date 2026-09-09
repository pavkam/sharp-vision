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
/// <see cref="InitializeScrollableContent"/>. The shared contract deliberately exposes semantic
/// offsets, extent, policy, styling, and events on the component while keeping the mutable host and
/// its realized presentation private.
/// </remarks>
[PublicAPI]
public abstract class ScrollableCompositeControlBase: CompositeControlBase
{
    private RetainedScrollPart? _scrollPart;
    private StyleSlot<ScrollBarStyle>? _scrollBarStyle;
    private Container? _scrollHost;

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
        _scrollPart = RegisterRetainedScrollPart(host, forwardsScrollEvent, OnScrollHostScrollChanged);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, host, nameof(ScrollBarStyle));
    }

    /// <summary>Adds one <see cref="ScrollChanged"/> subscriber.</summary>
    /// <remarks>
    /// The default implementation forwards to the private scrolling host's bridge. A derived
    /// component that republishes a transformed or settled transition through a different mechanism
    /// - such as a width-dependent projection coordinator - overrides this together with
    /// <see cref="RemoveScrollChangedHandler"/> to route subscribers there instead, typically paired
    /// with <c>forwardsScrollEvent: false</c> at <see cref="InitializeScrollableContent"/>.
    /// </remarks>
    /// <param name="handler">The subscriber to add, or null (a no-op).</param>
    protected virtual void AddScrollChangedHandler(EventHandler<ScrollChangedEventArgs>? handler) =>
        GetScrollPart().AddScrollChanged(handler);

    /// <summary>Removes one <see cref="ScrollChanged"/> subscriber.</summary>
    /// <param name="handler">The subscriber to remove, or null (a no-op).</param>
    protected virtual void RemoveScrollChangedHandler(EventHandler<ScrollChangedEventArgs>? handler) =>
        GetScrollPart().RemoveScrollChanged(handler);

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
    /// cref="InitializeScrollableContent"/>. That ordering is what lets an override synchronously
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

    [Pure]
    private RetainedScrollPart GetScrollPart() => _scrollPart ??
        throw new InvalidOperationException("The scrolling presentation host is not initialized.");

    [Pure]
    private StyleSlot<ScrollBarStyle> GetScrollBarStyle() => _scrollBarStyle ??
        throw new InvalidOperationException("The scrolling presentation host is not initialized.");

    [Pure]
    private Container GetScrollHost() => _scrollHost ??
        throw new InvalidOperationException("The scrolling presentation host is not initialized.");
}
