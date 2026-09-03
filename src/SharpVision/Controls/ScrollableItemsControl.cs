// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Scrolling;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Defines an item owner whose private presentation host provides bounded two-axis scrolling.</summary>
/// <remarks>
/// A derived constructor installs one detached scrolling host through
/// <see cref="InitializeScrollableItemsHost"/>. The shared contract deliberately exposes semantic
/// offsets, extent, policy, styling, and events on the item owner while keeping the mutable host and
/// its realized child collection private.
/// </remarks>
[PublicAPI]
public abstract class ScrollableItemsControl: ItemsControl
{
    private RetainedScrollPart? _scrollPart;
    private StyleSlot<ScrollBarStyle>? _scrollBarStyle;
    private Container? _scrollHost;

    /// <summary>Initializes a scrolling item owner whose derived constructor installs one host.</summary>
    protected ScrollableItemsControl()
    {
    }

    /// <summary>Raised after the private presentation host commits one or both offsets.</summary>
    /// <remarks>The event sender is always this semantic item owner, never its private host.</remarks>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged
    {
        add => GetScrollPart().AddScrollChanged(value);
        remove => GetScrollPart().RemoveScrollChanged(value);
    }

    /// <summary>Gets the committed non-negative scrolling content extent.</summary>
    public Size Extent => GetScrollPart().Extent;

    /// <summary>Gets the committed non-negative scrolling viewport extent.</summary>
    public Size Viewport => GetScrollPart().Viewport;

    /// <summary>Gets or sets the scrollable axes of the private presentation host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get => GetScrollPart().ScrollBars;
        set => GetScrollPart().ScrollBars = value;
    }

    /// <summary>Gets or sets the scrollbar reservation policy for enabled axes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => GetScrollPart().ShowScrollBars;
        set => GetScrollPart().ShowScrollBars = value;
    }

    /// <summary>Gets or sets the complete local style for generated scrollbars.</summary>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => GetScrollBarStyle().Local;
        set => GetScrollBarStyle().Local = value;
    }

    /// <summary>Gets the resolved generated-scrollbar style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => GetScrollBarStyle().Actual;

    /// <summary>Gets or sets the non-negative keyboard and wheel scrolling increment in cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [NonNegativeValue]
    public int LineSize
    {
        get => GetScrollPart().LineSize;
        set => GetScrollPart().LineSize = value;
    }

    /// <summary>Gets or sets non-negative cells retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [NonNegativeValue]
    public int PageOverlap
    {
        get => GetScrollPart().PageOverlap;
        set => GetScrollPart().PageOverlap = value;
    }

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [NonNegativeValue]
    public int HorizontalOffset
    {
        get => GetScrollPart().HorizontalOffset;
        set => GetScrollPart().HorizontalOffset = value;
    }

    /// <summary>Gets or sets the valid vertical content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
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
    /// <exception cref="InvalidOperationException">The attached owner is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public bool ScrollBy(int x, int y, ScrollCause cause = ScrollCause.Programmatic) =>
        GetScrollHost().ScrollBy(x, y, cause);

    /// <summary>Installs the one private presentation host and its shared scrolling contract.</summary>
    /// <param name="host">The non-null detached scrolling container.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    /// <exception cref="ArgumentException">The host cannot be retained by this owner.</exception>
    /// <exception cref="InvalidOperationException">A host was already installed or mutation is unavailable.</exception>
    /// <exception cref="ObjectDisposedException">The owner or host is disposed.</exception>
    protected void InitializeScrollableItemsHost(Container host)
    {
        ArgumentNullException.ThrowIfNull(host);
        InitializeItemsHost(host);
        _scrollHost = host;
        _scrollPart = RegisterRetainedScrollPart(host);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, host, nameof(ScrollBarStyle));
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
}
