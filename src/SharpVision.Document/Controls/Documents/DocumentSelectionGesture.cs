// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

using SharpVision.Terminal.Input;
using SharpVision.Threading;

/// <summary>Arbitrates primary-pointer clicks and document-owned selection drags.</summary>
/// <remarks>
/// The potential phase deliberately leaves routing, focus, and child capture untouched. Crossing
/// the shared threshold transfers capture to the document, which cancels a retained child's pending
/// press before any release can activate it.
/// </remarks>
internal sealed class DocumentSelectionGesture
{
    private static readonly TimeSpan _autoScrollInterval = TimeSpan.FromMilliseconds(50);

    private readonly Document _owner;
    private DispatcherTimer? _autoScrollTimer;
    private ulong _autoScrollGeneration;
    private int _anchor;
    private DocumentSelectionSource? _associatedSource;
    private Point _latestCells;
    private ControlBase? _originalSource;
    private DocumentLink? _pressedLink;
    private DocumentLink? _releasedLink;
    private PointerEventArgs? _releasedPointerEvent;
    private Point _pressCells;
    private ulong _semanticFingerprint;

    /// <summary>Initializes pointer arbitration for one document.</summary>
    /// <param name="owner">The non-null owning document.</param>
    internal DocumentSelectionGesture(Document owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets the current gesture phase for behavioral invariant tests.</summary>
    internal DocumentSelectionGesturePhase Phase { get; private set; }

    /// <summary>Observes one pointer route during preview and takes ownership only after a drag.</summary>
    /// <param name="eventArgs">The routed pointer event.</param>
    internal void HandlePreview(PointerEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        Debug.Assert(eventArgs.Phase == RoutingPhase.Preview, "Selection arbitration observes preview routes.");

        var pointer = eventArgs.Pointer;

        if (Phase == DocumentSelectionGesturePhase.Selecting)
        {
            HandleSelecting(eventArgs);
            return;
        }

        if (pointer is
            {
                Action: PointerAction.Press,
                Buttons: var buttons,
                Cells: { } pressedCells
            } &&
            (buttons & Buttons.Primary) != 0)
        {
            BeginPotential(eventArgs.OriginalSource, pressedCells);
            return;
        }

        if (Phase != DocumentSelectionGesturePhase.Potential)
        {
            return;
        }

        if (pointer is
            {
                Action: PointerAction.Move,
                Buttons: var moveButtons,
                Cells: { } movedCells
            } &&
            (moveButtons & Buttons.Primary) != 0 &&
            PointerDragThreshold.IsCrossed(_pressCells, movedCells))
        {
            BeginSelecting(eventArgs, movedCells);
            return;
        }

        if (IsPrimaryGestureRelease(pointer))
        {
            CompletePotential(eventArgs);
        }
        else if (pointer.Action == PointerAction.Leave)
        {
            Cancel(releaseCapture: false);
        }
    }

    /// <summary>Observes already-consumed preview input only to close an armed gesture.</summary>
    /// <param name="eventArgs">The handled routed pointer event.</param>
    /// <remarks>
    /// Handled presses, moves, and wheel records never start or extend selection. A handled release
    /// or leave still closes existing state so capture and potential-click identity cannot leak past
    /// an ancestor-owned transaction.
    /// </remarks>
    internal void HandleHandledPreview(PointerEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        Debug.Assert(eventArgs.Phase == RoutingPhase.Preview, "Selection cleanup observes preview routes.");
        Debug.Assert(eventArgs.IsHandled, "Selection cleanup is reserved for already-consumed input.");

        if (Phase == DocumentSelectionGesturePhase.Idle ||
            (eventArgs.Pointer.Action != PointerAction.Leave && !IsPrimaryGestureRelease(eventArgs.Pointer)))
        {
            return;
        }

        if (Phase == DocumentSelectionGesturePhase.Potential)
        {
            ReleasePotentialChildCapture();
            Cancel(releaseCapture: false);
            return;
        }

        Cancel(releaseCapture: true);
    }

    /// <summary>Returns and clears the link whose unhandled release completed an eligible click.</summary>
    /// <param name="eventArgs">The release event reaching the document default behavior.</param>
    /// <returns>The eligible link, or null.</returns>
    internal DocumentLink? TakeReleasedLink(PointerEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var released = _releasedLink;
        var matches = ReferenceEquals(_releasedPointerEvent, eventArgs);
        _releasedLink = null;
        _releasedPointerEvent = null;
        return matches ? released : null;
    }

    /// <summary>Ends any potential or active gesture and optionally releases document capture.</summary>
    /// <param name="releaseCapture">Whether document-owned capture should be released.</param>
    internal void Cancel(bool releaseCapture)
    {
        StopAutoScroll();
        Phase = DocumentSelectionGesturePhase.Idle;
        _anchor = 0;
        _associatedSource = null;
        _originalSource = null;
        _pressedLink = null;
        _releasedLink = null;
        _releasedPointerEvent = null;

        if (releaseCapture)
        {
            _owner.ReleaseSelectionPointerCapture();
        }
    }

    private void BeginPotential(ControlBase? originalSource, Point cells)
    {
        Cancel(releaseCapture: false);

        if (!_owner.IsSelectionContentSource(originalSource))
        {
            return;
        }

        _pressCells = cells;
        _anchor = _owner.HitTestSelection(cells);
        _semanticFingerprint = _owner.SelectionFingerprint;
        _associatedSource = _owner.SelectionSourceFor(originalSource, cells);
        _originalSource = originalSource;
        _pressedLink = _owner.LinkAt(cells);
        Phase = DocumentSelectionGesturePhase.Potential;
    }

    private void ReleasePotentialChildCapture()
    {
        for (var current = _originalSource;
             current is not null && !ReferenceEquals(current, _owner);
             current = current.Parent)
        {
            if (!current.HasPointerCapture)
            {
                continue;
            }

            // A handled release skips the child's default PressBehavior cleanup. Transfer through
            // the document so the child receives Transferred cancellation, then immediately release
            // the temporary ownership. An ordinary link or text press owns no capture and never
            // enters this branch.
            if (_owner.CaptureSelectionPointer())
            {
                _owner.ReleaseSelectionPointerCapture();
            }

            return;
        }
    }

    private void BeginSelecting(PointerEventArgs eventArgs, Point cells)
    {
        Debug.Assert(_originalSource is not null, "A selection drag begins from a routed content source.");

        var fingerprint = _owner.SelectionFingerprint;

        if (fingerprint != _semanticFingerprint)
        {
            Cancel(releaseCapture: false);
            return;
        }

        // Transfer capture before moving focus. PressBehavior cancels on either transition, but the
        // transfer publishes the precise Transferred reason and prevents the child from releasing
        // capture itself during focus loss first.
        if (!_owner.CaptureSelectionPointer())
        {
            Cancel(releaseCapture: false);
            return;
        }

        Phase = DocumentSelectionGesturePhase.Selecting;
        _pressedLink = null;
        _releasedLink = null;
        _releasedPointerEvent = null;
        _ = _owner.Focus();
        _latestCells = cells;
        UpdateAssociatedSource(cells);
        _owner.CommitPointerSelection(_anchor, _owner.HitTestSelectionForDrag(cells));
        RefreshAutoScroll();
        eventArgs.IsHandled = true;
    }

    private void HandleSelecting(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer is
            {
                Action: PointerAction.Move,
                Buttons: var buttons,
                Cells: { } movedCells
            } &&
            (buttons & Buttons.Primary) != 0)
        {
            _latestCells = movedCells;
            UpdateAssociatedSource(movedCells);
            _owner.CommitPointerSelection(_anchor, _owner.HitTestSelectionForDrag(movedCells));
            RefreshAutoScroll();
            eventArgs.IsHandled = true;
            return;
        }

        if (IsPrimaryGestureRelease(pointer))
        {
            if (pointer.Cells is { } releasedCells)
            {
                _owner.CommitPointerSelection(_anchor, _owner.HitTestSelectionForDrag(releasedCells));
            }

            eventArgs.IsHandled = true;

            // Commit idle before explicit release invokes the owner's capture-loss hook.
            StopAutoScroll();
            Phase = DocumentSelectionGesturePhase.Idle;
            _associatedSource = null;
            _originalSource = null;
            _pressedLink = null;
            _owner.ReleaseSelectionPointerCapture();
            return;
        }

        if (pointer.Action == PointerAction.Leave)
        {
            eventArgs.IsHandled = true;
            Cancel(releaseCapture: true);
        }
    }

