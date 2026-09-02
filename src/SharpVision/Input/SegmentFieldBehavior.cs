// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>
/// Implements the shared active-segment navigation, digit-entry buffering, and pointer
/// hit-testing state machine used by every segmented temporal field control
/// (<see cref="Controls.Input.DateInput"/>, <see cref="Controls.Input.TimeInput"/>,
/// <see cref="Controls.Input.DateTimeInput"/>).
/// </summary>
/// <remarks>
/// A behavior is composed into a control the same way <see cref="PressBehavior"/> is: it owns no
/// rendering and reaches value arithmetic only through the delegates supplied at construction. The
/// owning control supplies the descriptor layout - which may itself be culture- and format-driven
/// via <see cref="TemporalPatternSegmenter"/> - and interprets requested edits against its own
/// value type, applying its own clamping and range repair. This keeps calendar and clock
/// arithmetic (leap years, day-in-month clamping, 12/24-hour wrap, and so on) entirely in each
/// control while the navigation, buffering, and hit-testing mechanics are written once.
/// </remarks>
internal sealed class SegmentFieldBehavior
{
    private readonly Func<IReadOnlyList<SegmentDescriptor>> _segmentsProvider;
    private readonly Func<TemporalSegmentKind, int, bool> _applyDigitValue;
    private readonly Func<TemporalSegmentKind, int, bool> _incrementSegment;
    private readonly Func<TemporalSegmentKind, bool> _clearSegment;
    private readonly Action _invalidate;

    private int? _digitBuffer;
    private int _digitCount;

