// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Associates one test method with the Hidden/Collapsed evidence it proves for one
/// concrete control. Multiple instances may target the same method, and one instance may target
/// a control the method does not construct directly - for example a shared-base fixture proving
/// part of a concrete descendant's requirement.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class ComponentVisibilityEvidenceAttribute: Attribute
{
    /// <summary>Contains every defined evidence flag.</summary>
    private const ComponentVisibilityEvidence _allEvidence = ComponentVisibilityEvidence.HiddenRetainsSlot |
                                                              ComponentVisibilityEvidence.HiddenExcludesRenderInput |
                                                              ComponentVisibilityEvidence.CollapsedExcludesSize |
                                                              ComponentVisibilityEvidence.CollapsedRemovesSpacingOrTrack |
                                                              ComponentVisibilityEvidence.CollapsedExcludesInput |
                                                              ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
                                                              ComponentVisibilityEvidence.AncestorInheritance |
                                                              ComponentVisibilityEvidence.FocusCaptureCleanup |
                                                              ComponentVisibilityEvidence.ZeroTinyConstraint |
                                                              ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
                                                              ComponentVisibilityEvidence.MountedTransitionHitTargets |
                                                              ComponentVisibilityEvidence.ExcludedLeafCoveredByControlBase;

    /// <summary>Initializes evidence for one concrete control and non-empty defined evidence set.</summary>
    /// <param name="controlType">The concrete exported control type.</param>
    /// <param name="evidence">The non-empty evidence proved by the annotated method.</param>
    /// <exception cref="ArgumentNullException"><paramref name="controlType"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="controlType"/> is not a concrete control.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="evidence"/> is empty or contains undefined flags.</exception>
    internal ComponentVisibilityEvidenceAttribute(Type controlType, ComponentVisibilityEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        if (controlType.IsAbstract || !typeof(ControlBase).IsAssignableFrom(controlType))
        {
            throw new ArgumentException("Visibility evidence requires a concrete Control type.", nameof(controlType));
        }

        if (evidence == ComponentVisibilityEvidence.None || (evidence & ~_allEvidence) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(evidence), evidence,
                "Visibility evidence must contain defined flags.");
        }

        ControlType = controlType;
        Evidence = evidence;
    }

    /// <summary>Gets the concrete control whose evidence is proved.</summary>
    internal Type ControlType { get; }

    /// <summary>Gets the evidence flags proved by the annotated test.</summary>
    internal ComponentVisibilityEvidence Evidence { get; }
}
