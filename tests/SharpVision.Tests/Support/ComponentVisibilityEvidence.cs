// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Identifies focused unit, layout, and mounted-surface evidence for the three-state
/// <see cref="Visibility"/> contract required of one concrete control.</summary>
[Flags]
internal enum ComponentVisibilityEvidence
{
    /// <summary>No visibility evidence.</summary>
    None = 0,

    /// <summary>A Hidden child retains its measured/arranged slot, margin, and desired-size
    /// contribution to its parent.</summary>
    HiddenRetainsSlot = 1 << 0,

    /// <summary>A Hidden child renders nothing and accepts no input or hit-testing while its
    /// slot remains reserved.</summary>
    HiddenExcludesRenderInput = 1 << 1,

    /// <summary>A Collapsed child contributes no desired size, receives no arranged geometry, and
    /// its own margin does not participate.</summary>
    CollapsedExcludesSize = 1 << 2,

    /// <summary>A Collapsed child consumes neither the spacing nor the track it would otherwise
    /// occupy, without deleting an explicitly declared track.</summary>
    CollapsedRemovesSpacingOrTrack = 1 << 3,

    /// <summary>A IsVisible-Hidden-Collapsed-IsVisible runtime transition invalidates exactly the
    /// correct phases (Collapsed invalidates Measure; Hidden invalidates only rendering) and
    /// restores deterministic geometry.</summary>
    TransitionInvalidatesCorrectly = 1 << 4,

    /// <summary>Pins the host's ancestor-visibility behavior for descendants - either the
    /// inherited rendering/input exclusion, or the host's actual deviation from it when its
    /// realization model re-parents items away from their logical ancestors. The pinning test
    /// documents which of the two the host provides.</summary>
    AncestorInheritance = 1 << 5,

    /// <summary>A control that owns focus or pointer capture releases it cleanly when it becomes
    /// Hidden or Collapsed.</summary>
    FocusCaptureCleanup = 1 << 6,

    /// <summary>Zero and tiny constraints resolve with saturating, non-negative arithmetic across
    /// a Hidden/Collapsed transition.</summary>
    ZeroTinyConstraint = 1 << 7,

    /// <summary>A mounted IsVisible-Hidden-Collapsed-IsVisible transition commits the exact final
    /// geometry and rendered cells at every phase, not only the initial mounted state.</summary>
    MountedTransitionCommittedGeometry = 1 << 8,

    /// <summary>A mounted IsVisible-Hidden-Collapsed-IsVisible transition commits the exact hit
    /// targets at every phase, not only the initial mounted state.</summary>
    MountedTransitionHitTargets = 1 << 9,

    /// <summary>A Collapsed item is skipped by the host's own keyboard traversal, activation
    /// scan, or realization eligibility - input never reaches it through the host's item
    /// pipeline.</summary>
    CollapsedExcludesInput = 1 << 11,

    /// <summary>Marks a control as explicitly exempt from attributed evidence because its entire
    /// Hidden/Collapsed contract is already exhaustively proved elsewhere - either by
    /// <see cref="ControlBase"/> for a <see cref="ComponentVisibilityRole.Leaf"/> control, by the
    /// content-role bases for a <see cref="ComponentVisibilityRole.NotApplicable"/> control, or by
    /// a single-content base's own coverage for a concrete descendant whose content-collapse path
    /// adds no host-specific behavior. Mutually exclusive with every other flag.</summary>
    ExcludedLeafCoveredByControlBase = 1 << 10
}
