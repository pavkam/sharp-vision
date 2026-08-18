// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using SharpVision.Terminal.Input;

/// <summary>Edits saturation and value for one hue across a semantic color plane.</summary>
internal sealed class ColorPlane: ControlBase
{
    private readonly DragBehavior _drag;

    /// <summary>Initializes a focusable stretching saturation/value surface.</summary>
    public ColorPlane()
    {
        _drag = new DragBehavior(
            () => ContentBounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>Raised after saturation or value changes through user input.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the selected HSV hue angle (0-359 degrees).</summary>
    public int Hue { get; private set; }

    /// <summary>Gets the selected HSV saturation (0.0 = grey, 1.0 = fully saturated).</summary>
    public double Saturation { get; private set; } = 1;

    /// <summary>Gets the selected HSV brightness value (0.0 = black, 1.0 = full brightness).</summary>
    public double Value { get; private set; } = 1;

    /// <summary>Gets or sets the printable marker glyph drawn over the currently selected coordinate,
    /// or null to use the code-owned default. A value that cannot occupy exactly one cell under the
    /// active width policy falls back to the same code-owned repair glyph as an unset marker.</summary>
    public Rune? SelectedMarker
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Synchronizes the selected HSV coordinates without publishing input.</summary>
    /// <param name="hue">The hue from zero through 359.</param>
    /// <param name="saturation">The saturation from zero through one.</param>
    /// <param name="value">The value from zero through one.</param>
    public void SetSelection(int hue, double saturation, double value)
    {
        Debug.Assert(hue is >= 0 and < 360, "Picker hue is normalized.");
        Debug.Assert(saturation is >= 0 and <= 1, "Picker saturation is normalized.");
        Debug.Assert(value is >= 0 and <= 1, "Picker value is normalized.");
        VerifyMutable();

        if (Hue == hue && Saturation == saturation && Value == value)
        {
            return;
        }

        Hue = hue;
        Saturation = saturation;
        Value = value;
        Invalidate(InvalidationImpact.Render);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint.Width;
        return new Size(32, 8);
    }

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

        if (!eventArgs.IsHandled)
        {
            base.OnEvent(eventArgs);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _drag.Unavailable();

        if (reason == ReleaseReason.Disposed)
        {
            Changed = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _drag.FocusChanged(focused);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _drag.CaptureLost();
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var selectedX = bounds.Width <= 1
            ? 0
            : (int) Math.Round(Saturation * (bounds.Width - 1), MidpointRounding.AwayFromZero);
        var selectedY = bounds.Height <= 1
            ? 0
            : (int) Math.Round((1 - Value) * (bounds.Height - 1), MidpointRounding.AwayFromZero);
        var themedMarker = ControlGlyphs.ColorPlane.SelectedMarker;
        var marker = (SelectedMarker ?? themedMarker.Value).Resolve(themedMarker.Fallback, CellPolicy.AmbiguousWidth);

        for (var y = 0; y < bounds.Height; y++)
        {
            var value = bounds.Height <= 1 ? 1 : 1 - (y / (double) (bounds.Height - 1));

            for (var x = 0; x < bounds.Width; x++)
            {
                var saturation = bounds.Width <= 1 ? 0 : x / (double) (bounds.Width - 1);
                var color = Color.FromHsv(Hue, saturation, value);
                var selected = x == selectedX && y == selectedY;
                var glyph = selected ? marker : new Rune(' ');
                var foreground = selected ? color.Contrast() : Color.Default;

                // ColorPlane is borderless and fully covered by dynamic per-cell HSV content, so
                // the themed border recolor that gives other directly-focusable controls a focus
                // cue has nothing to attach to here, and reversing every cell would obscure the
                // gradient being picked. Instead, mirror the borderless-control fallback
                // (Theme.ApplyBorderlessFocusFallback forcing TerminalAttributes.Reverse) but
                // confine it to the outer ring of cells so the interior gradient stays legible.
                // The selected-marker cell already computes its own contrasting foreground and
                // must never be reversed on top of that, even when it sits on the ring.
                var isEdge = x == 0 || y == 0 || x == bounds.Width - 1 || y == bounds.Height - 1;
                var attributes = IsFocused && isEdge && !selected
                    ? TerminalAttributes.Reverse
                    : TerminalAttributes.None;
                canvas.DrawRune(
                    glyph,
                    new Point(bounds.X + x, bounds.Y + y),
                    new TerminalStyle(foreground, color, attributes));
            }
        }
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        if (eventArgs.Stroke.Code is not (Code.Left or Code.Right or Code.Up or Code.Down
            or Code.Home or Code.End))
        {
            return;
        }

        var saturation = Saturation;
        var value = Value;

        switch (eventArgs.Stroke.Code)
        {
            case Code.Left:
                saturation -= 0.01;
                break;
            case Code.Right:
                saturation += 0.01;
                break;
            case Code.Up:
                value += 0.01;
                break;
            case Code.Down:
                value -= 0.01;
                break;
            case Code.Home:
                saturation = 0;
                break;
            case Code.End:
                saturation = 1;
                break;
            case Code.Unknown:
                break;
            case Code.Character:
                break;
            case Code.Escape:
                break;
            case Code.Enter:
                break;
            case Code.Tab:
                break;
            case Code.Backspace:
                break;
            case Code.Insert:
                break;
            case Code.Delete:
                break;
            case Code.PageUp:
                break;
            case Code.PageDown:
                break;
            case Code.Begin:
                break;
            case Code.F1:
                break;
            case Code.F2:
                break;
            case Code.F3:
                break;
            case Code.F4:
                break;
            case Code.F5:
                break;
            case Code.F6:
                break;
            case Code.F7:
                break;
            case Code.F8:
                break;
            case Code.F9:
                break;
            case Code.F10:
                break;
            case Code.F11:
                break;
            case Code.F12:
                break;
            case Code.F13:
                break;
            case Code.F14:
                break;
            case Code.F15:
                break;
            case Code.F16:
                break;
            case Code.F17:
                break;
            case Code.F18:
                break;
            case Code.F19:
                break;
            case Code.F20:
                break;
            case Code.F21:
                break;
            case Code.F22:
                break;
            case Code.F23:
                break;
            case Code.F24:
                break;
            case Code.F25:
                break;
            case Code.F26:
                break;
            case Code.F27:
                break;
            case Code.F28:
                break;
            case Code.F29:
                break;
            case Code.F30:
                break;
            case Code.F31:
                break;
            case Code.F32:
                break;
            case Code.F33:
                break;
            case Code.F34:
                break;
            case Code.F35:
                break;
            case Code.F36:
                break;
            case Code.F37:
                break;
            case Code.F38:
                break;
            case Code.F39:
                break;
            case Code.F40:
                break;
            case Code.F41:
                break;
            case Code.F42:
                break;
            case Code.F43:
                break;
            case Code.F44:
                break;
            case Code.F45:
                break;
            case Code.F46:
                break;
            case Code.F47:
                break;
            case Code.F48:
                break;
            case Code.F49:
                break;
            case Code.F50:
                break;
            case Code.F51:
                break;
            case Code.F52:
                break;
            case Code.F53:
                break;
            case Code.F54:
                break;
            case Code.F55:
                break;
            case Code.F56:
                break;
            case Code.F57:
                break;
            case Code.F58:
                break;
            case Code.F59:
                break;
            case Code.F60:
                break;
            case Code.F61:
                break;
            case Code.F62:
                break;
            case Code.F63:
                break;
            case Code.CapsLock:
                break;
            case Code.ScrollLock:
                break;
            case Code.NumLock:
                break;
            case Code.PrintScreen:
                break;
            case Code.Pause:
                break;
            case Code.Menu:
                break;
            default:
                return;
        }

        _ = Commit(Math.Clamp(saturation, 0, 1), Math.Clamp(value, 0, 1));
        eventArgs.IsHandled = true;
    }

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (_drag.IsDragging)
        {
            eventArgs.IsHandled = true;

            if (pointer.Action is PointerAction.Release or PointerAction.Leave)
            {
                _drag.Cancel(releaseCapture: true);
            }
            else if (pointer.Cells is { } dragCells)
            {
                Select(dragCells);
            }

            return;
        }

        if (pointer.Action != PointerAction.Press ||
            (pointer.Buttons & Buttons.Primary) == 0 ||
            pointer.Cells is not { } cells ||
            !ContentBounds.Contains(cells))
        {
            return;
        }

        _ = RequestFocus();
        Select(cells);
        eventArgs.IsHandled = true;
        _ = _drag.TryStart(cells);
    }

    private void Select(Point point)
    {
        var bounds = ContentBounds;
        var saturation = bounds.Width <= 1
            ? 0
            : Math.Clamp(point.X - bounds.X, 0, bounds.Width - 1) / (double) (bounds.Width - 1);
        var value = bounds.Height <= 1
            ? 1
            : 1 - (Math.Clamp(point.Y - bounds.Y, 0, bounds.Height - 1) /
            (double) (bounds.Height - 1));
        _ = Commit(saturation, value);
    }

    private bool Commit(double saturation, double value)
    {
        if (Saturation == saturation && Value == value)
        {
            return false;
        }

        Saturation = saturation;
        Value = value;
        Invalidate(InvalidationImpact.Render);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

}
