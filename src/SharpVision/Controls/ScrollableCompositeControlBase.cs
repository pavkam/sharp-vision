// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Scrolling;

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
        _scrollPart = RegisterRetainedScrollPart(host, forwardsScrollEvent);
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
