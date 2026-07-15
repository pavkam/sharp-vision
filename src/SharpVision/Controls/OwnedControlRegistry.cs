// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

/// <summary>Coordinates every visual ownership edge for one control.</summary>
internal sealed class OwnedControlRegistry
{
    private readonly List<OwnedControlSlot> _slots = [];
    private int _transactionDepth;

    /// <summary>Initializes an empty registry for one non-null owner.</summary>
    /// <param name="owner">The owning control.</param>
    internal OwnedControlRegistry(Control owner)
    {
        Debug.Assert(owner is not null, "A registry requires one concrete owner.");
        Owner = owner;
    }

    /// <summary>Gets the control whose visual edges are registered here.</summary>
    internal Control Owner { get; }

    /// <summary>Gets the total number of direct controls across every registered slot.</summary>
    internal int Count
    {
        get
        {
            var count = 0;

            foreach (var slot in _slots)
            {
                count += slot.Count;
            }

            return count;
        }
    }

    /// <summary>Gets one direct control in slot-registration and item order.</summary>
    /// <param name="index">The valid zero-based global position.</param>
    /// <returns>The control at the requested position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the owned controls.</exception>
    internal Control At(int index)
    {
        var requested = index;
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        foreach (var slot in _slots)
        {
            if (index < slot.Count)
            {
                return slot[index];
            }

            index -= slot.Count;
        }

        throw new ArgumentOutOfRangeException(
            nameof(index),
            requested,
            "The position is outside the owned controls.");
    }

    /// <summary>Marks the current ownership root and changing subtree roots structurally busy.</summary>
    /// <param name="owner">The control whose tree contains the active transaction.</param>
    /// <param name="candidates">Detached candidate subtree roots, or null.</param>
    /// <returns>The distinct registries that must be released after publication.</returns>
    internal static List<OwnedControlRegistry> EnterPublication(
        Control owner,
        IEnumerable<Control>? candidates = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var root = owner;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        var entered = new List<OwnedControlRegistry>();
        var unique = new HashSet<OwnedControlRegistry>(ReferenceEqualityComparer.Instance);
        EnterRegistry(root.OwnedControls, unique, entered);

        if (candidates is not null)
        {
            foreach (var candidate in candidates)
            {
                EnterRegistry(candidate.OwnedControls, unique, entered);
            }
        }

        return entered;
    }

    /// <summary>Releases registries previously marked by <see cref="EnterPublication"/>.</summary>
    /// <param name="entered">The non-null distinct registry snapshot.</param>
    internal static void ExitPublication(List<OwnedControlRegistry> entered)
    {
        ArgumentNullException.ThrowIfNull(entered);

        for (var index = entered.Count - 1; index >= 0; index--)
        {
            Debug.Assert(entered[index]._transactionDepth > 0, "Publication depth is balanced.");
            entered[index]._transactionDepth--;
        }
    }

    /// <summary>Rejects structural mutation while this control or an ancestor is publishing a tree transaction.</summary>
    /// <param name="control">The non-null control about to mutate structure or lifetime.</param>
    /// <exception cref="InvalidOperationException">A containing transaction is active.</exception>
    internal static void VerifyMutationAllowed(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        for (var current = control; current is not null; current = current.Parent)
        {
            if (current.OwnedControls._transactionDepth > 0)
            {
                throw new InvalidOperationException("Owned-control mutation cannot be reentered.");
            }
        }
    }

