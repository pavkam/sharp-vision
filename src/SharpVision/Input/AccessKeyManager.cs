// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Discovers and invokes ampersand-declared access keys in the current interaction plane.</summary>
internal sealed class AccessKeyManager
{
    private const Modifiers _allowedModifiers =
        Modifiers.Alt | Modifiers.Shift | Modifiers.CapsLock | Modifiers.NumLock;

    private readonly ControlBase _root;
    private readonly InteractionPlaneCandidateWalker _walker;

    /// <summary>Initializes live-tree access-key discovery over one application ownership root.</summary>
    /// <param name="root">The non-null attached application root.</param>
    /// <param name="focus">The non-null focus manager for the same root.</param>
    /// <param name="modality">The non-null modality manager for the same root.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">The managers do not own <paramref name="root"/>.</exception>
    public AccessKeyManager(ControlBase root, FocusManager focus, ModalityManager modality)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(focus);
        ArgumentNullException.ThrowIfNull(modality);

        if (!ReferenceEquals(focus.Root, root) || !ReferenceEquals(modality.Root, root))
        {
            throw new ArgumentException("Access-key services must own the same application root.", nameof(root));
        }

        _root = root;
        _walker = new InteractionPlaneCandidateWalker(root, focus, modality);
    }

    /// <summary>Attempts one pressed Alt character against the current eligible caption snapshot.</summary>
    /// <param name="stroke">The immutable decoded keyboard transition.</param>
    /// <returns>True when one matching control accepts its semantic action.</returns>
    public bool Process(Stroke stroke)
    {
        if (stroke is not
            {
                Code: Code.Character,
                Character: { } key,
                Action: KeyAction.Press,
                Modifiers: var modifiers
            } ||
            (modifiers & Modifiers.Alt) == 0 ||
            (modifiers & ~_allowedModifiers) != 0)
        {
            return false;
        }

        _root.VerifyMutable();
        var matches = _walker.Collect((control, boundary) =>
        {
            var matched = control.MatchesAccessKey(key);

            return matched &&
                   (control is not IAccessKeyCaption ||
                    (!IsOwnedCaption(control) && !HasMatchingAncestor(control.Parent, boundary, key)))
                ? control
                : null;
        });

        return _walker.VisitAfterFocus(
            matches,
            candidate => candidate.MatchesAccessKey(key),
            candidate => candidate.InvokeAccessKey(key));
    }

    private static bool HasMatchingAncestor(ControlBase? control, ControlBase boundary, Rune key)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (current.MatchesAccessKey(key))
            {
                return true;
            }

            if (ReferenceEquals(current, boundary))
            {
                break;
            }
        }

        return false;
    }

    private static bool IsOwnedCaption(ControlBase caption) =>
        caption.Parent is IAccessKeyCaptionOwner owner && owner.OwnsAccessKeyCaption(caption);
}
