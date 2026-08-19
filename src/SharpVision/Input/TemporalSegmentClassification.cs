// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;

/// <summary>
/// Implements the shared AM/PM designator toggle, pointer-driven segment activation, and
/// digit/AM-PM keystroke classification used by every 12-hour-capable segmented temporal field
/// control (<see cref="Controls.Input.TimeInput"/>, <see cref="Controls.Input.DateTimeInput"/>).
/// </summary>
/// <remarks>
/// Every member here is either a pure function or reaches the owning control's segment layout,
/// active-value presence, and focus/pointer plumbing only through the parameters and delegates a
/// call site supplies - the same stateless composition <see cref="TemporalPatternSegmenter"/>
/// already uses for pattern parsing. This keeps each control's own value type (<see
/// cref="TimeOnly"/> versus <see cref="DateTime"/>) and its own <see
/// cref="ControlBase.RequestFocus"/>/<see cref="ControlBase.ContentBounds"/>
/// wiring entirely in the control while the AM/PM mechanics are written once. Unlike <see
/// cref="SegmentFieldBehavior"/>, which owns per-instance navigation state (the active segment
/// index and digit buffer), nothing here needs to persist between calls, so this stays a plain
/// static toolkit instead of a constructed instance every control would otherwise have to store
/// and dispose alongside its own <see cref="SegmentFieldBehavior"/>.
/// </remarks>
internal static class TemporalSegmentClassification
{
    /// <summary>Advances the AM/PM designator segment one step, if the current layout has one and
    /// the control currently has a value.</summary>
    /// <param name="segmentsProvider">Returns the current, possibly culture- or format-dependent, segment layout.</param>
    /// <param name="hasValue">Reports whether the owning control currently has a non-null value.</param>
    /// <param name="segments">The owning control's own segment navigation engine.</param>
    /// <returns>True if the value changed.</returns>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public static bool ToggleAmPm(
        [InstantHandle] Func<IReadOnlyList<SegmentDescriptor>> segmentsProvider,
        [InstantHandle] Func<bool> hasValue,
        SegmentFieldBehavior segments)
    {
        ArgumentNullException.ThrowIfNull(segmentsProvider);
        ArgumentNullException.ThrowIfNull(hasValue);
        ArgumentNullException.ThrowIfNull(segments);

        if (!hasValue())
        {
            return false;
        }

        var index = FindEditableIndex(segmentsProvider, TemporalSegmentKind.AmPmDesignator);

        if (index < 0)
        {
            return false;
        }

        segments.ActivateSegment(index);
        return segments.Increment(1);
    }

    /// <summary>Finds the zero-based editable-segment index of the first segment of a given kind
    /// in the current layout.</summary>
    /// <param name="segmentsProvider">Returns the current, possibly culture- or format-dependent, segment layout.</param>
    /// <param name="kind">The segment kind to locate.</param>
    /// <returns>The editable-segment index, or -1 when the layout has no segment of that kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="segmentsProvider"/> is null.</exception>
    [Pure]
    public static int FindEditableIndex(
        [InstantHandle] Func<IReadOnlyList<SegmentDescriptor>> segmentsProvider,
        TemporalSegmentKind kind)
    {
        ArgumentNullException.ThrowIfNull(segmentsProvider);

        var editableIndex = -1;

        foreach (var segment in segmentsProvider())
        {
            if (!segment.IsEditable)
            {
                continue;
            }

            editableIndex++;

            if (segment.Kind == kind)
            {
                return editableIndex;
            }
        }

        return -1;
    }

    /// <summary>Gets whether the current layout - whether derived from a 24-hour toggle or
    /// overridden by a custom pattern - includes an AM/PM designator segment, used as the
    /// effective 12-versus-24-hour policy for editing the hour segment.</summary>
    /// <param name="segmentsProvider">Returns the current, possibly culture- or format-dependent, segment layout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="segmentsProvider"/> is null.</exception>
    [Pure]
    public static bool HasAmPmDesignator([InstantHandle] Func<IReadOnlyList<SegmentDescriptor>> segmentsProvider) =>
        FindEditableIndex(segmentsProvider, TemporalSegmentKind.AmPmDesignator) >= 0;

    /// <summary>Reports whether a typed character is an ASCII decimal digit.</summary>
    [Pure]
    public static bool IsDigit(Rune character) =>
        character.Value is >= '0' and <= '9';

    /// <summary>Reports whether a typed character is the fixed "a"/"p" AM/PM toggle shortcut,
    /// independent of <see cref="Controls.Input.TimeInput.Culture"/> or <see
    /// cref="Controls.Input.DateTimeInput.Culture"/>'s own localized designator text.</summary>
    [Pure]
    public static bool IsAmPmToggle(Rune character) =>
        character.Value is 'a' or 'A' or 'p' or 'P';

    /// <summary>Converts a clamped 1-12 hour and an AM/PM flag to its 0-23 24-hour equivalent.</summary>
    /// <param name="hour12">The 1-12 hour value.</param>
    /// <param name="isPm">Whether the hour is in the PM half of the day.</param>
    [Pure]
    public static int To24Hour(int hour12, bool isPm) =>
        hour12 == 12 ? isPm ? 12 : 0 : isPm ? hour12 + 12 : hour12;

    /// <summary>Activates the editable segment under a primary pointer press and requests focus,
    /// the shared pointer-hit-testing entry point every segmented temporal field with inline
    /// editing uses.</summary>
    /// <param name="eventArgs">The routed pointer event.</param>
    /// <param name="segmentBox">
    /// The box segment text is actually drawn into - already deflated for any active start/end
    /// <see cref="Affix"/> and, for a popup-bearing control, already excluding the
    /// drop-down indicator's own reserved columns - so a press over an affix or the indicator
    /// never mis-activates a segment.
    /// </param>
    /// <param name="ambiguousWidth">
    /// The owning control's ambient East Asian Ambiguous width policy, matching the policy its
    /// canvas rendered the same segments under.
    /// </param>
    /// <param name="segments">The owning control's own segment navigation engine.</param>
    /// <param name="isFocused">Whether the owning control currently has focus.</param>
    /// <param name="requestFocus">Requests focus for the owning control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/>, <paramref name="segments"/>, or <paramref name="requestFocus"/> is null.</exception>
    public static void HandlePointer(
        PointerEventArgs eventArgs,
        Rect segmentBox,
        Ambiguous ambiguousWidth,
        SegmentFieldBehavior segments,
        bool isFocused,
        [InstantHandle] Func<bool> requestFocus)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(requestFocus);

        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Press || (pointer.Buttons & Buttons.Primary) == 0)
        {
            return;
        }

        if (pointer.Cells is not { } cells)
        {
            return;
        }

        if (!segmentBox.Contains(cells))
        {
            return;
        }

        var localX = cells.X - segmentBox.X;
        _ = segments.ActivateSegmentAtColumn(localX, ambiguousWidth);

        if (!isFocused)
        {
            _ = requestFocus();
        }

        eventArgs.IsHandled = true;
    }
}
