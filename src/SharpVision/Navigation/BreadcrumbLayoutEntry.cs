// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Records one semantic item's placement in an immutable breadcrumb layout generation.</summary>
internal readonly record struct BreadcrumbLayoutEntry
{
    /// <summary>Initializes one item placement.</summary>
    internal BreadcrumbLayoutEntry(BreadcrumbItem item, Rect bounds, bool isPrimary, bool isOverflowed)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
        Bounds = bounds;
        IsPrimary = isPrimary;
        IsOverflowed = isOverflowed;
    }

    /// <summary>Gets the semantic source item.</summary>
    internal BreadcrumbItem Item { get; }

    /// <summary>Gets the relative primary slot, or an empty rectangle when not presented.</summary>
    internal Rect Bounds { get; }

    /// <summary>Gets whether the original item participates in the primary row.</summary>
    internal bool IsPrimary { get; }

    /// <summary>Gets whether an otherwise available item is represented by the overflow menu.</summary>
    internal bool IsOverflowed { get; }
}
