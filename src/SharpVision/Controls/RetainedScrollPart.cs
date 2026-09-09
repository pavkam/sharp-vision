// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns property and event forwarding for one retained scrolling container.</summary>
/// <remarks>
/// Obtained only through <see cref="ControlBase.RegisterRetainedScrollPart"/>: a derived control
/// owning one private retained <see cref="Container"/> asks its base for a bridge, then republishes
/// this bridge's members under its own semantic names. Disposal follows the registering owner - a
/// caller never disposes an instance directly, and the owner's own ownership-path tracking disposes
/// it automatically once the retained source stops being an owned descendant.
/// </remarks>
[PublicAPI]
public sealed class RetainedScrollPart: IDisposable
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
    private readonly Action<ScrollChangedEventArgs>? _sourceScrollChanged;
    private readonly OwnedControlSlot _sourceSlot;
    private readonly RetainedPartProperty<int> _verticalOffset;
    private readonly RetainedPartProperty<Size> _viewport;
    private bool _isDisposed;
    private EventHandler<ScrollChangedEventArgs>? _scrollChanged;
    private ulong _scrollChangedVersion;

    /// <summary>Initializes all forwarding registrations for one retained scroll source.</summary>
    /// <param name="owner">The non-null semantic owner republishing this bridge's members.</param>
    /// <param name="source">The non-null retained scrolling container already owned by <paramref name="owner"/>.</param>
    /// <param name="forwardsScrollEvent">
    /// Whether a committed source <see cref="Container.ScrollChanged"/> transition also invokes this
    /// bridge's own forwarding subscribers directly. An owner that republishes a settled or
    /// otherwise transformed transition instead - through a different mechanism such as a projection
    /// coordinator - passes false and raises through <see cref="RaiseScrollChanged"/> itself.
    /// </param>
    /// <param name="sourceScrollChanged">
    /// An optional callback invoked for every committed source transition, after this bridge has
    /// refreshed the owner's cached <see cref="Extent"/>/<see cref="Viewport"/>/offset properties and
    /// before <paramref name="forwardsScrollEvent"/> decides whether the transition is also
    /// forwarded. This ordering lets the callback synchronously dispose or hide the owner without
    /// racing the refresh above it.
    /// </param>
    internal RetainedScrollPart(
        ControlBase owner,
        Container source,
        bool forwardsScrollEvent,
        Action<ScrollChangedEventArgs>? sourceScrollChanged = null)
    {
        Debug.Assert(owner is not null, "A retained scroll bridge requires its owner.");
        Debug.Assert(source is not null, "A retained scroll bridge requires its source.");
        Debug.Assert(source.OwningSlot is not null, "A retained scroll source is already owned.");
        _owner = owner;
        _source = source;
        _sourceSlot = source.OwningSlot;
        _forwardsScrollEvent = forwardsScrollEvent;
        _sourceScrollChanged = sourceScrollChanged;
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
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The registering owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The registering owner or source is disposed.</exception>
    public ScrollBars ScrollBars { get => _scrollBars.Value; set => _scrollBars.Value = value; }

    /// <summary>Gets or sets the retained scrollbar visibility policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The registering owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The registering owner or source is disposed.</exception>
    public ShowScrollBars ShowScrollBars { get => _showScrollBars.Value; set => _showScrollBars.Value = value; }

    /// <summary>Gets or sets the retained line increment.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The registering owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The registering owner or source is disposed.</exception>
    public int LineSize { get => _lineSize.Value; set => _lineSize.Value = value; }

    /// <summary>Gets or sets the retained page overlap.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The registering owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The registering owner or source is disposed.</exception>
    public int PageOverlap { get => _pageOverlap.Value; set => _pageOverlap.Value = value; }

    /// <summary>Gets or sets the retained horizontal offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The registering owner is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The registering owner or source is disposed.</exception>
    public int HorizontalOffset { get => _horizontalOffset.Value; set => _horizontalOffset.Value = value; }

    /// <summary>Gets or sets the retained vertical offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The registering owner is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The registering owner or source is disposed.</exception>
    public int VerticalOffset { get => _verticalOffset.Value; set => _verticalOffset.Value = value; }

    /// <summary>Gets the retained content extent.</summary>
    public Size Extent => _extent.Value;

    /// <summary>Gets the retained viewport extent.</summary>
    public Size Viewport => _viewport.Value;

    /// <summary>Adds one direct scroll-event forwarding subscriber.</summary>
    /// <param name="handler">The subscriber to add, or null (a no-op).</param>
    public void AddScrollChanged(EventHandler<ScrollChangedEventArgs>? handler) => _scrollChanged += handler;

    /// <summary>Removes one direct scroll-event forwarding subscriber.</summary>
    /// <param name="handler">The subscriber to remove, or null (a no-op).</param>
    public void RemoveScrollChanged(EventHandler<ScrollChangedEventArgs>? handler) => _scrollChanged -= handler;

    /// <summary>Publishes one transition to every current forwarding subscriber, independent of
    /// whether the source-forwarding path installed by the constructor is enabled.</summary>
    /// <remarks>
    /// An owner whose semantic offset is not a pure republication of the retained source - for
    /// example one that composes the source's own transition with a second, owner-tracked axis -
    /// calls this directly instead of relying on the automatic source-forwarding path. Publication
    /// stops calling later subscribers, without error, the moment a reentrant call to this method
    /// supersedes the transition being delivered, so an owner that raises through more than one path
    /// (a source-forwarded transition and an owner-tracked one) still delivers exactly the newest
    /// transition to every subscriber, never a stale one a reentrant subscriber has already
    /// superseded.
    /// </remarks>
    /// <param name="eventArgs">The non-null immutable transition to publish.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    public void RaiseScrollChanged(ScrollChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        PublishScrollChanged(eventArgs);
    }

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

    // Constructs the sub-property bridge directly rather than through the owner's
    // RegisterRetainedPartProperty: this bridge is not a subclass of ControlBase, so it cannot reach
    // that now-protected member through an owner-typed reference, and the ownership check it would
    // otherwise repeat was already satisfied by RegisterRetainedScrollPart moments earlier for the
    // same source. Each sub-property still lives for exactly this bridge's lifetime because Dispose
    // below disposes every one of them directly - registering them a second time in the owner's own
    // bookkeeping list would only double-dispose an already-idempotent Dispose().
    private RetainedPartProperty<T> Property<T>(
        string sourceName,
        string ownerName,
        Func<T> get,
        Action<T>? set = null) =>
        new(_owner, _source, sourceName, ownerName, get, set);

    private void OnSourceScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        _horizontalOffset.Refresh();
        _verticalOffset.Refresh();
        _extent.Refresh();
        _viewport.Refresh();
        _sourceScrollChanged?.Invoke(eventArgs);

        if (_forwardsScrollEvent)
        {
            PublishScrollChanged(eventArgs);
        }
    }

    private void PublishScrollChanged(ScrollChangedEventArgs eventArgs)
    {
        unchecked
        {
            _scrollChangedVersion++;
        }

        var version = _scrollChangedVersion;

        EventPublication.Publish<EventHandler<ScrollChangedEventArgs>>(
            _scrollChanged,
            () => _scrollChangedVersion == version,
            handler => handler(_owner, eventArgs));
    }

    private void OnSourceSlotChanged(OwnedControlChange change)
    {
        if (!ReferenceEquals(_source.OwningSlot, _sourceSlot))
        {
            Dispose();
        }
    }
}
