// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Provides the immutable single-row placement shared by arrange, render, hit testing,
/// keyboard navigation, and overflow projection for one generation.</summary>
internal sealed class BreadcrumbLayout
{
    /// <summary>Gets the initial empty layout.</summary>
    internal static BreadcrumbLayout Empty { get; } = new([], [], [], default, false, 0, 0);

    private BreadcrumbLayout(
        BreadcrumbLayoutEntry[] entries,
        BreadcrumbItem[] primaryItems,
        BreadcrumbItem[] overflowItems,
        Rect triggerBounds,
        bool triggerPrecedesPrimary,
        int occupiedWidth,
        long generation)
    {
        Entries = entries;
        PrimaryItems = primaryItems;
        OverflowItems = overflowItems;
        TriggerBounds = triggerBounds;
        TriggerPrecedesPrimary = triggerPrecedesPrimary;
        OccupiedWidth = occupiedWidth;
        Generation = generation;
    }

    /// <summary>Gets the placement for every source item in source order.</summary>
    internal IReadOnlyList<BreadcrumbLayoutEntry> Entries { get; }

    /// <summary>Gets primary-presented items in visual source order, including authored hidden slots.</summary>
    internal IReadOnlyList<BreadcrumbItem> PrimaryItems { get; }

    /// <summary>Gets available sources represented by menu projections in source order.</summary>
    internal IReadOnlyList<BreadcrumbItem> OverflowItems { get; }

    /// <summary>Gets the relative overflow-trigger bounds, or an empty rectangle.</summary>
    internal Rect TriggerBounds { get; }

    /// <summary>Gets whether the trigger is laid out before rather than after the primary items.</summary>
    internal bool TriggerPrecedesPrimary { get; }

    /// <summary>Gets the occupied row width.</summary>
    internal int OccupiedWidth { get; }

    /// <summary>Gets the owner-assigned layout generation.</summary>
    internal long Generation { get; }

    /// <summary>Builds a whole-item layout under an optional finite cell width.</summary>
    internal static BreadcrumbLayout Create(Breadcrumb owner, int? availableWidth, int triggerWidth, long generation)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfNegative(triggerWidth);

        var count = owner.ItemCount;
        var widths = new int[count];
        List<int> participants = [];
        var natural = 0;

