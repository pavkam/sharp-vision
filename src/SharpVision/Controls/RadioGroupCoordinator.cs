// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

/// <summary>Coordinates radio membership, exclusive selection, and roving tab entry.</summary>
/// <remarks>Membership is resolved from the current ownership root for named groups and from the
/// exact owning slot for unnamed groups. This keeps mutually exclusive behavior independent from
/// render-tree shape while retaining a single authority for each semantic radio group.</remarks>
internal static class RadioGroupCoordinator
{
    /// <summary>Clears one selected member and leaves its group empty.</summary>
    /// <param name="value">The non-null member to clear.</param>
    /// <param name="cause">The defined selection cause.</param>
    internal static void Clear(RadioButton value, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(cause);
        var eventArgs = new SelectionChangedEventArgs(value, current: null, cause);
        var version = value.StageChecked(false);

        if (version == 0)
        {
            return;
        }

        var failure = (ExceptionDispatchInfo?) null;
        CaptureFailure(value.PublishChecked, ref failure);

        if (value.IsCheckedCommitCurrent(version, value: false))
        {
            CaptureFailure(() => value.RaiseUnchecked(eventArgs), ref failure);
        }

        if (value.IsCheckedCommitCurrent(version, value: false))
        {
            CaptureFailure(() => value.RaiseSelectionChanged(eventArgs), ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Selects one member after staging the complete mutually exclusive group.</summary>
    /// <param name="value">The non-null member to select.</param>
    /// <param name="cause">The defined selection cause.</param>
    internal static void Select(RadioButton value, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(cause);
        RadioButton? previous = null;

        foreach (var member in Members(value))
        {
            if (!ReferenceEquals(member, value) && member.IsChecked)
            {
                previous = member;
                break;
            }
        }

        var changed = !value.IsChecked;

        if (previous is null && !changed)
        {
            return;
        }

        var eventArgs = new SelectionChangedEventArgs(previous, value, cause);
        var previousVersion = previous?.StageChecked(false) ?? 0;
        var currentVersion = changed ? value.StageChecked(true) : 0;
        var failure = (ExceptionDispatchInfo?) null;

        if (previous is not null && previous.IsCheckedCommitCurrent(previousVersion, value: false))
        {
            CaptureFailure(previous.PublishChecked, ref failure);
        }

        if (changed && value.IsCheckedCommitCurrent(currentVersion, value: true))
        {
            CaptureFailure(value.PublishChecked, ref failure);
        }

        if (previous is not null &&
            previous.IsCheckedCommitCurrent(previousVersion, value: false) &&
            value.IsChecked)
        {
            CaptureFailure(() => previous.RaiseUnchecked(eventArgs), ref failure);
        }

        if (changed && value.IsCheckedCommitCurrent(currentVersion, value: true))
        {
            CaptureFailure(() => value.RaiseChecked(eventArgs), ref failure);
        }

        if ((!changed && value.IsChecked) || value.IsCheckedCommitCurrent(currentVersion, value: true))
        {
            CaptureFailure(() => value.RaiseSelectionChanged(eventArgs), ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Moves selection and focus through eligible group order with wrapping.</summary>
    /// <param name="value">The non-null current member.</param>
    /// <param name="reverse">Whether to traverse toward the preceding member.</param>
    /// <returns>True when one eligible member accepted focus and selection.</returns>
    internal static bool Move(RadioButton value, bool reverse)
    {
        ArgumentNullException.ThrowIfNull(value);
        var members = Members(value);
        var eligible = members.FindAll(static member =>
            !member.IsDisposed &&
            member.EffectiveIsEnabled &&
            member.EffectiveIsVisible &&
            member.CanFocus);

        if (eligible.Count == 0)
        {
            return false;
        }

        var current = eligible.FindIndex(member => ReferenceEquals(member, value));
        var next = reverse
            ? (current <= 0 ? eligible.Count - 1 : current - 1)
            : (current < 0 || current == eligible.Count - 1 ? 0 : current + 1);
        var target = eligible[next];

        if (!target.RequestGroupFocus())
        {
            return false;
        }

        Select(target, ActivationCause.Keyboard);
        return true;
    }

    /// <summary>Gets whether one eligible member is the group's effective sequential entry.</summary>
    /// <param name="value">The non-null member to inspect.</param>
    /// <returns>Whether this is the checked eligible member, or the first eligible member when none is checked.</returns>
    internal static bool IsRovingTabStop(RadioButton value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var eligible = Members(value).Where(static member =>
            !member.IsDisposed &&
            member.EffectiveIsEnabled &&
            member.EffectiveIsVisible &&
            member.TabStop &&
            member.CanFocus).ToList();

        if (eligible.Count == 0)
        {
            return false;
        }

        var selected = eligible.FirstOrDefault(static member => member.IsChecked);
        return ReferenceEquals(selected ?? eligible[0], value);
    }

    private static void CaptureFailure(Action action, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    private static List<RadioButton> Members(RadioButton value)
    {
        List<RadioButton> result = [];

        if (value.GroupName is null)
        {
            if (value.OwningSlot is not { } slot)
            {
                result.Add(value);
            }
            else
            {
                foreach (var child in slot.Items)
                {
                    if (child is RadioButton { GroupName: null } member)
                    {
                        result.Add(member);
                    }
                }
            }

            return result;
        }

        Control root = value;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        Collect(root, value.GroupName, result);

        if (!result.Contains(value))
        {
            result.Add(value);
        }

        return result;
    }

    private static void Collect(Control control, string groupName, List<RadioButton> result)
    {
        Debug.Assert(control is not null, "Radio group collection requires a non-null root.");
        Debug.Assert(groupName is not null, "Radio group collection requires a non-null group name.");
        Debug.Assert(result is not null, "Radio group collection requires a non-null result list.");

        if (control is RadioButton member &&
            string.Equals(member.GroupName, groupName, StringComparison.Ordinal))
        {
            result.Add(member);
        }

        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            Collect(control.OwnedControlAt(index), groupName, result);
        }
    }

    private static void Validate(ActivationCause cause)
    {
        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), cause, "The activation cause is unknown.");
        }
    }
}
