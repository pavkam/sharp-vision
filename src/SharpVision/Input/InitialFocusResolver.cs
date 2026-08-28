// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Resolves the first eligible focus target for surface and modal-plane activation.</summary>
internal static class InitialFocusResolver
{
    /// <summary>Finds one target in deterministic owned-control order, preferring descendants to the root.</summary>
    /// <remarks>This operation only selects a candidate. The caller performs the transactional focus request with
    /// its surface-specific reason and cancellability; FocusManager revalidates the candidate immediately before
    /// committing that request.</remarks>
    /// <param name="root">The non-null retained subtree root.</param>
    /// <param name="includeRoot">Whether the root may be returned after its descendants.</param>
    /// <param name="modality">The optional manager whose active plane and unavailable state constrain eligibility.</param>
    /// <param name="excludedSubtrees">Optional subtree roots excluded from traversal.</param>
    /// <param name="attempted">Optional identities already attempted by a reentrant focus transaction.</param>
    /// <returns>The first eligible target, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    internal static ControlBase? FindFirstEligibleFocusTarget(
        ControlBase root,
        bool includeRoot,
        ModalityManager? modality = null,
        IReadOnlyList<ControlBase>? excludedSubtrees = null,
        IReadOnlySet<ControlBase?>? attempted = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (IsExcluded(root, modality, excludedSubtrees))
        {
            return null;
        }

        var descendant = FindEligibleDescendant(root, modality, excludedSubtrees, attempted);
        return descendant ??
               (includeRoot &&
                attempted?.Contains(root) is not true &&
                IsEligibleFocusTarget(root, modality, excludedSubtrees)
                   ? root
                   : null);
    }

    /// <summary>Reports whether one control is an attached, focusable, available member of the active plane.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <param name="modality">The optional active-plane authority.</param>
    /// <param name="excludedSubtrees">Optional subtree roots excluded from focus.</param>
    /// <param name="respectActivePlane">Whether the manager's current active plane constrains the candidate.</param>
    /// <returns>True when the candidate may receive initial focus.</returns>
    internal static bool IsEligibleFocusTarget(
        ControlBase control,
        ModalityManager? modality = null,
        IReadOnlyList<ControlBase>? excludedSubtrees = null,
        bool respectActivePlane = true) =>
        !IsExcluded(control, modality, excludedSubtrees) &&
        !control.IsDisposed &&
        control.Dispatcher is not null &&
        control.CanFocus &&
        control.EffectiveIsVisible &&
        control.EffectiveIsEnabled &&
        (!respectActivePlane || modality?.Allows(control) is not false);

    private static ControlBase? FindEligibleDescendant(
        ControlBase owner,
        ModalityManager? modality,
        IReadOnlyList<ControlBase>? excludedSubtrees,
        IReadOnlySet<ControlBase?>? attempted)
    {
        for (var index = 0; index < owner.OwnedControlCount; index++)
        {
            var child = owner.OwnedControlAt(index);
            if (IsExcluded(child, modality, excludedSubtrees))
            {
                continue;
            }

            if (attempted?.Contains(child) is not true &&
                IsEligibleFocusTarget(child, modality, excludedSubtrees))
            {
                return child;
            }

            if (FindEligibleDescendant(child, modality, excludedSubtrees, attempted) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool IsExcluded(
        ControlBase control,
        ModalityManager? modality,
        IReadOnlyList<ControlBase>? excludedSubtrees)
    {
        if (modality?.IsUnavailable(control) == true)
        {
            return true;
        }

        if (excludedSubtrees is null)
        {
            return false;
        }

        foreach (var subtree in excludedSubtrees)
        {
            if (ModalityManager.IsWithin(control, subtree))
            {
                return true;
            }
        }

        return false;
    }
}
