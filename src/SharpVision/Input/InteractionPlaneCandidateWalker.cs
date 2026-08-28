// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Discovers and visits controls in the current interaction plane's focus-relative order.</summary>
internal sealed class InteractionPlaneCandidateWalker
{
    private readonly ControlBase _root;
    private readonly FocusManager _focus;
    private readonly ModalityManager _modality;

    /// <summary>Initializes traversal over one application tree and its focus and modality services.</summary>
    /// <param name="root">The non-null attached application root.</param>
    /// <param name="focus">The non-null focus manager for the same root.</param>
    /// <param name="modality">The non-null modality manager for the same root.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">The managers do not own <paramref name="root"/>.</exception>
    internal InteractionPlaneCandidateWalker(
        ControlBase root,
        FocusManager focus,
        ModalityManager modality)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(focus);
        ArgumentNullException.ThrowIfNull(modality);

        if (!ReferenceEquals(focus.Root, root) || !ReferenceEquals(modality.Root, root))
        {
            throw new ArgumentException("Interaction-plane services must own the same application root.", nameof(root));
        }

        _root = root;
        _focus = focus;
        _modality = modality;
    }

    /// <summary>Collects caller-selected controls from the current unrestricted or modal roots.</summary>
    /// <typeparam name="TCandidate">The selected control type.</typeparam>
    /// <param name="selector">A non-null selector receiving each control and its plane boundary.</param>
    /// <returns>A new deterministic depth-first candidate snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
    internal List<TCandidate> Collect<TCandidate>(
        Func<ControlBase, ControlBase, TCandidate?> selector)
        where TCandidate : ControlBase
    {
        ArgumentNullException.ThrowIfNull(selector);
        var candidates = new List<TCandidate>();

        if (_modality.Active is null)
        {
            Collect(_root, _root, selector, candidates);
        }
        else
        {
            for (var index = 0; index < _modality.ActiveRootCount; index++)
            {
                var boundary = _modality.ActiveRootAt(index);
                Collect(boundary, boundary, selector, candidates);
            }
        }

        return candidates;
    }

    /// <summary>Visits the candidate snapshot after the control containing current focus, wrapping once.</summary>
    /// <typeparam name="TCandidate">The candidate control type.</typeparam>
    /// <param name="candidates">The non-null discovery snapshot.</param>
    /// <param name="isValid">A non-null caller policy rechecking current match eligibility.</param>
    /// <param name="visitor">A non-null callback returning true when it accepts a candidate.</param>
    /// <returns>True when one current candidate is accepted.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal bool VisitAfterFocus<TCandidate>(
        IReadOnlyList<TCandidate> candidates,
        Func<TCandidate, bool> isValid,
        Func<TCandidate, bool> visitor)
        where TCandidate : ControlBase
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(isValid);
        ArgumentNullException.ThrowIfNull(visitor);

        if (candidates.Count == 0)
        {
            return false;
        }

        var anchor = FindAnchor(candidates, _focus.Focused);

        for (var offset = 1; offset <= candidates.Count; offset++)
        {
            var candidate = candidates[(anchor + offset) % candidates.Count];

            if (_modality.Allows(candidate) && isValid(candidate) && visitor(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static void Collect<TCandidate>(
        ControlBase control,
        ControlBase boundary,
        Func<ControlBase, ControlBase, TCandidate?> selector,
        List<TCandidate> candidates)
        where TCandidate : ControlBase
    {
        if (selector(control, boundary) is { } candidate)
        {
            candidates.Add(candidate);
        }

        for (var index = 0; index < control.OwnedControlCount; index++)
        {
            Collect(control.OwnedControlAt(index), boundary, selector, candidates);
        }
    }

    private static int FindAnchor<TCandidate>(IReadOnlyList<TCandidate> candidates, ControlBase? focused)
        where TCandidate : ControlBase
    {
        if (focused is null)
        {
            return candidates.Count - 1;
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            if (ModalityManager.IsWithin(focused, candidates[index]))
            {
                return index;
            }
        }

        return candidates.Count - 1;
    }
}
