// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns property and event forwarding for one retained scrolling container.</summary>
internal sealed class RetainedScrollPart: IDisposable
{
    private readonly RetainedPartProperty<Size> _extent;
    private readonly bool _forwardsScrollEvent;
    private readonly RetainedPartProperty<int> _horizontalOffset;
    private readonly RetainedPartProperty<int> _lineSize;
    private readonly ControlBase _owner;
    private readonly RetainedPartProperty<int> _pageOverlap;
    private readonly RetainedPartProperty<ScrollBars> _scrollBars;
    private readonly RetainedPartProperty<ShowScrollBars> _showScrollBars;
    private readonly Container _source;
    private readonly OwnedControlSlot _sourceSlot;
    private readonly RetainedPartProperty<int> _verticalOffset;
    private readonly RetainedPartProperty<Size> _viewport;
    private bool _isDisposed;
    private EventHandler<ScrollChangedEventArgs>? _scrollChanged;

    /// <summary>Initializes all forwarding registrations for one retained scroll source.</summary>
    public RetainedScrollPart(ControlBase owner, Container source, bool forwardsScrollEvent)
    {
        Debug.Assert(owner is not null, "A retained scroll bridge requires its owner.");
        Debug.Assert(source is not null, "A retained scroll bridge requires its source.");
        Debug.Assert(source.OwningSlot is not null, "A retained scroll source is already owned.");
        _owner = owner;
        _source = source;
        _sourceSlot = source.OwningSlot;
        _forwardsScrollEvent = forwardsScrollEvent;
        _scrollBars = Property(
            nameof(Container.ScrollBars),
            nameof(Container.ScrollBars),
            () => source.ScrollBars,
            value => source.ScrollBars = value);
        _showScrollBars = Property(
            nameof(Container.ShowScrollBars),
            nameof(Container.ShowScrollBars),
            () => source.ShowScrollBars,
            value => source.ShowScrollBars = value);
        _lineSize = Property(
            nameof(Container.LineSize),
            nameof(Container.LineSize),
            () => source.LineSize,
            value => source.LineSize = value);
        _pageOverlap = Property(
            nameof(Container.PageOverlap),
            nameof(Container.PageOverlap),
            () => source.PageOverlap,
            value => source.PageOverlap = value);
        _horizontalOffset = Property(
            nameof(Container.HorizontalOffset),
            nameof(Container.HorizontalOffset),
            () => source.HorizontalOffset,
            value => source.HorizontalOffset = value);
        _verticalOffset = Property(
            nameof(Container.VerticalOffset),
            nameof(Container.VerticalOffset),
            () => source.VerticalOffset,
            value => source.VerticalOffset = value);
        _extent = Property(nameof(Container.Extent), nameof(Container.Extent), () => source.Extent);
        _viewport = Property(nameof(Container.Viewport), nameof(Container.Viewport), () => source.Viewport);
        source.ScrollChanged += OnSourceScrollChanged;
        _sourceSlot.Changed += OnSourceSlotChanged;
    }

    /// <summary>Gets or sets the retained scrollable axes.</summary>
    public ScrollBars ScrollBars { get => _scrollBars.Value; set => _scrollBars.Value = value; }

    /// <summary>Gets or sets the retained scrollbar visibility policy.</summary>
    public ShowScrollBars ShowScrollBars { get => _showScrollBars.Value; set => _showScrollBars.Value = value; }

    /// <summary>Gets or sets the retained line increment.</summary>
    public int LineSize { get => _lineSize.Value; set => _lineSize.Value = value; }

    /// <summary>Gets or sets the retained page overlap.</summary>
    public int PageOverlap { get => _pageOverlap.Value; set => _pageOverlap.Value = value; }

    /// <summary>Gets or sets the retained horizontal offset.</summary>
    public int HorizontalOffset { get => _horizontalOffset.Value; set => _horizontalOffset.Value = value; }

    /// <summary>Gets or sets the retained vertical offset.</summary>
    public int VerticalOffset { get => _verticalOffset.Value; set => _verticalOffset.Value = value; }

    /// <summary>Gets the retained content extent.</summary>
    public Size Extent => _extent.Value;

    /// <summary>Gets the retained viewport extent.</summary>
    public Size Viewport => _viewport.Value;

    /// <summary>Adds one direct scroll-event forwarding subscriber.</summary>
    public void AddScrollChanged(EventHandler<ScrollChangedEventArgs>? handler) => _scrollChanged += handler;

    /// <summary>Removes one direct scroll-event forwarding subscriber.</summary>
    public void RemoveScrollChanged(EventHandler<ScrollChangedEventArgs>? handler) => _scrollChanged -= handler;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _sourceSlot.Changed -= OnSourceSlotChanged;
        _source.ScrollChanged -= OnSourceScrollChanged;
        _viewport.Dispose();
        _extent.Dispose();
        _verticalOffset.Dispose();
        _horizontalOffset.Dispose();
        _pageOverlap.Dispose();
        _lineSize.Dispose();
        _showScrollBars.Dispose();
        _scrollBars.Dispose();
        _scrollChanged = null;
    }

    private RetainedPartProperty<T> Property<T>(
        string sourceName,
        string ownerName,
        Func<T> get,
        Action<T>? set = null) =>
        _owner.RegisterRetainedPartProperty(_source, sourceName, ownerName, get, set);

    private void OnSourceScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        _horizontalOffset.Refresh();
        _verticalOffset.Refresh();
        _extent.Refresh();
        _viewport.Refresh();

        if (_forwardsScrollEvent)
        {
            _scrollChanged?.Invoke(sender, eventArgs);
        }
    }

    private void OnSourceSlotChanged(OwnedControlChange change)
    {
        if (!ReferenceEquals(_source.OwningSlot, _sourceSlot))
        {
            Dispose();
        }
    }
}
