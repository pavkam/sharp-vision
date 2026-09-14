// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Keeps at most one non-empty semantic-text selection alive per application.</summary>
/// <remarks>
/// Every <see cref="ControlBase"/> with <see cref="ControlBase.IsTextSelectionEnabled"/> keeps its
/// own committed range, so nothing structural stops two inputs, or an input and a document, from
/// each painting a highlighted range at the same time. That is not what a user expects from one
/// application: selecting in one place deselects everywhere else, exactly as it does across the
/// widgets of a desktop window. The dispatcher owns one arbiter; an owner claims it when it commits
/// a non-empty range and releases it when the range collapses or the owner leaves the tree. A claim
/// collapses the previous owner's range through that owner's ordinary
/// <see cref="ControlBase.ClearTextSelection"/> path, so its caret, events, and rendering all
/// update the way any other collapse would. Dispatcher-affine like every control it references.
/// </remarks>
internal sealed class TextSelectionArbiter
{
    /// <summary>Initializes an arbiter with no current owner.</summary>
    public TextSelectionArbiter()
    {
    }

    /// <summary>Gets the control whose non-empty selection is currently the application's one
    /// selection, or null. Exposed so tests can prove that a collapse, detach, or disposal released
    /// the claim rather than merely hiding the highlight.</summary>
    internal ControlBase? Owner { get; private set; }

    /// <summary>Makes <paramref name="control"/> the sole selection owner, collapsing the range of
    /// whichever control held the claim before.</summary>
    /// <param name="control">The non-null control that just committed a non-empty range.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="Exception">The previous owner's collapse notification fails; the claim has
    /// already transferred by then, so the arbiter stays consistent.</exception>
    internal void Claim(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (ReferenceEquals(Owner, control))
        {
            return;
        }

        // Transfer first, so the previous owner's collapse - which re-enters this arbiter through
        // its own empty commit - observes that it no longer holds the claim and does not release
        // the new owner.
        var previous = Owner;
        Owner = control;

        if (previous is { IsDisposed: false, IsTextSelectionEnabled: true } &&
            previous.Dispatcher is not null &&
            !previous.TextSelection.IsEmpty)
        {
            previous.ClearTextSelection();
        }
    }

    /// <summary>Drops <paramref name="control"/>'s claim when it holds one; a no-op otherwise.</summary>
    /// <param name="control">The non-null control whose range collapsed or which left the tree.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    internal void Release(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (ReferenceEquals(Owner, control))
        {
            Owner = null;
        }
    }
}
