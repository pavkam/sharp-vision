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
    private readonly FocusManager _focus;
    private readonly ModalityManager _modality;

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
        _focus = focus;
        _modality = modality;
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
        var matches = new List<ControlBase>();

        if (_modality.Active is null)
        {
            Collect(_root, key, matches);
        }
        else
        {
            for (var index = 0; index < _modality.ActiveRootCount; index++)
            {
                Collect(_modality.ActiveRootAt(index), key, matches);
            }
        }

        if (matches.Count == 0)
        {
            return false;
        }

        var anchor = FindAnchor(matches, _focus.Focused);

        for (var offset = 1; offset <= matches.Count; offset++)
        {
            var index = (anchor + offset) % matches.Count;
            var candidate = matches[index];

            if (candidate.InvokeAccessKey(key))
            {
                return true;
            }
        }

        return false;
    }

    private static void Collect(ControlBase control, Rune key, List<ControlBase> matches)
    {
        var matched = control.MatchesAccessKey(key);

        if (matched &&
            (control is not IAccessKeyCaption ||
             (!IsPressableCaption(control) && !HasMatchingAncestor(control.Parent, key))))
        {
            matches.Add(control);
        }

        for (var index = 0; index < control.OwnedControlCount; index++)
        {
            var child = control.OwnedControlAt(index);

            Collect(child, key, matches);
        }
    }

    private static bool HasMatchingAncestor(ControlBase? control, Rune key)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (current.MatchesAccessKey(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPressableCaption(ControlBase caption) =>
        caption.Parent is PressableBase owner && owner.OwnsCaption(caption);

    private static int FindAnchor(List<ControlBase> matches, ControlBase? focused)
    {
        if (focused is null)
        {
            return matches.Count - 1;
        }

        for (var index = 0; index < matches.Count; index++)
        {
            if (ModalityManager.IsWithin(focused, matches[index]))
            {
                return index;
            }
        }

        return matches.Count - 1;
    }
}