    /// <summary>Registers one distinct ordered slot.</summary>
    /// <param name="options">The validated role and traversal metadata.</param>
    /// <param name="capacity">The non-negative maximum control count.</param>
    /// <returns>The newly registered empty slot.</returns>
    /// <exception cref="ArgumentException">A stable part key is already registered.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Registration occurs during structural publication.</exception>
    internal OwnedControlSlot Register(OwnedControlOptions options, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        VerifyNotTransacting();

        if (options.PartKey is not null &&
            _slots.Exists(slot => string.Equals(
                slot.Options.PartKey,
                options.PartKey,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"The owned part key '{options.PartKey}' is already registered.",
                nameof(options));
        }

        var slot = new OwnedControlSlot(this, options, capacity);
        _slots.Add(slot);
        return slot;
    }

    /// <summary>Visits every direct owned control in slot registration and item order.</summary>
    /// <param name="visitor">The non-null synchronous visitor.</param>
    internal void Visit(Action<Control> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        foreach (var slot in _slots)
        {
            foreach (var control in slot.Items)
            {
                visitor(control);
            }
        }
    }

    /// <summary>Gets the number of direct controls eligible for sequential focus navigation.</summary>
    internal int NavigationCount
    {
        get
        {
            var count = 0;

            foreach (var slot in _slots)
            {
                if (slot.Options.ParticipatesInNavigation)
                {
                    count += slot.Count;
                }
            }

            return count;
        }
    }

    /// <summary>Gets one navigation-eligible control in slot-registration and item order.</summary>
    /// <param name="index">The valid zero-based navigation position.</param>
    /// <returns>The control at the requested position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the eligible controls.</exception>
    internal Control NavigationAt(int index)
    {
        var requested = index;
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        foreach (var slot in _slots)
        {
            if (!slot.Options.ParticipatesInNavigation)
            {
                continue;
            }

            if (index < slot.Count)
            {
                return slot[index];
            }

            index -= slot.Count;
        }

        throw new ArgumentOutOfRangeException(
            nameof(index),
            requested,
            "The navigation position is outside the eligible controls.");
    }

    /// <summary>Finds the topmost ordinary-layer target in reverse global ownership order.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <returns>The deepest eligible target, or null.</returns>
    internal Control? HitTestNormal(Point point)
    {
        for (var slotIndex = _slots.Count - 1; slotIndex >= 0; slotIndex--)
        {
            var slot = _slots[slotIndex];

            if (slot.Options.Layer != OwnedControlLayer.Normal ||
                !slot.Options.ParticipatesInHitTesting)
            {
                continue;
            }

            for (var itemIndex = slot.Count - 1; itemIndex >= 0; itemIndex--)
            {
                var child = slot[itemIndex];

                if (child.ResolveOwnedLayer(slot.Options.Layer) == OwnedControlLayer.Normal &&
                    child.HitTest(point) is { } hit)
                {
                    return hit;
                }
            }
        }

        return null;
    }

    /// <summary>Finds the topmost elevated target before every ordinary-layer target.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <returns>The deepest eligible elevated target, or null.</returns>
    internal Control? HitTestPopup(Point point)
    {
        for (var slotIndex = _slots.Count - 1; slotIndex >= 0; slotIndex--)
        {
            var slot = _slots[slotIndex];

            if (!slot.Options.ParticipatesInHitTesting)
            {
                continue;
            }

            for (var itemIndex = slot.Count - 1; itemIndex >= 0; itemIndex--)
            {
                var child = slot[itemIndex];

                if (child.HitTestPopupBranch(point, slot.Options.Layer) is { } popup)
                {
                    return popup;
                }
            }
        }

        return null;
    }

    /// <summary>Renders ordinary-layer controls in slot-registration and item order.</summary>
    /// <param name="canvas">The already clipped owner canvas.</param>
    internal void RenderNormal(TerminalCanvas canvas)
    {
        _ = canvas.Bounds;

        foreach (var slot in _slots)
        {
            if (slot.Options.Layer != OwnedControlLayer.Normal)
            {
                continue;
            }

            foreach (var child in slot.Items)
            {
                if (child.ResolveOwnedLayer(slot.Options.Layer) == OwnedControlLayer.Normal)
                {
                    child.Render(canvas);
                }
            }
        }
    }

    /// <summary>Renders elevated controls after every ordinary sibling in global ownership order.</summary>
    /// <param name="canvas">The root-relative frame canvas.</param>
    internal void RenderPopup(TerminalCanvas canvas)
    {
        _ = canvas.Bounds;

        foreach (var slot in _slots)
        {
            foreach (var child in slot.Items)
            {
                child.RenderPopupBranch(canvas, slot.Options.Layer);
            }
        }
    }

    /// <summary>Disposes every remaining direct child and continues after callback failures.</summary>
    internal void DisposeAll()
    {
        var failure = (ExceptionDispatchInfo?) null;

        for (var slotIndex = _slots.Count - 1; slotIndex >= 0; slotIndex--)
        {
            var slot = _slots[slotIndex];

            while (slot.Count > 0)
            {
                var control = slot[slot.Count - 1];
                CaptureFailure(control.DisposeOwned, ref failure);

                if (slot.Contains(control))
                {
                    var entered = EnterPublication(Owner, [control]);

                    try
                    {
                        CaptureFailure(
                            () => RemoveForDisposalWithinPublication(slot, control),
                            ref failure);
                    }
                    finally
                    {
                        ExitPublication(entered);
                    }
                }
            }
        }

        failure?.Throw();
    }

    /// <summary>Inserts one candidate after validating the complete resulting slot.</summary>
    internal void Insert(OwnedControlSlot slot, int index, Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        VerifySlot(slot);
        Owner.VerifyMutable();
        VerifyNotTransacting();
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) index, (uint) slot.Count);
        var next = new List<Control>(slot.Items);
        next.Insert(index, control);
        ValidateSnapshot(slot, next);
        Commit(slot, next, ReleaseReason.Detached, notifyUnavailable: true);
    }

    /// <summary>Replaces one candidate after validating the complete resulting slot.</summary>
    internal void Replace(OwnedControlSlot slot, int index, Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        VerifySlot(slot);
        Owner.VerifyMutable();
        VerifyNotTransacting();
        var previous = slot[index];

        if (ReferenceEquals(previous, control))
        {
            return;
        }

        var next = new List<Control>(slot.Items) { [index] = control };
        ValidateSnapshot(slot, next);
        Commit(slot, next, ReleaseReason.Detached, notifyUnavailable: true);
    }

    /// <summary>Replaces a complete slot after batch-wide validation.</summary>
    internal void ReplaceAll(OwnedControlSlot slot, IEnumerable<Control> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        VerifySlot(slot);
        Owner.VerifyMutable();
        VerifyNotTransacting();
        var next = new List<Control>();

        foreach (var control in controls)
        {
            if (control is null)
            {
                throw new ArgumentNullException(nameof(controls), "The owned-control sequence contains null.");
            }

            next.Add(control);
        }

        ValidateSnapshot(slot, next);

        if (SameOrder(slot.Items, next))
        {
            return;
        }

        Commit(slot, next, ReleaseReason.Detached, notifyUnavailable: true);
    }

    /// <summary>Removes one identical child when present.</summary>
    internal bool Remove(OwnedControlSlot slot, Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        VerifySlot(slot);
        Owner.VerifyMutable();
        VerifyNotTransacting();
        var index = slot.IndexOf(control);

        if (index < 0)
        {
            return false;
        }

        RemoveAtCore(slot, index, ReleaseReason.Detached, notifyUnavailable: true);
        return true;
    }

    /// <summary>Removes one child at a valid position.</summary>
    internal void RemoveAt(OwnedControlSlot slot, int index)
    {
        VerifySlot(slot);
        Owner.VerifyMutable();
        VerifyNotTransacting();
        _ = slot[index];
        RemoveAtCore(slot, index, ReleaseReason.Detached, notifyUnavailable: true);
    }

    /// <summary>Clears one complete slot atomically.</summary>
    internal void Clear(OwnedControlSlot slot)
    {
        VerifySlot(slot);
        Owner.VerifyMutable();
        VerifyNotTransacting();

        if (slot.Count == 0)
        {
            return;
        }

        Commit(slot, [], ReleaseReason.Detached, notifyUnavailable: true);
    }

    /// <summary>Removes one disposing child while an enclosing disposal publication is active.</summary>
    internal void RemoveForDisposalWithinPublication(OwnedControlSlot slot, Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        VerifySlot(slot);
        Owner.VerifyMutable();
        Debug.Assert(
            control.OwnedControls._transactionDepth > 0,
            "Exact disposal unlink remains guarded after the child loses its parent.");
        var index = slot.IndexOf(control);

        if (index < 0)
        {
            return;
        }

        RemoveAtCore(
            slot,
            index,
            ReleaseReason.Disposed,
            notifyUnavailable: false,
            publicationAlreadyActive: true);
    }

    private void RemoveAtCore(
        OwnedControlSlot slot,
        int index,
        ReleaseReason reason,
        bool notifyUnavailable,
        bool publicationAlreadyActive = false)
    {
        var next = new List<Control>(slot.Items);
        next.RemoveAt(index);
        Commit(slot, next, reason, notifyUnavailable, publicationAlreadyActive);
    }

    private void ValidateSnapshot(OwnedControlSlot slot, List<Control> next)
    {
        Debug.Assert(slot is not null, "Snapshot validation requires a registered slot.");
        Debug.Assert(next is not null, "Snapshot validation requires an owned candidate list.");

        if (next.Count > slot.Capacity)
        {
            throw new InvalidOperationException("The owned-control slot is at capacity.");
        }

        var unique = new HashSet<Control>(ReferenceEqualityComparer.Instance);

        foreach (var control in next)
        {
            if (!unique.Add(control))
            {
                throw new ArgumentException(
                    "The same control cannot appear more than once in an owned slot.",
                    nameof(next));
            }

            if (ReferenceEquals(control.OwningSlot, slot))
            {
                continue;
            }

            ValidateCandidate(control);
        }
    }

    private void ValidateCandidate(Control control)
    {
        Debug.Assert(control is not null, "Candidate validation requires a concrete control.");
        VerifyMutationAllowed(control);
        ObjectDisposedException.ThrowIf(control.IsDisposed, control);

        if (control.OwningSlot is not null || control.Parent is not null || control.Dispatcher is not null)
        {
            throw new ArgumentException("The control already belongs to a tree.", nameof(control));
        }

        for (var ancestor = Owner; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, control))
            {
                throw new ArgumentException("Adding the control would create a cycle.", nameof(control));
            }
        }

        control.ValidateAttachment();
    }

    private void Commit(
        OwnedControlSlot slot,
        List<Control> next,
        ReleaseReason reason,
        bool notifyUnavailable,
        bool publicationAlreadyActive = false)
    {
        var previous = new List<Control>(slot.Items);
        var removed = previous.FindAll(control => !ContainsIdentity(next, control));
        var added = next.FindAll(control => !ContainsIdentity(previous, control));
        var themeChanged = new List<Control>();
        var attached = new List<Control>();
        var detached = new List<Control>();
        var failure = (ExceptionDispatchInfo?) null;
        List<OwnedControlRegistry>? entered = null;

        if (!publicationAlreadyActive)
        {
            var changingRoots = new List<Control>(removed.Count + added.Count);
            changingRoots.AddRange(removed);
            changingRoots.AddRange(added);
            entered = EnterPublication(Owner, changingRoots);
        }

        try
        {
            if (notifyUnavailable)
            {
                foreach (var control in removed)
                {
                    CaptureFailure(() => control.NotifyUnavailable(reason), ref failure);
                }
            }

            slot.Items.Clear();
            slot.Items.AddRange(next);

            foreach (var control in removed)
            {
                control.CommitOwnership(null, null);
            }

            foreach (var control in added)
            {
                control.CommitOwnership(Owner, slot);
            }

            foreach (var control in removed)
            {
                control.CommitSubtreeContext(
                    null,
                    Policy.Default,
                    null,
                    null,
                    null,
                    themeChanged,
                    attached,
                    detached);
            }

            foreach (var control in added)
            {
                control.CommitSubtreeContext(
                    Owner.Dispatcher,
                    Owner.CellPolicy,
                    Owner.FocusOwner,
                    Owner.CaptureOwner,
                    Owner.ThemeContext,
                    themeChanged,
                    attached,
                    detached);
            }

            foreach (var control in removed)
            {
                CaptureFailure(() => control.PublishParentChanged(Owner, null), ref failure);
            }

            foreach (var control in added)
            {
                CaptureFailure(() => control.PublishParentChanged(null, Owner), ref failure);
            }

            foreach (var control in themeChanged)
            {
                CaptureFailure(control.PublishThemeContextChanged, ref failure);
            }

            foreach (var control in detached)
            {
                CaptureFailure(control.PublishDetached, ref failure);
            }

            foreach (var control in attached)
            {
                CaptureFailure(control.PublishAttached, ref failure);
            }

            CaptureFailure(slot.PublishChanged, ref failure);
        }
        finally
        {
            Owner.Invalidate(Control.InvalidationFor(slot.Options.Impact));
            if (entered is not null)
            {
                ExitPublication(entered);
            }
        }

        failure?.Throw();
    }

    private void VerifySlot(OwnedControlSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (!ReferenceEquals(slot.Registry, this))
        {
            throw new ArgumentException("The slot belongs to another registry.", nameof(slot));
        }
    }

    private void VerifyNotTransacting() => VerifyMutationAllowed(Owner);

    private static void EnterRegistry(
        OwnedControlRegistry registry,
        HashSet<OwnedControlRegistry> unique,
        List<OwnedControlRegistry> entered)
    {
        if (!unique.Add(registry))
        {
            return;
        }

        registry._transactionDepth++;
        entered.Add(registry);
    }

    private static bool SameOrder(List<Control> left, List<Control> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!ReferenceEquals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsIdentity(List<Control> controls, Control candidate)
    {
        foreach (var control in controls)
        {
            if (ReferenceEquals(control, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static void CaptureFailure(Action action, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }
}