    /// <summary>Initializes a behavior bound to a specific control's segment layout and value callbacks.</summary>
    /// <param name="segmentsProvider">Returns the current, possibly culture- or format-dependent, segment layout.</param>
    /// <param name="applyDigitValue">
    /// Applies a fully or partially typed numeric value to a segment's kind, clamping as the
    /// control sees fit, and returns whether the value actually changed.
    /// </param>
    /// <param name="incrementSegment">Applies a one-step increment (positive or negative delta) to a segment's kind and returns whether the value changed.</param>
    /// <param name="clearSegment">Resets a segment's kind to its lowest representable value and returns whether the value changed.</param>
    /// <param name="invalidate">Requests a render invalidation after a navigation-only change (one that does not itself change the value).</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public SegmentFieldBehavior(
        Func<IReadOnlyList<SegmentDescriptor>> segmentsProvider,
        Func<TemporalSegmentKind, int, bool> applyDigitValue,
        Func<TemporalSegmentKind, int, bool> incrementSegment,
        Func<TemporalSegmentKind, bool> clearSegment,
        Action invalidate)
    {
        ArgumentNullException.ThrowIfNull(segmentsProvider);
        ArgumentNullException.ThrowIfNull(applyDigitValue);
        ArgumentNullException.ThrowIfNull(incrementSegment);
        ArgumentNullException.ThrowIfNull(clearSegment);
        ArgumentNullException.ThrowIfNull(invalidate);

        _segmentsProvider = segmentsProvider;
        _applyDigitValue = applyDigitValue;
        _incrementSegment = incrementSegment;
        _clearSegment = clearSegment;
        _invalidate = invalidate;
    }

    /// <summary>Gets the zero-based index of the active segment among the layout's editable segments only.</summary>
    public int ActiveSegment { get; private set; }

    /// <summary>Gets whether the current layout has at least one editable segment.</summary>
    public bool HasEditableSegments => EditableSegments().Count > 0;

    /// <summary>Discards any partially typed digit, without moving the active segment or touching the value.</summary>
    public void ResetDigitBuffer()
    {
        _digitBuffer = null;
        _digitCount = 0;
    }

    /// <summary>Clamps the active segment index into range after the layout shrinks, such as when a control toggles a segment off.</summary>
    public void ClampActiveSegment()
    {
        var last = EditableSegments().Count - 1;
        ActiveSegment = last < 0 ? 0 : Math.Clamp(ActiveSegment, 0, last);
    }

    /// <summary>Moves the active segment to the first editable position, discarding any partial digit.</summary>
    public void ActivateFirstSegment()
    {
        ResetDigitBuffer();
        ActiveSegment = 0;
    }

    /// <summary>Moves the active segment directly to a known editable-segment index, such as one
    /// located via a keyboard shortcut that jumps to a specific kind (for example an "a"/"p"
    /// keystroke jumping straight to an AM/PM designator).</summary>
    /// <param name="index">The zero-based editable-segment index to activate.</param>
    public void ActivateSegment(int index)
    {
        var count = EditableSegments().Count;

        if (index < 0 || index >= count)
        {
            return;
        }

        var changed = index != ActiveSegment;
        ActiveSegment = index;
        ResetDigitBuffer();

        if (changed)
        {
            _invalidate();
        }
    }

    /// <summary>Moves the active segment by one position.</summary>
    /// <param name="direction">A negative value moves toward the first segment; a positive value moves toward the last.</param>
    /// <param name="wrap">Whether moving past an edge wraps to the opposite edge instead of stopping.</param>
    /// <returns>True if the active segment changed.</returns>
    public bool MoveSegment(int direction, bool wrap)
    {
        var count = EditableSegments().Count;

        if (count == 0)
        {
            return false;
        }

        var target = ActiveSegment + direction;

        if (wrap)
        {
            target = ((target % count) + count) % count;
        }
        else if (target < 0 || target >= count)
        {
            return false;
        }

        if (target == ActiveSegment)
        {
            return false;
        }

        ActiveSegment = target;
        ResetDigitBuffer();
        _invalidate();
        return true;
    }

    /// <summary>Moves the active segment directly to the first or last editable position.</summary>
    /// <param name="first">True to move to the first segment; false to move to the last.</param>
    /// <returns>True if the active segment changed.</returns>
    public bool MoveToEdge(bool first)
    {
        var count = EditableSegments().Count;

        if (count == 0)
        {
            return false;
        }

        var target = first ? 0 : count - 1;

        if (target == ActiveSegment)
        {
            return false;
        }

        ActiveSegment = target;
        ResetDigitBuffer();
        _invalidate();
        return true;
    }

    /// <summary>Applies a one-step increment to the active segment's value.</summary>
    /// <param name="delta">A positive or negative step, interpreted by the owning control.</param>
    /// <returns>True if the value changed.</returns>
    public bool Increment(int delta)
    {
        var editable = EditableSegments();

        if (ActiveSegment < 0 || ActiveSegment >= editable.Count)
        {
            return false;
        }

        ResetDigitBuffer();
        return _incrementSegment(editable[ActiveSegment].Kind!.Value, delta);
    }

    /// <summary>Buffers or commits a typed digit against the active segment.</summary>
    /// <param name="digit">The digit value, from 0 through 9.</param>
    /// <returns>True if the value changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="digit"/> is not between 0 and 9.</exception>
    public bool TypeDigit(int digit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(digit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);

        var editable = EditableSegments();

        if (ActiveSegment < 0 || ActiveSegment >= editable.Count)
        {
            return false;
        }

        var segment = editable[ActiveSegment];

        if (!segment.IsDigitTypable)
        {
            return false;
        }

        var kind = segment.Kind!.Value;

        if (_digitBuffer.HasValue)
        {
            var combined = (_digitBuffer.Value * 10) + digit;
            _digitCount++;

            // Segments wider than two digits (a four-digit year) keep buffering instead of
            // committing after the second keystroke, since a single- or double-digit prefix is
            // never itself a valid value for that segment.
            if (_digitCount < segment.DigitCapacity)
            {
                _digitBuffer = combined;
                return _applyDigitValue(kind, combined);
            }

            ResetDigitBuffer();
            var committed = _applyDigitValue(kind, combined);

            if (ActiveSegment < editable.Count - 1)
            {
                ActiveSegment++;
                _invalidate();
            }

            return committed;
        }

        if (digit > segment.FirstDigitOverflowThreshold)
        {
            var committed = _applyDigitValue(kind, digit);

            if (ActiveSegment < editable.Count - 1)
            {
                ActiveSegment++;
                _invalidate();
            }

            return committed;
        }

        _digitBuffer = digit;
        _digitCount = 1;
        return _applyDigitValue(kind, digit);
    }

    /// <summary>Resets the active segment's value to its lowest representable value.</summary>
    /// <returns>True if the value changed.</returns>
    public bool ClearActiveSegment()
    {
        var editable = EditableSegments();

        if (ActiveSegment < 0 || ActiveSegment >= editable.Count)
        {
            return false;
        }

        ResetDigitBuffer();
        return _clearSegment(editable[ActiveSegment].Kind!.Value);
    }

    /// <summary>Classifies and applies one routed key through this field's shared navigation and
    /// edit state, while leaving popup and typed-value policy in the owning control.</summary>
    /// <param name="eventArgs">The routed key event.</param>
    /// <param name="options">The owning control's typed commands and handled policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> or <paramref name="options"/> is null.</exception>
    public void HandleKey(KeyEventArgs eventArgs, SegmentFieldKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        ArgumentNullException.ThrowIfNull(options);

        if (!eventArgs.IsKeyDown)
        {
            return;
        }

        var popupResult = options.HandlePopupCommand?.Invoke(eventArgs);

        if (popupResult.HasValue)
        {
            eventArgs.IsHandled = popupResult.Value;
            return;
        }

        var stepDelta = options.ResolveStepDelta(eventArgs);

        if (stepDelta.HasValue)
        {
            var stepChanged = HasEditableSegments && Increment(stepDelta.Value);
            eventArgs.IsHandled = stepChanged || (HasEditableSegments && options.HandleRecognizedWithoutChange);
            return;
        }

        var stroke = eventArgs.Stroke;
        var recognized = false;
        var changed = false;

#pragma warning disable IDE0010, IDE0072 // Unknown or unsupported keys intentionally remain unhandled.
        switch (stroke.Code)
        {
            case Code.Left:
                recognized = HasEditableSegments;
                changed = recognized && MoveSegment(-1, wrap: false);
                break;
            case Code.Right:
                recognized = HasEditableSegments;
                changed = recognized && MoveSegment(1, wrap: false);
                break;
            case Code.Home:
                recognized = HasEditableSegments;
                changed = recognized && MoveToEdge(first: true);
                break;
            case Code.End:
                recognized = HasEditableSegments;
                changed = recognized && MoveToEdge(first: false);
                break;
            case Code.Delete:
                // Delete is a whole-value clearing command, not a segment edit: it stays
                // available under a layout with no editable segments at all (a literal-only
                // Format such as "'choose date'"), where it must still clear the value. It is
                // recognized - consumed even without an observable change, subject to the
                // owner's policy - whenever editable segments exist, matching Backspace and
                // the navigation arms, so an already-empty field does not leak the key to
                // ancestors.
                changed = options.ClearValue();
                recognized = changed || HasEditableSegments;
                break;
            case Code.Backspace:
                recognized = HasEditableSegments;
                changed = recognized && ClearActiveSegment();
                break;
            case Code.Character when stroke.Character is { } character &&
                KeyboardModifierPolicy.IsTextEntryEligible(stroke.Modifiers):
                if (TemporalSegmentClassification.IsDigit(character))
                {
                    recognized = HasEditableSegments;
                    changed = recognized && TypeDigit(character.Value - '0');
                }
                else if (options.HandleCharacterCommand is not null)
                {
                    recognized = true;
                    changed = options.HandleCharacterCommand(character);
                }

                break;
            default:
                break;
        }
#pragma warning restore IDE0010, IDE0072

        eventArgs.IsHandled = changed || (recognized && options.HandleRecognizedWithoutChange);
    }

    /// <summary>Activates the segment under a primary pointer press and performs focus transfer
    /// only while the owning control remains available.</summary>
    /// <param name="eventArgs">The routed pointer event.</param>
    /// <param name="segmentBox">The rendered segment box, excluding affixes and popup indicators.</param>
    /// <param name="ambiguousWidth">The ambient ambiguous-width policy used to render the box.</param>
    /// <param name="isFocused">Whether the owning control currently has focus.</param>
    /// <param name="requestFocus">Requests focus for the owner.</param>
    /// <param name="canContinueAfterFocus">Reports whether focus callbacks left the owner available.</param>
    /// <exception cref="ArgumentNullException">An event or callback is null.</exception>
    public void HandlePointer(
        PointerEventArgs eventArgs,
        Rect segmentBox,
        Ambiguous ambiguousWidth,
        bool isFocused,
        Func<bool> requestFocus,
        Func<bool> canContinueAfterFocus)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        ArgumentNullException.ThrowIfNull(requestFocus);
        ArgumentNullException.ThrowIfNull(canContinueAfterFocus);

        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Press ||
            (pointer.Buttons & Buttons.Primary) == 0 ||
            pointer.Cells is not { } cells ||
            !segmentBox.Contains(cells))
        {
            return;
        }

        if (!ActivateSegmentAtColumn(cells.X - segmentBox.X, ambiguousWidth))
        {
            return;
        }

        if (!isFocused)
        {
            _ = requestFocus();

            if (!canContinueAfterFocus())
            {
                return;
            }
        }

        eventArgs.IsHandled = true;
    }

    /// <summary>Activates the editable segment occupying a rendered column, discarding any partial digit.</summary>
    /// <param name="column">The zero-based column within the field's content area.</param>
    /// <param name="ambiguousWidth">
    /// The owning control's ambient East Asian Ambiguous width policy, matching the policy its
    /// canvas rendered the same segments under.
    /// </param>
    /// <returns>True if an editable segment occupies that column and became active.</returns>
    public bool ActivateSegmentAtColumn(int column, Ambiguous ambiguousWidth = Ambiguous.Narrow)
    {
        var index = SegmentIndexAtColumn(column, ambiguousWidth);

        if (index < 0)
        {
            return false;
        }

        ActiveSegment = index;
        ResetDigitBuffer();
        _invalidate();
        return true;
    }

    /// <summary>Resolves the editable-segment index whose rendered column range contains a column.</summary>
    /// <param name="column">The zero-based column within the field's content area.</param>
    /// <param name="ambiguousWidth">
    /// The owning control's ambient East Asian Ambiguous width policy, matching the policy its
    /// canvas rendered the same segments under.
    /// </param>
    /// <returns>The editable segment index, or -1 when the layout has no editable segments.</returns>
    [Pure]
    public int SegmentIndexAtColumn(int column, Ambiguous ambiguousWidth = Ambiguous.Narrow)
    {
        var running = 0;
        var threshold = 0;
        var editableIndex = -1;

        foreach (var segment in _segmentsProvider())
        {
            running += Width.Measure(segment.Text, ambiguousWidth).Cells;

            if (segment.IsEditable)
            {
                threshold = running;
                editableIndex++;
            }

            if (column < threshold)
            {
                return editableIndex;
            }
        }

        return editableIndex;
    }

    private List<SegmentDescriptor> EditableSegments()
    {
        var segments = _segmentsProvider();
        var editable = new List<SegmentDescriptor>(segments.Count);

        foreach (var segment in segments)
        {
            if (segment.IsEditable)
            {
                editable.Add(segment);
            }
        }

        return editable;
    }
}
