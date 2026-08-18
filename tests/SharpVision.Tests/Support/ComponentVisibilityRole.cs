// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Classifies how one concrete control structurally manages child visibility.</summary>
/// <remarks>
/// <see cref="ControlBase"/> enforces the leaf mechanics of the three-state
/// <see cref="Visibility"/> contract, but a host that manages children owns additional
/// parent-specific effects - spacing, track contribution, desired-size aggregation, scroll
/// extents, offsets, item realization, and stale-cell cleanup. This classification names which
/// family of parent-specific effects one control is responsible for, so
/// <see cref="ComponentVisibilityCoverageTests"/> can require the matching evidence.
/// </remarks>
[Flags]
internal enum ComponentVisibilityRole
{
    /// <summary>Has no structural child or content; its entire contract is proved by
    /// <see cref="ControlBase"/> and this classification is itself the explicit exclusion.</summary>
    Leaf = 1 << 0,

    /// <summary>Owns exactly one composed content root (a public Content property, a private
    /// composition root, or an equivalent single child) whose Hidden/Collapsed treatment the host
    /// composes its own chrome around.</summary>
    SingleContent = 1 << 1,

    /// <summary>Arranges children along one ordered axis with spacing between visible siblings
    /// (Stack- or Dock-shaped).</summary>
    OrderedChildren = 1 << 2,

    /// <summary>Arranges children into explicit rows and columns whose track sizing must ignore a
    /// collapsed child's request without deleting a declared track (Grid-shaped).</summary>
    TrackedChildren = 1 << 3,

    /// <summary>Aggregates children into one shared bounds via maximum desired-size, alignment,
    /// offsets, and z-order (Overlay-shaped).</summary>
    LayeredChildren = 1 << 4,

    /// <summary>Owns the shared scrolling extent, viewport, offset, and auto-scrollbar contract
    /// that <see cref="Container"/> defines.</summary>
    ScrollingExtent = 1 << 5,

    /// <summary>Manages a realized, virtualization-capable collection of item children with
    /// custom realization/derealization arithmetic beyond ordered- or tracked-children layout.</summary>
    RealizedItems = 1 << 6,

    /// <summary>Composes content that is not itself a tree of <see cref="ControlBase"/> children -
    /// for example rows painted from plain data onto one internal surface - so the Hidden/Collapsed
    /// child-visibility matrix does not apply and this classification is itself the explicit
    /// exclusion.</summary>
    NotApplicable = 1 << 7
}
