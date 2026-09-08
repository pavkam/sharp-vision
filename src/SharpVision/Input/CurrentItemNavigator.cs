// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Owns a collection's current-item identity independently of selection.</summary>
/// <remarks>
/// The owner supplies the realized items on demand, so this navigator never caches a stale
/// collection across an owner mutation. Moving current state never mutates selected state, which
/// keeps keyboard navigation and selection policy independently composable: a control commits
/// selection itself, typically from a <see cref="ControlBase.HandleCurrentItemNavigation"/> commit
/// callback, after this type moves current.
/// </remarks>
[PublicAPI]
public sealed class CurrentItemNavigator
{
    private readonly Func<IReadOnlyList<ControlBase>> _items;

    /// <summary>Initializes a navigator over a collection an owner supplies on demand.</summary>
    /// <param name="items">Returns the owner's currently realized, navigable items in order,
    /// evaluated fresh on every <see cref="Move"/> call.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    public CurrentItemNavigator(Func<IReadOnlyList<ControlBase>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
    }

    /// <summary>Gets the item currently identified as current, or null when none is.</summary>
    public ControlBase? Current { get; private set; }

    /// <summary>Steps <see cref="Current"/> by one item toward <paramref name="delta"/>'s
    /// sign.</summary>
    /// <param name="delta">A positive value steps toward the end of the collection, a negative
    /// value steps toward the start, and only the sign is significant - magnitude greater than one
    /// still steps a single item. Zero is a documented no-op that always returns false.</param>
    /// <param name="wrap">Whether stepping past either end continues from the other end instead of
    /// leaving <see cref="Current"/> unchanged.</param>
    /// <returns>True when <see cref="Current"/> changed as a result of this call.</returns>
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

    /// <summary>Sets <see cref="Current"/> directly, such as to an explicit Home/End target or to
    /// clear current entirely.</summary>
    /// <param name="value">The item to become current, or null to clear it. Not required to be a
    /// member of the owner's realized items.</param>
    /// <returns>True when <see cref="Current"/> changed as a result of this call.</returns>
    public bool SetCurrent(ControlBase? value)
    {
        if (ReferenceEquals(Current, value))
        {
            return false;
        }

        if (Current is { IsDisposed: false } current)
        {
            current.SetCurrentState(false);
        }

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
