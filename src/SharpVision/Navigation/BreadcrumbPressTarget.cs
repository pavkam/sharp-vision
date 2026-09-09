// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Identifies one press target inside a <see cref="Breadcrumb"/>'s face: either an owned
/// primary item, or the overflow trigger, resolved at the moment a primary pointer press hit it.</summary>
internal readonly record struct BreadcrumbPressTarget
{
    /// <summary>Initializes one resolved press target.</summary>
    /// <param name="item">The pressed primary item, or null when <paramref name="isOverflow"/> is
    /// true.</param>
    /// <param name="isOverflow">Whether this target is the overflow trigger rather than a primary
    /// item.</param>
    /// <param name="layoutGeneration">The owning breadcrumb's layout generation at the moment this
    /// target was resolved, used to detect a layout regenerated since - for example, by the
    /// direct-focus request this press also issues.</param>
    public BreadcrumbPressTarget(BreadcrumbItem? item, bool isOverflow, long layoutGeneration)
    {
        Debug.Assert(isOverflow || item is not null, "A non-overflow target always carries its item.");
        Item = item;
        IsOverflow = isOverflow;
        LayoutGeneration = layoutGeneration;
    }

    /// <summary>Gets the pressed primary item, or null when <see cref="IsOverflow"/> is true.</summary>
    public BreadcrumbItem? Item { get; }

    /// <summary>Gets whether this target is the overflow trigger rather than a primary item.</summary>
    public bool IsOverflow { get; }

    /// <summary>Gets the owning breadcrumb's layout generation at the moment this target was
    /// resolved.</summary>
    public long LayoutGeneration { get; }
}
