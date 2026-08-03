// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Owns a collection's current-item identity independently of selection.</summary>
/// <remarks>The owner supplies the realized items. Moving current state never mutates selected
/// state, which keeps keyboard navigation and selection policy independently composable.</remarks>
internal sealed class CurrentItemNavigator
{
    private readonly Func<IReadOnlyList<ControlBase>> _items;

    public CurrentItemNavigator(Func<IReadOnlyList<ControlBase>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
    }

    public ControlBase? Current { get; private set; }

    public bool Move(int delta, bool wrap)
    {
        var items = _items();
        if (items.Count == 0 || delta == 0)
        {
            return false;
        }

        var index = Current is null ? delta > 0 ? -1 : items.Count : IndexOf(items, Current);
        if (index < 0)
        {
            index = delta > 0 ? -1 : items.Count;
        }

        var candidate = index + Math.Sign(delta);
        if (candidate < 0 || candidate >= items.Count)
        {
            if (!wrap)
            {
                return false;
            }

            candidate = candidate < 0 ? items.Count - 1 : 0;
        }

        return SetCurrent(items[candidate]);
    }

    public bool SetCurrent(ControlBase? value)
    {
        if (ReferenceEquals(Current, value))
        {
            return false;
        }

        Current?.SetCurrentState(false);
        Current = value;
        Current?.SetCurrentState(true);
        return true;
    }

    private static int IndexOf(IReadOnlyList<ControlBase> items, ControlBase value)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (ReferenceEquals(items[index], value))
            {
                return index;
            }
        }

        return -1;
    }
}
