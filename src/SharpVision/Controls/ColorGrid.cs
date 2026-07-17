// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;
using SharpVision.Terminal.Rendering;

/// <summary>Edits one indexed terminal color through a responsive swatch grid.</summary>
internal sealed class ColorGrid: Control
{
    private readonly int _columns;
    private readonly int _count;
    private readonly int _rows;
    private bool _dragging;

    /// <summary>Initializes a 16- or 256-entry focusable palette.</summary>
    /// <param name="count">The supported palette entry count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not 16 or 256.</exception>
    internal ColorGrid(int count)
    {
        if (count is not (16 or 256))
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "A color grid contains 16 or 256 entries.");
        }

        _count = count;
        _columns = count == 256 ? 16 : 4;
        _rows = count / _columns;
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>Raised after the selected palette index changes through user input.</summary>
    internal event EventHandler? Changed;

    /// <summary>Gets the selected palette index.</summary>
    internal int SelectedIndex { get; private set; }

    /// <summary>Synchronizes the selected palette index without publishing input.</summary>
    /// <param name="index">The index inside this palette.</param>
    internal void SetSelectedIndex(int index)
    {
        Debug.Assert(index >= 0 && index < _count, "Picker projects values into the active palette.");
        VerifyMutable();

        if (SelectedIndex == index)
        {
            return;
        }

        SelectedIndex = index;
        Invalidate(ChangeImpact.Render);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint.Width;
        return new Size(_columns * 2, _rows);
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
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        CancelDrag(releaseCapture: false);

        if (reason == ReleaseReason.Disposed)
        {
            Changed = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (!focused)
        {
            CancelDrag(releaseCapture: true);
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        CancelDrag(releaseCapture: false);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;

        for (var y = 0; y < bounds.Height; y++)
        {
            var row = Math.Min(_rows - 1, y * _rows / Math.Max(1, bounds.Height));

            for (var x = 0; x < bounds.Width; x++)
            {
                var column = Math.Min(_columns - 1, x * _columns / Math.Max(1, bounds.Width));
                var index = Math.Min(_count - 1, (row * _columns) + column);
                var indexed = Color.Indexed(index);
                var rgb = Palette.Resolve(indexed);
                var selected = index == SelectedIndex && IsMarkerCell(x, bounds.Width, column);
                var glyph = selected ? new Rune('◆') : new Rune(' ');
                var foreground = selected ? ColorMath.Contrast(rgb) : Color.Default;
                canvas.DrawRune(
                    glyph,
                    new Point(bounds.X + x, bounds.Y + y),
                    new TerminalStyle(foreground, indexed));
            }
        }
    }

    private bool IsMarkerCell(int x, int width, int column)
    {
        var start = column * width / _columns;
        return x == start;
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        var next = eventArgs.Stroke.Code switch
        {
            Code.Left => SelectedIndex - 1,
            Code.Right => SelectedIndex + 1,
            Code.Up => SelectedIndex - _columns,
            Code.Down => SelectedIndex + _columns,
            Code.Home => 0,
            Code.End => _count - 1,
            Code.Unknown => throw new NotImplementedException(),
            Code.Character => throw new NotImplementedException(),
            Code.Escape => throw new NotImplementedException(),
            Code.Enter => throw new NotImplementedException(),
            Code.Tab => throw new NotImplementedException(),
            Code.Backspace => throw new NotImplementedException(),
            Code.Insert => throw new NotImplementedException(),
            Code.Delete => throw new NotImplementedException(),
            Code.PageUp => throw new NotImplementedException(),
            Code.PageDown => throw new NotImplementedException(),
            Code.F1 => throw new NotImplementedException(),
            Code.F2 => throw new NotImplementedException(),
            Code.F3 => throw new NotImplementedException(),
            Code.F4 => throw new NotImplementedException(),
            Code.F5 => throw new NotImplementedException(),
            Code.F6 => throw new NotImplementedException(),
            Code.F7 => throw new NotImplementedException(),
            Code.F8 => throw new NotImplementedException(),
            Code.F9 => throw new NotImplementedException(),
            Code.F10 => throw new NotImplementedException(),
            Code.F11 => throw new NotImplementedException(),
            Code.F12 => throw new NotImplementedException(),
            Code.F13 => throw new NotImplementedException(),
            Code.F14 => throw new NotImplementedException(),
            Code.F15 => throw new NotImplementedException(),
            Code.F16 => throw new NotImplementedException(),
            Code.F17 => throw new NotImplementedException(),
            Code.F18 => throw new NotImplementedException(),
            Code.F19 => throw new NotImplementedException(),
            Code.F20 => throw new NotImplementedException(),
            Code.F21 => throw new NotImplementedException(),
            Code.F22 => throw new NotImplementedException(),
            Code.F23 => throw new NotImplementedException(),
            Code.F24 => throw new NotImplementedException(),
            Code.F25 => throw new NotImplementedException(),
            Code.F26 => throw new NotImplementedException(),
            Code.F27 => throw new NotImplementedException(),
            Code.F28 => throw new NotImplementedException(),
            Code.F29 => throw new NotImplementedException(),
            Code.F30 => throw new NotImplementedException(),
            Code.F31 => throw new NotImplementedException(),
            Code.F32 => throw new NotImplementedException(),
            Code.F33 => throw new NotImplementedException(),
            Code.F34 => throw new NotImplementedException(),
            Code.F35 => throw new NotImplementedException(),
            Code.CapsLock => throw new NotImplementedException(),
            Code.ScrollLock => throw new NotImplementedException(),
            Code.NumLock => throw new NotImplementedException(),
            Code.PrintScreen => throw new NotImplementedException(),
            Code.Pause => throw new NotImplementedException(),
            Code.Menu => throw new NotImplementedException(),
            _ => int.MinValue,
        };

        if (next == int.MinValue)
        {
            return;
        }

        _ = Commit(Math.Clamp(next, 0, _count - 1));
        eventArgs.Handled = true;
    }

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (_dragging)
        {
            eventArgs.Handled = true;

            if (pointer.Action is PointerAction.Release or PointerAction.Leave)
            {
                CancelDrag(releaseCapture: true);
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
        eventArgs.Handled = true;

        if (CapturePointer())
        {
            _dragging = true;
            SetPressed(true);
        }
    }

    private void Select(Point point)
    {
        var bounds = ContentBounds;
        var column = Math.Min(_columns - 1, (point.X - bounds.X) * _columns / Math.Max(1, bounds.Width));
        var row = Math.Min(_rows - 1, (point.Y - bounds.Y) * _rows / Math.Max(1, bounds.Height));
        _ = Commit(Math.Clamp((row * _columns) + column, 0, _count - 1));
    }

    private bool Commit(int index)
    {
        if (SelectedIndex == index)
        {
            return false;
        }

        SelectedIndex = index;
        Invalidate(ChangeImpact.Render);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void CancelDrag(bool releaseCapture)
    {
        _dragging = false;
        SetPressed(false);

        if (releaseCapture && HasPointerCapture)
        {
            ReleasePointerCapture();
        }
    }
}
