// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

using SharpVision.Terminal.Input;

/// <summary>Arbitrates primary clicks and capture-backed text-selection drags for one control.</summary>
internal sealed class TextSelectionGesture
{
    private static readonly TimeSpan _autoScrollInterval = TimeSpan.FromMilliseconds(50);

    private readonly ControlBase _owner;
    private DispatcherTimer? _autoScrollTimer;
    private ulong _autoScrollGeneration;
    private int _anchor;
    private TextSelectionSource? _associatedSource;
    private int _clickCount;
    private bool _capturedPotential;
    private Point _latestCells;
    private ControlBase? _originalSource;
    private Point _pressCells;
    private ulong _semanticFingerprint;

    /// <summary>Initializes one control-wide pointer-selection gesture.</summary>
    /// <param name="owner">The non-null selection owner.</param>
    internal TextSelectionGesture(ControlBase owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets the committed pointer-selection phase.</summary>
    internal TextSelectionGesturePhase Phase { get; private set; }

    /// <summary>Resets selection on press, then observes the preview route and claims it only after dragging begins.</summary>
    /// <param name="eventArgs">The routed pointer event.</param>
    internal void Handle(PointerEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var pointer = eventArgs.Pointer;

        if (eventArgs.IsHandled)
        {
            if (Phase != TextSelectionGesturePhase.Idle &&
                (pointer.Action == PointerAction.Leave || IsPrimaryRelease(pointer)))
            {
                if (Phase == TextSelectionGesturePhase.Potential && IsPrimaryRelease(pointer))
                {
                    _owner.ReleasePotentialTextSelectionChildCapture(_originalSource);
                }

                Cancel(releaseCapture: Phase == TextSelectionGesturePhase.Selecting);
            }

            return;
        }

        if (Phase == TextSelectionGesturePhase.Selecting)
        {
            HandleSelecting(eventArgs);
            return;
        }

        if (pointer is
            {
                Action: PointerAction.Press,
                Buttons: var pressedButtons,
                Cells: { } pressedCells
            } &&
            (pressedButtons & Buttons.Primary) != 0)
        {
            _pressCells = pressedCells;
            _originalSource = eventArgs.OriginalSource;
            _anchor = _owner.HitTestTextSelection(pressedCells);
            _clickCount = _owner.GetTextSelectionClickCount(eventArgs.OriginalSource, eventArgs.ClickCount);
            _associatedSource = _owner.GetTextSelectionSource(eventArgs.OriginalSource, pressedCells);
            // A new gesture must not display or expose the previous range while it waits for the
            // drag threshold or release. Collapsing here preserves the child's ordinary press path.
            _owner.CommitPointerTextSelection(_anchor, _anchor);
            _semanticFingerprint = _owner.TextSelectionFingerprint;
            Phase = TextSelectionGesturePhase.Potential;

            if (_owner.ShouldCaptureTextSelectionOnPress)
            {
                _capturedPotential = _owner.CaptureTextSelectionPointer();

                if (!_capturedPotential)
                {
                    Cancel(releaseCapture: false);
                }
            }

            return;
        }

        if (Phase != TextSelectionGesturePhase.Potential)
        {
            return;
        }

        if (pointer is
            {
                Action: PointerAction.Move,
                Buttons: var movedButtons,
                Cells: { } movedCells
            } &&
            (movedButtons & Buttons.Primary) != 0 &&
            PointerDragThreshold.IsCrossed(_pressCells, movedCells))
        {
            if (_owner.TextSelectionFingerprint != _semanticFingerprint)
            {
                Cancel(releaseCapture: false);
                return;
            }

            if (!_owner.CaptureTextSelectionPointer())
            {
                Cancel(releaseCapture: false);
                return;
            }

            Phase = TextSelectionGesturePhase.Selecting;
            _ = _owner.Focus();
            _latestCells = movedCells;
            _associatedSource = _owner.ResolveTextSelectionSource(_associatedSource, movedCells);
            _owner.CommitPointerTextSelection(_anchor, _owner.HitTestTextSelection(movedCells));
            RefreshAutoScroll();
            eventArgs.IsHandled = true;
            return;
        }

        if (IsPrimaryRelease(pointer))
        {
            if (pointer.Cells is { } releasedCells)
            {
                _owner.CommitTextSelectionClick(
                    _owner.HitTestTextSelection(releasedCells),
                    _clickCount);
                _owner.CompleteTextSelectionClick(
                    _originalSource,
                    _pressCells,
                    releasedCells,
                    _clickCount,
                    eventArgs);

                if (_clickCount >= 2)
                {
                    eventArgs.IsHandled = true;
                }
            }

            Cancel(releaseCapture: false);
        }
        else if (pointer.Action == PointerAction.Leave)
        {
            Cancel(releaseCapture: false);
        }
    }

    /// <summary>Cancels all retained gesture state and optionally releases owner capture.</summary>
    /// <param name="releaseCapture">Whether selection-owned capture should be released.</param>
    internal void Cancel(bool releaseCapture)
    {
        StopAutoScroll();
        Phase = TextSelectionGesturePhase.Idle;
        _anchor = 0;
        _clickCount = 0;
        _associatedSource = null;
        _originalSource = null;

        if (_capturedPotential)
        {
            releaseCapture = true;
            _capturedPotential = false;
        }

        if (releaseCapture)
        {
            _owner.ReleaseTextSelectionPointerCapture();
        }
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
            _associatedSource = _owner.ResolveTextSelectionSource(_associatedSource, movedCells);
            _owner.CommitPointerTextSelection(_anchor, _owner.HitTestTextSelection(movedCells));
            RefreshAutoScroll();
            eventArgs.IsHandled = true;
            return;
        }

        if (IsPrimaryRelease(pointer))
        {
            if (pointer.Cells is { } releasedCells)
            {
                _owner.CommitPointerTextSelection(_anchor, _owner.HitTestTextSelection(releasedCells));
            }

            eventArgs.IsHandled = true;
            StopAutoScroll();
            Cancel(releaseCapture: true);
        }
        else if (pointer.Action == PointerAction.Leave)
        {
            eventArgs.IsHandled = true;
            Cancel(releaseCapture: true);
        }
    }

