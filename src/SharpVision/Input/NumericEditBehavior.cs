// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Owns the routed buffer-editing, pointer-caret, and focus-transition lifecycle shared by
/// decimal numeric input controls.</summary>
internal sealed class NumericEditBehavior
{
    private readonly NumericEditBuffer _buffer;
    private readonly NumericInputCommitCoordinator _coordinator;
    private readonly Action _configureBuffer;
    private readonly Func<int> _getDecimalPlaces;
    private readonly Func<bool> _isFocused;
    private readonly Func<Point, bool> _containsContentPoint;
    private readonly Func<bool> _requestEditingFocus;
    private readonly Func<Point, int> _resolveCaretIndex;
    private readonly Action _invalidateRender;

    /// <summary>Initializes behavior around one retained buffer and its control-specific geometry
    /// and formatting policies.</summary>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public NumericEditBehavior(
        NumericEditBuffer buffer,
        NumericInputCommitCoordinator coordinator,
        Action configureBuffer,
        Func<int> getDecimalPlaces,
        Func<bool> isFocused,
        Func<Point, bool> containsContentPoint,
        Func<bool> requestEditingFocus,
        Func<Point, int> resolveCaretIndex,
        Action invalidateRender)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(configureBuffer);
        ArgumentNullException.ThrowIfNull(getDecimalPlaces);
        ArgumentNullException.ThrowIfNull(isFocused);
        ArgumentNullException.ThrowIfNull(containsContentPoint);
        ArgumentNullException.ThrowIfNull(requestEditingFocus);
        ArgumentNullException.ThrowIfNull(resolveCaretIndex);
        ArgumentNullException.ThrowIfNull(invalidateRender);

        _buffer = buffer;
        _coordinator = coordinator;
        _configureBuffer = configureBuffer;
        _getDecimalPlaces = getDecimalPlaces;
        _isFocused = isFocused;
        _containsContentPoint = containsContentPoint;
        _requestEditingFocus = requestEditingFocus;
        _resolveCaretIndex = resolveCaretIndex;
        _invalidateRender = invalidateRender;
    }

    /// <summary>Processes one routed editing event.</summary>
    /// <returns>Whether the event was handled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    public bool HandleEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        _configureBuffer();

        return eventArgs switch
        {
            KeyEventArgs key => HandleKey(key),
            TextEventArgs text => InsertText(text, text.Text.Value.ToString()),
            PasteEventArgs paste => InsertPaste(paste),
            PointerEventArgs pointer => HandlePointer(pointer),
            _ => false
        };
    }

    /// <summary>Synchronizes the edit buffer across one committed focus transition.</summary>
    public void FocusChanged(bool focused)
    {
        if (focused)
        {
            _configureBuffer();
            _ = _coordinator.RevertBuffer();
        }
        else
        {
            _ = _coordinator.CommitBuffer();
        }

        _invalidateRender();
    }

    private bool HandleKey(KeyEventArgs eventArgs)
    {
        if (!eventArgs.IsKeyDown)
        {
            return false;
        }

        var stroke = eventArgs.Stroke;

        if (stroke.Code is Code.Up or Code.Down)
        {
            _ = _coordinator.ApplyStep(stroke.Code == Code.Up ? 1 : -1);
            eventArgs.IsHandled = true;
            return true;
        }

#pragma warning disable IDE0072 // Every unmatched key intentionally remains unhandled.
        var handled = stroke.Code switch
        {
            Code.Home => _coordinator.JumpToBound(minimum: true, _getDecimalPlaces()),
            Code.End => _coordinator.JumpToBound(minimum: false, _getDecimalPlaces()),
            Code.Enter => _coordinator.CommitBuffer(),
            Code.Escape when stroke.Modifiers.IsActivationEligible() => _coordinator.RevertBuffer(),
            Code.Backspace => _buffer.Backspace(),
            Code.Delete => _buffer.Delete(),
            Code.Left => _buffer.MovePrevious(extend: false),
            Code.Right => _buffer.MoveNext(extend: false),
            Code.Character when stroke.Character is { } ch &&
                KeyboardModifierPolicy.IsTextEntryEligible(stroke.Modifiers) => _buffer.Insert(ch.ToString()),
            _ => false
        };
#pragma warning restore IDE0072

        if (handled)
        {
            eventArgs.IsHandled = true;
            _invalidateRender();
        }

        return handled;
    }

    private bool InsertText(TextEventArgs eventArgs, string text)
    {
        _ = _buffer.Insert(text);
        eventArgs.IsHandled = true;
        _invalidateRender();
        return true;
    }

    private bool InsertPaste(PasteEventArgs eventArgs)
    {
        _ = _buffer.Insert(Encoding.UTF8.GetString(eventArgs.Paste.Utf8.Span));
        eventArgs.IsHandled = true;
        _invalidateRender();
        return true;
    }

    private bool HandlePointer(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Press ||
            (pointer.Buttons & Buttons.Primary) == 0 ||
            pointer.Cells is not { } cells ||
            !_containsContentPoint(cells))
        {
            return false;
        }

        if (!_isFocused() && !_requestEditingFocus())
        {
            return false;
        }

        _buffer.SetCaret(_resolveCaretIndex(cells));
        _invalidateRender();
        eventArgs.IsHandled = true;
        return true;
    }
}
