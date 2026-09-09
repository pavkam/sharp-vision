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
        host.ScrollChanged += OnHostScrollChanged;
        _scrollPart = RegisterRetainedScrollPart(host);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, host, nameof(ScrollBarStyle));
    }

    /// <summary>Runs the shared virtualized uniform-row-height state machine after the caller has
    /// arranged its private presentation host.</summary>
    /// <remarks>
    /// Up to two passes call <see cref="TryResolveUniformRowHeight"/> against the viewport height
    /// with <paramref name="leadingBandHeight"/> excluded; whenever a pass actually commits a new
    /// height, <paramref name="host"/> is re-measured and re-arranged through the shared
    /// <see cref="ControlBase.MeasureChild(ControlBase, Constraint)"/> and
    /// <see cref="ControlBase.ArrangeChild(ControlBase, Rect, ResolvedAxes)"/> seams so the settled
    /// height is reflected before the next pass or the remap step below reads it. Once the loop
    /// settles, if <see cref="ResolvedUniformRowHeight"/> actually differs from
    /// <paramref name="previousRowHeight"/>, <paramref name="previousOffset"/> is remapped onto the
    /// same logical row and proportional point within it through
    /// <see cref="UniformRowHeight.RemapOffset(int, int, int, int)"/>, and
    /// <paramref name="host"/> is re-anchored there through
    /// <see cref="Container.ScrollByKnownMaximum"/> so the caller never observes an
    /// unrelated jump. An offset that already sits inside <paramref name="leadingBandHeight"/> - a
    /// header band a derived control reserves ahead of its rows, for example - passes through
    /// unchanged instead of being remapped, because that band's own height did not change.
    /// </remarks>
    /// <param name="host">The non-null private presentation host, already arranged by the caller.</param>
    /// <param name="bounds">This control's own final arranged bounds.</param>
    /// <param name="previousRowHeight">
    /// The row height committed before this arrange pass began, or null when none has ever resolved.
    /// A caller anchors this before its own initial <c>ArrangeChild(host, ...)</c> call, since the
    /// first resolve pass below may already replace the host's committed height.
    /// </param>
    /// <param name="previousOffset">
    /// The non-negative vertical offset captured at the same time as <paramref name="previousRowHeight"/>.
    /// </param>
    /// <param name="leadingBandHeight">
    /// The non-negative fixed content excluded from both the row viewport and the remapped stride,
    /// such as a header band. Zero for a caller with no such band.
    /// </param>
    /// <param name="rowGap">The non-negative fixed gap after each row. Zero when rows are contiguous.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="previousOffset"/>, <paramref name="leadingBandHeight"/>, or
    /// <paramref name="rowGap"/> is negative.
    /// </exception>
    protected void ArrangeUniformRows(
        Container host,
        Rect bounds,
        int? previousRowHeight,
        int previousOffset,
        int leadingBandHeight,
        int rowGap)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentOutOfRangeException.ThrowIfNegative(previousOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(leadingBandHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(rowGap);

        for (var pass = 0; pass < 2; pass++)
        {
            var viewportHeight = Math.Max(0, Viewport.Height - leadingBandHeight);

            if (!TryResolveUniformRowHeight(viewportHeight))
            {
                break;
            }

            _ = MeasureChild(host, new Constraint(bounds.Width, bounds.Height));
            ArrangeChild(host, bounds, ResolvedAxes.Both);
        }

        if (previousRowHeight is not int height || height == ResolvedUniformRowHeight)
        {
            return;
        }

        var contentOffset = Math.Max(0, previousOffset - leadingBandHeight);
        var mapped = UniformRowHeight.RemapOffset(contentOffset, height, ResolvedUniformRowHeight, rowGap);
        var target = previousOffset < leadingBandHeight ? previousOffset : leadingBandHeight.Add(mapped);
        var maximum = Math.Max(0, Extent.Height - Viewport.Height);
        _ = host.ScrollByKnownMaximum(target - VerticalOffset, maximum, ScrollCause.Resize);
    }

    /// <summary>Attempts to resolve and commit this control's uniform row height against one
    /// viewport height.</summary>
    /// <param name="viewportHeight">The non-negative viewport height, with any leading band already excluded.</param>
    /// <returns>
    /// True when the resolved height differs from the previously committed height and this call
    /// committed it, requiring the caller to re-measure and re-arrange its presentation host; false
    /// when the committed height is already current.
    /// </returns>
    /// <remarks>
    /// The default implementation never resolves a uniform row height and always returns false. A
    /// virtualizing owner that calls <see cref="ArrangeUniformRows"/> overrides this together with
    /// <see cref="ResolvedUniformRowHeight"/> to resolve and commit its own uniform row height
    /// request, such as through
    /// <see cref="UniformRowHeight.Resolve(Length, int)"/>.
    /// </remarks>
    protected virtual bool TryResolveUniformRowHeight(int viewportHeight) => false;

    /// <summary>Gets the currently committed uniform row height.</summary>
    /// <remarks>
    /// The default implementation never has a committed uniform row height. A virtualizing owner
    /// that calls <see cref="ArrangeUniformRows"/> overrides this to expose the height
    /// <see cref="TryResolveUniformRowHeight"/> most recently committed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No uniform row height has ever been committed. The default implementation always throws this.
    /// </exception>
    protected virtual int ResolvedUniformRowHeight =>
        throw new InvalidOperationException(
            "This scrolling item owner never resolves a uniform row height. Override " +
            $"{nameof(TryResolveUniformRowHeight)} and {nameof(ResolvedUniformRowHeight)} before " +
            $"calling {nameof(ArrangeUniformRows)}.");

    /// <summary>Responds to one committed offset, extent, or viewport transition on the private
    /// presentation host.</summary>
    /// <param name="eventArgs">The non-null committed transition.</param>
    /// <remarks>
    /// The default implementation skips <see cref="ScrollCause.Content"/> and
    /// <see cref="ScrollCause.Resize"/> - both causes originate from the host's own arrange
    /// transaction, which is still open when this runs, so reconciling realized item controls here
    /// risks the exact non-convergence hazard <see cref="Container"/> documents for lazy child
    /// creation during layout; a generous overscan margin in <see cref="RewindowItems"/> absorbs
    /// ordinary resize deltas instead, and the window catches up fully on the next genuine scroll -
    /// then calls <see cref="RewindowItems"/> for every other cause. A derived owner overrides this
    /// only to change which causes trigger a rewindow.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    protected virtual void OnItemsHostScrollChanged(ScrollChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs.Cause is ScrollCause.Content or ScrollCause.Resize)
        {
            return;
        }

        RewindowItems();
    }

    /// <summary>Reconciles realized item controls against the current viewport, offset, and any
    /// overscan margin.</summary>
    /// <remarks>
    /// The default implementation does nothing. A virtualizing owner overrides this to derealize
    /// item controls outside the current window and realize item controls inside it. Must only ever
    /// run outside an active measure or arrange transaction - see <see cref="OnItemsHostScrollChanged"/>.
    /// </remarks>
    protected virtual void RewindowItems()
    {
    }

    private void OnHostScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        _ = sender;
        OnItemsHostScrollChanged(eventArgs);
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
