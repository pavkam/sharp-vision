using System.Diagnostics;
using System.Text;

using SharpVision.Input;
using SharpVision.Terminal.Input;

using KeyAction = SharpVision.Terminal.Input.Action;

namespace SharpVision.Controls;

/// <summary>Provides shared focus, keyboard, pointer, capture, and pressed behavior.</summary>
public abstract class Pressable: Container
{
    private bool _pointerHeld;
    private bool _spaceHeld;
    private CaptureManager? _subscribedCapture;

    /// <summary>Initializes a focusable pressable container with finite child capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    protected Pressable(int capacity) : base(capacity) => CanFocus = true;

    /// <inheritdoc/>
    internal override bool OwnsHover => true;

    /// <summary>Completes one validated activation in a concrete control.</summary>
    /// <param name="cause">The input path that completed activation.</param>
    protected abstract void Activate(ActivationCause cause);

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            return;
        }

        if (eventArgs is KeyEventArgs key)
        {
            Handle(key);
        }
        else if (eventArgs is PointerEventArgs pointer)
        {
            Handle(pointer);
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (!focused)
        {
            Cancel(releaseCapture: true);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        Cancel(releaseCapture: false);
    }

    private void Cancel(bool releaseCapture)
    {
        _spaceHeld = false;
        _pointerHeld = false;
        SetPressed(false);
        UnsubscribeCapture();

        if (releaseCapture && CaptureOwner?.Captured is { } captured && ReferenceEquals(captured, this))
        {
            CaptureOwner.Release();
        }
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;
        var space = stroke.Code == Code.Character && stroke.Character == new Rune(' ');

        if (space)
        {
            eventArgs.Handled = true;

            if (stroke.Action == KeyAction.Press && !_spaceHeld)
            {
                _spaceHeld = true;
                SetPressed(true);
            }
            else if (stroke.Action == KeyAction.Release && _spaceHeld)
            {
                _spaceHeld = false;
                SetPressed(false);

                if (FocusOwner is null || IsFocused)
                {
                    Activate(ActivationCause.Keyboard);
                }
            }

            return;
        }

        if (stroke.Code == Code.Enter && stroke.Action == KeyAction.Press)
        {
            eventArgs.Handled = true;
            Activate(ActivationCause.Keyboard);
        }
    }

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if ((pointer.Buttons & Buttons.Primary) == 0)
        {
            if (pointer.Action == PointerAction.Press)
            {
                SetPressed(false);
            }

            return;
        }

        var inside = pointer.Cells is { } cells && Bounds.Contains(cells);

        if (pointer.Action == PointerAction.Press && inside)
        {
            var capture = CaptureOwner;

            if (capture is null || !capture.Capture(this))
            {
                return;
            }

            _ = FocusOwner?.Focus(this);
            _pointerHeld = true;
            SubscribeCapture(capture);
            SetPressed(true);
            eventArgs.Handled = true;
            return;
        }

        if (!_pointerHeld)
        {
            return;
        }

        SetPressed(inside);
        eventArgs.Handled = true;

        if (pointer.Action == PointerAction.Release)
        {
            _pointerHeld = false;
            UnsubscribeCapture();
            CaptureOwner?.Release();
            SetPressed(false);

            if (inside)
            {
                Activate(ActivationCause.Pointer);
            }
        }
    }

    private void OnCaptureCancelled(object? sender, CaptureCancelledEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Control, this))
        {
            Debug.Assert(ReferenceEquals(sender, _subscribedCapture), "Cancellation comes from the subscribed owner.");
            Cancel(releaseCapture: false);
        }
    }

    private void SubscribeCapture(CaptureManager value)
    {
        UnsubscribeCapture();
        _subscribedCapture = value;
        value.Cancelled += OnCaptureCancelled;
    }

    private void UnsubscribeCapture()
    {
        if (_subscribedCapture is { } capture)
        {
            capture.Cancelled -= OnCaptureCancelled;
            _subscribedCapture = null;
        }
    }
}
