// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

using SharpVision.Scrolling;

/// <summary>Settles one width-dependent retained projection against its composed scrolling
/// viewport and exposes only the final scroll transition from that synchronous transaction.</summary>
/// <remarks>The owner supplies projection policy through a callback while this
/// coordinator owns the bounded layout loop, exact measure constraint, event coalescing, and
/// exception-safe transaction lifetime. The optional active predicate keeps width-independent
/// projections out of the reconciliation path.</remarks>
internal sealed class WidthDependentViewportCoordinator
{
    /// <summary>Gets the maximum projection rebuilds allowed in one layout transaction.</summary>
    internal const int MaximumReconciliationAttempts = 4;

    private readonly ControlBase _eventSender;
    private readonly Container _viewport;
    private readonly ControlBase _projection;
    private readonly Func<bool> _isActive;
    private readonly Func<int?> _projectionWidth;
    private readonly Action<int> _reproject;
    private Constraint _measureConstraint;
    private bool _isReconciling;
    private ScrollChangedEventArgs? _pendingScrollChanged;
    private ulong _transitionVersion;

    /// <summary>Initializes one coordinator over a retained viewport and projection surface.</summary>
    /// <param name="eventSender">The non-null public control reported as the event sender.</param>
    /// <param name="viewport">The non-null retained scrolling container.</param>
    /// <param name="projection">The non-null retained width-dependent surface.</param>
    /// <param name="isActive">Returns whether the current projection depends on viewport width.</param>
    /// <param name="projectionWidth">Returns the width used by the current projection, or null.</param>
    /// <param name="reproject">Rebuilds projection state for one positive viewport width.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public WidthDependentViewportCoordinator(
        ControlBase eventSender,
        Container viewport,
        ControlBase projection,
        Func<bool> isActive,
        Func<int?> projectionWidth,
        Action<int> reproject)
    {
        ArgumentNullException.ThrowIfNull(eventSender);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(isActive);
        ArgumentNullException.ThrowIfNull(projectionWidth);
        ArgumentNullException.ThrowIfNull(reproject);

        _eventSender = eventSender;
        _viewport = viewport;
        _projection = projection;
        _isActive = isActive;
        _projectionWidth = projectionWidth;
        _reproject = reproject;
        _viewport.ScrollChanged += OnViewportScrollChanged;
    }

    /// <summary>Raised after one direct viewport transition or one settled reconciliation
    /// transaction changes the composed offset.</summary>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    /// <summary>Captures the owner's exact current measure constraint for synchronous remeasure.</summary>
    /// <param name="constraint">The exact constraint received by the owner.</param>
    public void CaptureMeasureConstraint(Constraint constraint) => _measureConstraint = constraint;

    /// <summary>Runs the owner's initial arrange and, when active, synchronously settles its
    /// projection against the resulting scrollbar-aware viewport width.</summary>
    /// <param name="bounds">The resolved content bounds passed to the retained viewport.</param>
    /// <param name="arrange">The non-null callback that performs the owner's initial arrange.</param>
    /// <exception cref="ArgumentNullException"><paramref name="arrange"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The active projection fails to converge within
    /// <see cref="MaximumReconciliationAttempts"/> rebuilds, or reconciliation is reentered.</exception>
    public void Arrange(Rect bounds, Action arrange)
    {
        ArgumentNullException.ThrowIfNull(arrange);

        if (!_isActive())
        {
            arrange();
            return;
        }

        if (_isReconciling)
        {
            throw new InvalidOperationException("Width-dependent viewport reconciliation cannot be reentered.");
        }

        _isReconciling = true;
        _pendingScrollChanged = null;
        ScrollChangedEventArgs? settled = null;
        var completed = false;

        try
        {
            arrange();
            Reconcile(bounds);
            settled = _pendingScrollChanged;
            completed = true;
        }
        finally
        {
            _isReconciling = false;
            _pendingScrollChanged = null;
        }

        if (completed && settled is { } eventArgs && eventArgs.PreviousOffset != eventArgs.Offset)
        {
            RaiseScrollChanged(eventArgs);
        }
    }

    private void Reconcile(Rect bounds)
    {
        for (var attempt = 0; attempt < MaximumReconciliationAttempts; attempt++)
        {
            var width = _viewport.Viewport.Width;

            if (width <= 0 || _projectionWidth() == width)
            {
                return;
            }

            _reproject(width);
            _viewport.InvalidateSelf(Invalidation.Measure);
            _projection.InvalidateSelf(Invalidation.Measure);
            _viewport.Measure(_measureConstraint);
            _viewport.Arrange(bounds, widthResolved: true, heightResolved: true);
            RefreshPendingGeometry();
        }

        var finalWidth = _viewport.Viewport.Width;

        if (finalWidth > 0 && _projectionWidth() != finalWidth)
        {
            throw new InvalidOperationException(
                $"Width-dependent viewport projection did not converge after {MaximumReconciliationAttempts} attempts.");
        }
    }

    private void OnViewportScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        _ = sender;

        if (!_isReconciling)
        {
            RaiseScrollChanged(eventArgs);
            return;
        }

        _pendingScrollChanged = new ScrollChangedEventArgs(
            _pendingScrollChanged?.PreviousOffset ?? eventArgs.PreviousOffset,
            eventArgs.Offset,
            eventArgs.Extent,
            eventArgs.Viewport,
            eventArgs.Cause);
    }

    private void RefreshPendingGeometry()
    {
        if (_pendingScrollChanged is not { } pending)
        {
            return;
        }

        _pendingScrollChanged = new ScrollChangedEventArgs(
            pending.PreviousOffset,
            new Point(_viewport.HorizontalOffset, _viewport.VerticalOffset),
            _viewport.Extent,
            _viewport.Viewport,
            pending.Cause);
    }

    private void RaiseScrollChanged(ScrollChangedEventArgs eventArgs)
    {
        unchecked
        {
            _transitionVersion++;
        }

        var version = _transitionVersion;
        var handlers = ScrollChanged;

        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList())
        {
            if (_transitionVersion != version)
            {
                break;
            }

            ((EventHandler<ScrollChangedEventArgs>) subscriber)(_eventSender, eventArgs);
        }
    }
}