        for (var index = 0; index < count; index++)
        {
            var item = owner.ItemAt(index);

            if (item.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            widths[index] = item.DesiredSize.Width.Add(item.Margin.Horizontal);
            natural = natural.Add(widths[index]);
            participants.Add(index);
        }

        natural = natural.Add(Math.Max(0, participants.Count - 1));
        var limit = availableWidth ?? natural;
        var primary = new HashSet<int>();
        var currentIndex = owner.CurrentIndex;
        var triggerPrecedes = currentIndex >= 0;
        var trigger = false;

        if (natural <= limit &&
            (currentIndex < 0 || participants.Count == 0 || participants[^1] == currentIndex))
        {
            primary.UnionWith(participants);
        }
        else if (currentIndex >= 0)
        {
            var currentPosition = participants.IndexOf(currentIndex);

            if (currentPosition >= 0)
            {
                for (var start = 0; start <= currentPosition; start++)
                {
                    var candidate = participants.GetRange(start, currentPosition - start + 1);
                    var omitted = HasAvailableOmission(owner, candidate);
                    var width = Measure(candidate, widths).Add(omitted ? triggerWidth.Add(1) : 0);

                    if (width <= limit)
                    {
                        primary.UnionWith(candidate);
                        trigger = omitted;
                        break;
                    }
                }
            }

            if (primary.Count == 0 && triggerWidth <= limit && HasAvailableOmission(owner, []))
            {
                trigger = true;
            }
        }
        else
        {
            for (var length = participants.Count; length >= 0; length--)
            {
                var candidate = participants.GetRange(0, length);
                var omitted = HasAvailableOmission(owner, candidate);

                if (!omitted)
                {
                    continue;
                }

                var width = Measure(candidate, widths).Add(triggerWidth).Add(candidate.Count > 0 ? 1 : 0);

                if (width <= limit)
                {
                    primary.UnionWith(candidate);
                    trigger = true;
                    break;
                }
            }

            if (!trigger)
            {
                for (var length = participants.Count; length >= 0; length--)
                {
                    var candidate = participants.GetRange(0, length);

                    if (Measure(candidate, widths) <= limit)
                    {
                        primary.UnionWith(candidate);
                        break;
                    }
                }
            }
        }

        var primaryIndices = participants.Where(primary.Contains).ToArray();
        var overflowIndices = Enumerable.Range(0, count)
            .Where(index => !primary.Contains(index) && owner.IsAvailableItem(owner.ItemAt(index)))
            .ToArray();
        var entries = new BreadcrumbLayoutEntry[count];
        var triggerBounds = default(Rect);
        var x = 0;

        if (trigger && triggerPrecedes)
        {
            triggerBounds = new Rect(x, 0, triggerWidth, 1);
            x = x.Add(triggerWidth).Add(primaryIndices.Length > 0 ? 1 : 0);
        }

        foreach (var index in primaryIndices)
        {
            entries[index] = new BreadcrumbLayoutEntry(
                owner.ItemAt(index),
                new Rect(x, 0, widths[index], 1),
                isPrimary: true,
                isOverflowed: false);
            x = x.Add(widths[index]).Add(index == primaryIndices[^1] ? 0 : 1);
        }

        if (trigger && !triggerPrecedes)
        {
            x = x.Add(primaryIndices.Length > 0 ? 1 : 0);
            triggerBounds = new Rect(x, 0, triggerWidth, 1);
            x = x.Add(triggerWidth);
        }

        for (var index = 0; index < count; index++)
        {
            if (primary.Contains(index))
            {
                continue;
            }

            var item = owner.ItemAt(index);
            entries[index] = new BreadcrumbLayoutEntry(
                item,
                default,
                isPrimary: false,
                isOverflowed: owner.IsAvailableItem(item));
        }

        return new BreadcrumbLayout(
            entries,
            [.. primaryIndices.Select(owner.ItemAt)],
            [.. overflowIndices.Select(owner.ItemAt)],
            triggerBounds,
            triggerPrecedes,
            x,
            generation);
    }

    /// <summary>Gets the entry for one source identity.</summary>
    internal BreadcrumbLayoutEntry EntryFor(BreadcrumbItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        foreach (var entry in Entries)
        {
            if (ReferenceEquals(entry.Item, item))
            {
                return entry;
            }
        }

        return default;
    }

    /// <summary>Gets whether another snapshot presents the same identities at the same cells.</summary>
    internal bool HasSameWindow(BreadcrumbLayout other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (TriggerBounds != other.TriggerBounds ||
            TriggerPrecedesPrimary != other.TriggerPrecedesPrimary ||
            Entries.Count != other.Entries.Count)
        {
            return false;
        }

        for (var index = 0; index < Entries.Count; index++)
        {
            if (!ReferenceEquals(Entries[index].Item, other.Entries[index].Item) ||
                Entries[index].Bounds != other.Entries[index].Bounds ||
                Entries[index].IsOverflowed != other.Entries[index].IsOverflowed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAvailableOmission(Breadcrumb owner, IReadOnlyCollection<int> primary)
    {
        for (var index = 0; index < owner.ItemCount; index++)
        {
            if (!primary.Contains(index) && owner.IsAvailableItem(owner.ItemAt(index)))
            {
                return true;
            }
        }

        return false;
    }

    private static int Measure(IEnumerable<int> indices, int[] widths)
    {
        var width = 0;
        var count = 0;

        foreach (var index in indices)
        {
            width = width.Add(widths[index]);
            count++;
        }

        return width.Add(Math.Max(0, count - 1));
    }
}
