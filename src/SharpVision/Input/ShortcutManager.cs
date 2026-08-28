// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Menus;
using SharpVision.Terminal.Input;

/// <summary>Discovers and invokes MenuItem shortcuts in the current interaction plane.</summary>
/// <remarks>
/// Modeled on <c>AccessKeyManager</c>: a stateless tree walk, not a registration table. Menu items
/// keep no lifetime bookkeeping with this manager, so detaching, disposing, reparenting, or opening
/// and closing a submenu all resolve correctly without any explicit unregistration step. The only
/// bookkeeping this design leans on is the owning <see cref="Dispatcher"/>'s live-shortcut count -
/// a plain existence signal, not a per-item registration - which <see cref="Process"/> checks
/// before it decides whether the walk is even worth doing.
/// </remarks>
internal sealed class ShortcutManager
{
    private readonly ControlBase _root;
    private readonly InteractionPlaneCandidateWalker _walker;

    /// <summary>Initializes live-tree shortcut discovery over one application ownership root.</summary>
    /// <param name="root">The non-null attached application root.</param>
    /// <param name="focus">The non-null focus manager for the same root.</param>
    /// <param name="modality">The non-null modality manager for the same root.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">The managers do not own <paramref name="root"/>.</exception>
    public ShortcutManager(ControlBase root, FocusManager focus, ModalityManager modality)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(focus);
        ArgumentNullException.ThrowIfNull(modality);

        if (!ReferenceEquals(focus.Root, root) || !ReferenceEquals(modality.Root, root))
        {
            throw new ArgumentException("Shortcut services must own the same application root.", nameof(root));
        }

        _root = root;
        _walker = new InteractionPlaneCandidateWalker(root, focus, modality);
    }

    /// <summary>Attempts one pressed keyboard transition against every reachable MenuItem shortcut.</summary>
    /// <param name="stroke">The immutable decoded keyboard transition.</param>
    /// <returns>True when a matching item was invoked.</returns>
    /// <remarks>
    /// Checks the owning dispatcher's live-shortcut count before doing anything else, so an
    /// ordinary keypress in an application with no menu shortcuts anywhere never allocates or
    /// walks the owned-control tree - it costs one field read. The count can only ever
    /// under-report zero live shortcuts as "none", never the reverse, so this early-out cannot
    /// skip a walk that would otherwise have found a match.
    /// </remarks>
    public bool Process(Stroke stroke)
    {
        if (stroke.Action != KeyAction.Press)
        {
            return false;
        }

        _root.VerifyMutable();

        return _root.Dispatcher is { HasLiveShortcuts: true } && ProcessLive(stroke);
    }

    /// <summary>Walks the interaction plane after the zero-allocation no-shortcut fast path.</summary>
    /// <param name="stroke">The pressed keyboard transition.</param>
    /// <returns>True when one current matching item was invoked.</returns>
    private bool ProcessLive(Stroke stroke)
    {
        var matches = _walker.Collect(
            (control, _) => control is MenuItem item && item.MatchesShortcut(stroke)
                ? item
                : null);

        return _walker.VisitAfterFocus(
            matches,
            candidate => candidate.MatchesShortcut(stroke),
            candidate =>
            {
                candidate.ActivateFromMenu(ActivationCause.Keyboard);
                return true;
            });
    }
}