    [Pure]
    private static bool IsPrimaryRelease(Pointer pointer) =>
        pointer.Action == PointerAction.Release &&
        (pointer.Buttons == Buttons.None || (pointer.Buttons & Buttons.Primary) != 0);

    private void RefreshAutoScroll()
    {
        Debug.Assert(Phase == TextSelectionGesturePhase.Selecting, "Only active selection owns autoscroll.");

        if (!_owner.HasPointerCapture ||
            !_owner.HasTextSelectionAutoScrollRequest(_latestCells, _associatedSource))
        {
            StopAutoScroll();
            return;
        }

        if (_autoScrollTimer is not null || _owner.Dispatcher is not { } dispatcher)
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

        if (_owner.TextSelectionFingerprint != _semanticFingerprint)
        {
            Cancel(releaseCapture: true);
            return;
        }

        _associatedSource = _owner.ResolveTextSelectionSource(_associatedSource, _latestCells);

        if (!_owner.HasTextSelectionAutoScrollRequest(_latestCells, _associatedSource))
        {
            StopAutoScroll();
            return;
        }

        if (!_owner.AutoScrollTextSelection(_latestCells, _associatedSource, out var hitAdjustment) ||
            !IsTickCurrent(sender, timer, generation))
        {
            return;
        }

        if (_owner.TextSelectionFingerprint != _semanticFingerprint)
        {
            Cancel(releaseCapture: true);
            return;
        }

        var adjusted = new Point(
            AddCoordinates(_latestCells.X, hitAdjustment.X),
            AddCoordinates(_latestCells.Y, hitAdjustment.Y));
        _owner.CommitPointerTextSelection(_anchor, _owner.HitTestTextSelection(adjusted));
    }

    private bool IsTickCurrent(object? sender, DispatcherTimer? timer, ulong generation) =>
        ReferenceEquals(sender, timer) &&
        ReferenceEquals(timer, _autoScrollTimer) &&
        generation == _autoScrollGeneration &&
        Phase == TextSelectionGesturePhase.Selecting &&
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
