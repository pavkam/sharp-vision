// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;

/// <summary>Resolves a pointer- or keyboard-driven selection gesture over a keyed collection into a
/// proposed selection set and anchor.</summary>
/// <remarks>
/// A multi-select collection with Shift/Control gestures follows the same three-branch policy
/// regardless of what it iterates over: Shift extends from a live anchor to the target, Control
/// toggles the target in or out, and anything else collapses the selection down to the target
/// alone. This type owns only that decision - it never touches a control's actual selection
/// storage, and it never decides whether the proposal is accepted; a caller commits the returned
/// set through its own machinery (see <see cref="SelectionCommit{TKey}"/>) and decides for itself
/// whether and when to adopt the returned anchor.
/// </remarks>
internal static class SelectionGesture<TKey>
{
    /// <summary>Resolves one gesture against <paramref name="target"/>.</summary>
    /// <param name="comparer">The equality comparer the proposed selection set is built with.</param>
    /// <param name="selection">The selection before this gesture.</param>
    /// <param name="hasAnchor">Whether a live anchor exists to extend a Shift range from. An
    /// unconstrained <c>TKey?</c> cannot represent "no anchor" for a value-typed key without
    /// erasing to plain <c>TKey</c>, so the sentinel is carried as this separate flag instead.</param>
    /// <param name="anchor">The current anchor. Ignored when <paramref name="hasAnchor"/> is false.</param>
    /// <param name="target">The key the gesture was performed against.</param>
    /// <param name="modifiers">The modifiers held during the gesture.</param>
    /// <param name="allowsMultiple">Whether the owning collection's selection mode permits more
    /// than one selected key (the only distinction the three branches make).</param>
    /// <param name="eligibleRange">Produces the eligible keys between the anchor and the target,
    /// inclusive, for a Shift range. Returning null (as opposed to an empty sequence) signals that
    /// the pair could not be resolved into a range at all - for example because one endpoint has
    /// left the domain this is evaluated over - and leaves the selection untouched rather than
    /// clearing it.</param>
    /// <returns>The proposed next selection set and the anchor the caller should adopt if it
    /// accepts the proposal.</returns>
    [Pure]
    public static (HashSet<TKey> Selection, TKey Anchor) Resolve(
        IEqualityComparer<TKey> comparer,
        IReadOnlySet<TKey> selection,
        bool hasAnchor,
        TKey anchor,
        TKey target,
        Modifiers modifiers,
        bool allowsMultiple,
        [InstantHandle] Func<TKey, TKey, IEnumerable<TKey>?> eligibleRange)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(eligibleRange);

        var next = new HashSet<TKey>(selection, comparer);
        var control = (modifiers & Modifiers.Control) != 0;
        var shift = (modifiers & Modifiers.Shift) != 0;
        var nextAnchor = anchor;

        if (allowsMultiple && shift && hasAnchor)
        {
            var range = eligibleRange(anchor, target);

            if (range is not null)
            {
                next.Clear();
                next.UnionWith(range);
            }
        }
        else if (allowsMultiple && control)
        {
            if (!next.Remove(target))
            {
                _ = next.Add(target);
            }

            nextAnchor = target;
        }
        else
        {
            next.Clear();
            _ = next.Add(target);
            nextAnchor = target;
        }

        return (next, nextAnchor);
    }
}