    private void CompletePotential(PointerEventArgs eventArgs)
    {
        var cells = eventArgs.Pointer.Cells;
        int? caret = null;

        if (cells is { } point)
        {
            // Hit testing refreshes semantic layout before LinkAt consults link regions. A release
            // handler can mutate or reflow the document earlier on the same preview route, and the
            // pressed link must never activate from the stale pre-mutation projection.
            caret = _owner.HitTestSelection(point);
        }

        var releasedLink = cells is { } releasedCells
            ? _owner.LinkAt(releasedCells)
            : null;

        if (caret is { } endpoint)
        {
            _owner.CommitPointerSelection(endpoint, endpoint);
        }

        _releasedLink = _pressedLink is { IsEnabled: true } pressed &&
                        ReferenceEquals(pressed, releasedLink)
            ? pressed
            : null;
        _releasedPointerEvent = _releasedLink is null ? null : eventArgs;
        Phase = DocumentSelectionGesturePhase.Idle;
        _anchor = 0;
        _associatedSource = null;
        _originalSource = null;
        _pressedLink = null;
    }

    [Pure]
    // Legacy X10/VT200 selector-three releases cannot identify the transitioned button. Once a
    // primary gesture is armed, that unqualified release is its completion; explicit non-primary
    // releases remain unrelated transitions.
    private static bool IsPrimaryGestureRelease(Pointer pointer) =>
        pointer.Action == PointerAction.Release &&
        (pointer.Buttons == Buttons.None || (pointer.Buttons & Buttons.Primary) != 0);

