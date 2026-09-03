// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using SharpVision.Surfaces;

/// <summary>Coordinates radio membership, exclusive selection, roving tab entry, and group navigation.</summary>
/// <remarks>Membership is resolved from the current ownership root for named groups and from the
/// exact owning slot for unnamed groups. This keeps mutually exclusive behavior independent from
/// render-tree shape while retaining a single authority for each semantic radio group.</remarks>
internal static class RadioGroupCoordinator
{
    extension(RadioButton value)
    {
        /// <summary>Clears one selected member and leaves its group empty.</summary>
        /// <param name="cause">The defined selection cause.</param>
        public void ClearGroup(ActivationCause cause)
        {
            ArgumentNullException.ThrowIfNull(value);
            Validate(cause);
            var eventArgs = new RadioButtonSelectionChangedEventArgs(value, current: null, cause);
            var version = value.StageChecked(false);

            if (version == 0)
            {
                return;
            }

            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(value.PublishChecked, ref failure);

            if (value.IsCheckedCommitCurrent(version, value: false))
            {
                ExceptionAggregation.Capture(() => value.RaiseUnchecked(eventArgs), ref failure);
            }

            if (value.IsCheckedCommitCurrent(version, value: false))
            {
                ExceptionAggregation.Capture(() => value.RaiseSelectionChanged(eventArgs), ref failure);
            }

            failure?.Throw();
        }

        /// <summary>Selects one member after staging the complete mutually exclusive group.</summary>
        /// <param name="cause">The defined selection cause.</param>
        public void SelectInGroup(ActivationCause cause)
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

            var eventArgs = new RadioButtonSelectionChangedEventArgs(previous, value, cause);
            var groupName = value.GroupName;
            var owningSlot = value.OwningSlot;
            var groupRoot = groupName is null ? null : FindGroupRoot(value);
            var previousVersion = previous?.StageChecked(false) ?? 0;
            var currentVersion = changed ? value.StageChecked(true) : 0;
            ExceptionDispatchInfo? failure = null;

            if (previous is not null &&
                IsCurrentMember(previous, groupName, owningSlot, groupRoot) &&
                previous.IsCheckedCommitCurrent(previousVersion, value: false))
            {
                ExceptionAggregation.Capture(previous.PublishChecked, ref failure);
            }

            if (changed &&
                IsCurrentMember(value, groupName, owningSlot, groupRoot) &&
                value.IsCheckedCommitCurrent(currentVersion, value: true))
            {
                ExceptionAggregation.Capture(value.PublishChecked, ref failure);
            }

            if (previous is not null &&
                IsCurrentMember(previous, groupName, owningSlot, groupRoot) &&
                previous.IsCheckedCommitCurrent(previousVersion, value: false) &&
                IsCurrentMember(value, groupName, owningSlot, groupRoot) &&
                value.IsChecked)
            {
                ExceptionAggregation.Capture(() => previous.RaiseUnchecked(eventArgs), ref failure);
            }

            if (changed &&
                IsCurrentMember(value, groupName, owningSlot, groupRoot) &&
                value.IsCheckedCommitCurrent(currentVersion, value: true))
            {
                ExceptionAggregation.Capture(() => value.RaiseChecked(eventArgs), ref failure);
            }

            if (IsCurrentMember(value, groupName, owningSlot, groupRoot) &&
                ((!changed && value.IsChecked) || value.IsCheckedCommitCurrent(currentVersion, value: true)))
            {
                ExceptionAggregation.Capture(() => value.RaiseSelectionChanged(eventArgs), ref failure);
            }

            failure?.Throw();
        }

