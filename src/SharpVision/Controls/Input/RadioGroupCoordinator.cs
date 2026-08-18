// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using SharpVision.Surfaces;

/// <summary>Coordinates radio membership, exclusive selection, and roving tab entry.</summary>
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
            var previousVersion = previous?.StageChecked(false) ?? 0;
            var currentVersion = changed ? value.StageChecked(true) : 0;
            ExceptionDispatchInfo? failure = null;

            if (previous is not null && previous.IsCheckedCommitCurrent(previousVersion, value: false))
            {
                ExceptionAggregation.Capture(previous.PublishChecked, ref failure);
            }

            if (changed && value.IsCheckedCommitCurrent(currentVersion, value: true))
            {
                ExceptionAggregation.Capture(value.PublishChecked, ref failure);
            }

            if (previous is not null &&
                previous.IsCheckedCommitCurrent(previousVersion, value: false) &&
                value.IsChecked)
            {
                ExceptionAggregation.Capture(() => previous.RaiseUnchecked(eventArgs), ref failure);
            }

            if (changed && value.IsCheckedCommitCurrent(currentVersion, value: true))
            {
                ExceptionAggregation.Capture(() => value.RaiseChecked(eventArgs), ref failure);
            }

            if ((!changed && value.IsChecked) || value.IsCheckedCommitCurrent(currentVersion, value: true))
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
            var members = Members(value);
            var eligible = members.FindAll(static member =>
                member is { IsDisposed: false, EffectiveIsEnabled: true, EffectiveIsVisible: true, CanFocus: true });

            if (eligible.Count == 0)
            {
                return false;
            }

            var current = eligible.FindIndex(member => ReferenceEquals(member, value));
            var next = reverse
                ? current <= 0 ? eligible.Count - 1 : current - 1
                : current < 0 || current == eligible.Count - 1 ? 0 : current + 1;
            var target = eligible[next];

            if (!target.RequestGroupFocus())
            {
                return false;
            }

            target.SelectInGroup(ActivationCause.Keyboard);
            return true;
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
        ControlBase root = value;

        while (root is not FloatingSurfaceBase && root.Parent is { } parent)
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