    private void UpdateAssociatedSource(Point cells)
        => _associatedSource = _owner.SelectionSourceAt(cells) ??
                               _owner.ResolveSelectionSource(_associatedSource, cells);

    private void RefreshAutoScroll()
    {
        Debug.Assert(Phase == DocumentSelectionGesturePhase.Selecting, "Only active selection owns autoscroll.");

        if (!_owner.HasPointerCapture ||
            !_owner.HasSelectionAutoScrollRequest(_latestCells, _associatedSource))
        {
            StopAutoScroll();
            return;
        }

        if (_autoScrollTimer is not null)
        {
            return;
        }

        var dispatcher = _owner.Dispatcher;

        if (dispatcher is null)
        {
            return;
        }

        var timer = new DispatcherTimer(dispatcher, _autoScrollInterval);
        timer.Tick += OnAutoScrollTick;
        _autoScrollTimer = timer;
        unchecked
        {
            _autoScrollGeneration++;
        }
        timer.Start();
    }

    private void OnAutoScrollTick(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;
        var timer = _autoScrollTimer;
        var generation = _autoScrollGeneration;

        if (!IsTickCurrent(sender, timer, generation))
        {
            return;
        }

        var fingerprint = _owner.SelectionFingerprint;

        if (!IsTickCurrent(sender, timer, generation))
        {
            return;
        }

        if (fingerprint != _semanticFingerprint)
        {
            Cancel(releaseCapture: true);
            return;
        }

        _associatedSource = _owner.ResolveSelectionSource(_associatedSource, _latestCells);

        if (!_owner.HasSelectionAutoScrollRequest(_latestCells, _associatedSource))
        {
            StopAutoScroll();
            return;
        }

        if (_owner.AutoScrollSelection(_latestCells, _associatedSource, out var hitAdjustment))
        {
            if (!IsTickCurrent(sender, timer, generation))
            {
                return;
            }

            fingerprint = _owner.SelectionFingerprint;

            if (!IsTickCurrent(sender, timer, generation))
            {
                return;
            }

            if (fingerprint != _semanticFingerprint)
            {
                Cancel(releaseCapture: true);
                return;
            }

            var caret = _owner.HitTestSelectionForDrag(new Point(
                AddCoordinates(_latestCells.X, hitAdjustment.X),
                AddCoordinates(_latestCells.Y, hitAdjustment.Y)));

            if (!IsTickCurrent(sender, timer, generation))
            {
                return;
            }

            fingerprint = _owner.SelectionFingerprint;

            if (!IsTickCurrent(sender, timer, generation))
            {
                return;
            }

            if (fingerprint != _semanticFingerprint)
            {
                Cancel(releaseCapture: true);
                return;
            }

            _owner.CommitPointerSelection(_anchor, caret);
        }
    }

    private bool IsTickCurrent(object? sender, DispatcherTimer? timer, ulong generation) =>
        ReferenceEquals(sender, timer) &&
        ReferenceEquals(timer, _autoScrollTimer) &&
        generation == _autoScrollGeneration &&
        Phase == DocumentSelectionGesturePhase.Selecting &&
        !_owner.IsDisposed &&
        _owner.Dispatcher is not null &&
        _owner.EffectiveIsEnabled &&
        _owner.EffectiveIsVisible &&
        _owner.HasPointerCapture;

    private void StopAutoScroll()
    {
        var timer = _autoScrollTimer;

        if (timer is null)
        {
            return;
        }

        _autoScrollTimer = null;
        unchecked
        {
            _autoScrollGeneration++;
        }
        timer.Tick -= OnAutoScrollTick;
        timer.Dispose();
    }

    [Pure]
    private static int AddCoordinates(int left, int right) =>
        (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);
}