        /// <summary>Moves selection and focus through eligible group order with wrapping.</summary>
        /// <param name="reverse">Whether to traverse toward the preceding member.</param>
        /// <returns>True when one eligible member accepted focus and selection.</returns>
        public bool MoveGroup(bool reverse)
        {
            ArgumentNullException.ThrowIfNull(value);
            var eligible = EligibleMembers(value);

            if (eligible.Count == 0)
            {
                return false;
            }

            var current = eligible.FindIndex(member => ReferenceEquals(member, value));
            var next = reverse
                ? current <= 0 ? eligible.Count - 1 : current - 1
                : current < 0 || current == eligible.Count - 1 ? 0 : current + 1;
            return FocusAndSelect(value, eligible[next]);
        }

        /// <summary>Moves selection and focus to an eligible group endpoint.</summary>
        /// <param name="end">Whether to select the last rather than first eligible member.</param>
        /// <returns>True when one endpoint accepted focus and remained in the group.</returns>
        public bool MoveGroupEndpoint(bool end)
        {
            ArgumentNullException.ThrowIfNull(value);
            var eligible = EligibleMembers(value);

            return eligible.Count != 0 && FocusAndSelect(value, end ? eligible[^1] : eligible[0]);
        }

        /// <summary>Gets whether one eligible member is the group's effective sequential entry.</summary>
        /// <returns>Whether this is the checked eligible member, or the first eligible member when none is checked.</returns>
        public bool IsRovingTabStop()
        {
            ArgumentNullException.ThrowIfNull(value);
            var eligible = Members(value).Where(static member =>
                member is
                {
                    IsDisposed: false, EffectiveIsEnabled: true, EffectiveIsVisible: true, IsTabStop: true, CanFocus: true
                }).ToList();

            if (eligible.Count == 0)
            {
                return false;
            }

            var selected = eligible.FirstOrDefault(static member => member.IsChecked);
            return ReferenceEquals(selected ?? eligible[0], value);
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
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

        // Every window/popup/dialog ultimately attaches under the one process-wide Screen
        // (directly, or via PresentationHost walking up to it), so climbing all the way to the
        // true root would resolve a named group across every currently open top-level surface
        // instead of scoping it to the one that owns the group — stop at the nearest enclosing
        // FloatingSurfaceBase (Window or Popup) instead, mirroring how Menu.SelectRadio scopes to
        // its own Items. A control with no enclosing surface (content attached
        // directly to the Screen) keeps the prior root-of-tree behavior.
        var root = FindGroupRoot(value);

        Collect(root, value.GroupName, result);

        if (!result.Contains(value))
        {
            result.Add(value);
        }

        return result;
    }

    [Pure]
    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static List<RadioButton> EligibleMembers(RadioButton value) => Members(value).FindAll(static member =>
        member is { IsDisposed: false, EffectiveIsEnabled: true, EffectiveIsVisible: true, CanFocus: true });

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static bool FocusAndSelect(RadioButton value, RadioButton target)
    {
        Debug.Assert(value is not null, "Group movement requires a source member.");
        Debug.Assert(target is not null, "Group movement requires an eligible target member.");

        if (!target.RequestGroupFocus())
        {
            return false;
        }

        if (!target.IsFocused || !target.CanFocus || !Members(value).Contains(target))
        {
            return false;
        }

        target.SelectInGroup(ActivationCause.Keyboard);
        return true;
    }

    [Pure]
    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static bool IsCurrentMember(
        RadioButton member,
        string? groupName,
        OwnedControlSlot? owningSlot,
        ControlBase? groupRoot)
    {
        return !member.IsDisposed &&
            string.Equals(member.GroupName, groupName, StringComparison.Ordinal) &&
            (groupName is null
                ? ReferenceEquals(member.OwningSlot, owningSlot)
                : ReferenceEquals(FindGroupRoot(member), groupRoot));
    }

    [Pure]
    private static ControlBase FindGroupRoot(RadioButton value)
    {
        ControlBase root = value;

        while (root is not FloatingSurfaceBase && root.Parent is { } parent)
        {
            root = parent;
        }

        return root;
    }

    private static void Collect(ControlBase control, string groupName, List<RadioButton> result)
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

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void Validate(ActivationCause cause) =>
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause, nameof(cause), "The activation cause is unknown.");
}
