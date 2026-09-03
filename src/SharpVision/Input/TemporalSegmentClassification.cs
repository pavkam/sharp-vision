// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

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

    /// <summary>Moves the AM/PM designator segment to the requested half of the day, if the current
    /// layout has one and the control currently has a value. Unlike <see cref="ToggleAmPm"/>, an
    /// "a" pressed while the value is already AM (or "p" while already PM) activates the designator
    /// segment without changing the value, so a repeated keystroke never flips the half of the day
    /// the user just asked for.</summary>
    /// <param name="segmentsProvider">Returns the current, possibly culture- or format-dependent, segment layout.</param>
    /// <param name="hasValue">Reports whether the owning control currently has a non-null value.</param>
    /// <param name="isPm">Reports whether the owning control's current value falls in the PM half of the day.</param>
    /// <param name="segments">The owning control's own segment navigation engine.</param>
    /// <param name="selectPm">True to select PM; false to select AM.</param>
    /// <returns>True when the designator was selected: the segment became active and, when the
    /// requested half differed, the value flipped. Moving the active-segment highlight is itself an
    /// observable change, so a repeated letter reports true and the owning field consumes it rather
    /// than leaking a key that visibly acted on the field to an ancestor. False when the layout has
    /// no designator or the field has no value.</returns>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public static bool SelectAmPm(
        [InstantHandle] Func<IReadOnlyList<SegmentDescriptor>> segmentsProvider,
        [InstantHandle] Func<bool> hasValue,
        [InstantHandle] Func<bool> isPm,
        SegmentFieldBehavior segments,
        bool selectPm)
    {
        ArgumentNullException.ThrowIfNull(segmentsProvider);
        ArgumentNullException.ThrowIfNull(hasValue);
        ArgumentNullException.ThrowIfNull(isPm);
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

        if (isPm() != selectPm)
        {
            _ = segments.Increment(1);
        }

        return true;
    }

    /// <summary>Classifies a typed character as the fixed "a"/"p" AM/PM selection shortcut,
    /// independent of the owning control's localized designator text.</summary>
    /// <param name="character">The typed character.</param>
    /// <param name="selectPm">Set to true for "p"/"P" and false for "a"/"A".</param>
    /// <returns>True when the character is an AM/PM selection shortcut.</returns>
    [Pure]
    public static bool TryGetAmPmSelection(Rune character, out bool selectPm)
    {
        selectPm = character.Value is 'p' or 'P';
        return selectPm || character.Value is 'a' or 'A';
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

    /// <summary>Resolves the text an editable AM/PM designator segment renders, substituting the
    /// invariant designator when the culture formats the half of the day as nothing at all.</summary>
    /// <param name="formatted">The designator text the culture produced for the value.</param>
    /// <param name="isPm">Whether the value falls in the PM half of the day.</param>
    /// <returns><paramref name="formatted"/> when it has any text; otherwise the invariant designator.</returns>
    /// <remarks>Some cultures declare an empty AM/PM designator. The segment stays editable (Up
    /// flips the half of the day, "a"/"p" jump to it), so rendering it as an empty run would leave
    /// the active segment as an invisible zero-width highlight with nothing to show which half the
    /// field is in. The invariant "AM"/"PM" keeps the editable state visible and hit-testable.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="formatted"/> is null.</exception>
    [Pure]
    public static string ResolveDesignatorText(string formatted, bool isPm)
    {
        ArgumentNullException.ThrowIfNull(formatted);

        return formatted.Length > 0
            ? formatted
            : isPm
                ? CultureInfo.InvariantCulture.DateTimeFormat.PMDesignator
                : CultureInfo.InvariantCulture.DateTimeFormat.AMDesignator;
    }

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

}
