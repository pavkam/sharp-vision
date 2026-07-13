// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


/// <summary>Coordinates RadioButton selection by scanning the current owned tree.</summary>
internal static class RadioGroup
{
    /// <summary>Clears one selected member and leaves its group empty.</summary>
    internal static void Clear(RadioButton value, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!value.Commit(false))
        {
            return;
        }

        SelectionChangedEventArgs eventArgs = new(value, current: null, cause);
        value.RaiseUnchecked(eventArgs);
        value.RaiseSelectionChanged(eventArgs);
    }

    /// <summary>Selects one member after atomically clearing its current peer.</summary>
    internal static void Select(RadioButton value, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(value);
        RadioButton? previous = null;

        foreach (RadioButton member in Members(value))
        {
            if (!ReferenceEquals(member, value) && member.IsChecked)
            {
                previous = member;
                break;
            }
        }

        bool changed = !value.IsChecked;
        _ = previous?.Commit(false);
        _ = value.Commit(true);

        if (previous is null && !changed)
        {
            return;
        }

        SelectionChangedEventArgs eventArgs = new(previous, value, cause);
        previous?.RaiseUnchecked(eventArgs);

        if (!value.IsChecked)
        {
            return;
        }

        if (changed)
        {
            value.RaiseChecked(eventArgs);
        }

        if (value.IsChecked)
        {
            value.RaiseSelectionChanged(eventArgs);
        }
    }

    /// <summary>Moves selection and focus through eligible group order with wrapping.</summary>
    internal static bool Move(RadioButton value, bool reverse)
    {
        ArgumentNullException.ThrowIfNull(value);
        List<RadioButton> members = Members(value);
        List<RadioButton> eligible = members.FindAll(static member =>
            !member.IsDisposed &&
            member.EffectiveIsEnabled &&
            member.EffectiveIsVisible &&
            member.CanFocus);

        if (eligible.Count == 0)
        {
            return false;
        }

        int current = eligible.FindIndex(member => ReferenceEquals(member, value));
        int next = reverse
            ? (current <= 0 ? eligible.Count - 1 : current - 1)
            : (current < 0 || current == eligible.Count - 1 ? 0 : current + 1);
        RadioButton target = eligible[next];

        if (target.FocusOwner?.Focus(target) == false)
        {
            return false;
        }

        Select(target, ActivationCause.Keyboard);
        return true;
    }

    private static List<RadioButton> Members(RadioButton value)
    {
        List<RadioButton> result = [];

        if (value.GroupName is null)
        {
            if (value.Parent is null)
            {
                result.Add(value);
            }
            else
            {
                foreach (Control child in value.Parent.Children)
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
        if (control is RadioButton member &&
            string.Equals(member.GroupName, groupName, StringComparison.Ordinal))
        {
            result.Add(member);
        }

        if (control is Container container)
        {
            foreach (Control child in container.Children)
            {
                Collect(child, groupName, result);
            }
        }
    }
}
