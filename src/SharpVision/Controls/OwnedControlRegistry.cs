// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;
using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Coordinates every visual ownership edge for one control.</summary>
internal sealed class OwnedControlRegistry
{
    private readonly List<OwnedControlSlot> _slots = [];
    private int _transactionDepth;

    /// <summary>Initializes an empty registry for one non-null owner.</summary>
    /// <param name="owner">The owning control.</param>
    public OwnedControlRegistry(ControlBase owner)
    {
        Debug.Assert(owner is not null, "A registry requires one concrete owner.");
        Owner = owner;
    }

    /// <summary>Gets the control whose visual edges are registered here.</summary>
    public ControlBase Owner { get; }

    /// <summary>Gets the total number of direct controls across every registered slot.</summary>
    public int Count
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
    [Pure]
    public ControlBase At([NonNegativeValue] int index)
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
    public static List<OwnedControlRegistry> EnterPublication(
        ControlBase owner,
        IEnumerable<ControlBase>? candidates = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return EnterCompoundPublication([owner], candidates);
    }

    /// <summary>Releases registries previously marked by
    /// <see cref="EnterPublication(ControlBase, IEnumerable{ControlBase}?)"/>.</summary>
    /// <param name="entered">The non-null distinct registry snapshot.</param>
    public static void ExitPublication(List<OwnedControlRegistry> entered)
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
    public static void VerifyMutationAllowed(ControlBase control)
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
    public OwnedControlSlot Register(OwnedControlOptions options, [NonNegativeValue] int capacity)
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

    /// <summary>Finds a previously registered stable part by its non-empty key.</summary>
    /// <param name="partKey">The non-empty stable part key.</param>
    /// <returns>The registered slot, or null when the key is not registered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="partKey"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="partKey"/> is empty or whitespace.</exception>
    internal OwnedControlSlot? FindPart(string partKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partKey);
        return _slots.Find(slot => string.Equals(
            slot.Options.PartKey,
            partKey,
            StringComparison.Ordinal));
    }

    /// <summary>Visits every direct owned control in slot registration and item order.</summary>
    /// <param name="visitor">The non-null synchronous visitor.</param>
    public void Visit([InstantHandle] Action<ControlBase> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        foreach (var control in _slots.SelectMany(slot => slot.Items))
        {
            visitor(control);
        }
    }

    /// <summary>Gets the number of direct controls eligible for sequential focus navigation.</summary>
    public int NavigationCount =>
        _slots.Where(slot => slot.Options.ParticipatesInNavigation).Sum(slot => slot.Count);

    /// <summary>Gets one navigation-eligible control in slot-registration and item order.</summary>
    /// <param name="index">The valid zero-based navigation position.</param>
    /// <returns>The control at the requested position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the eligible controls.</exception>
    [Pure]
    public ControlBase NavigationAt([NonNegativeValue] int index)
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
    public ControlBase? HitTestNormal(Point point)
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
    public ControlBase? HitTestPopup(Point point)
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
    /// <param name="canvas">The nearest hard branch clip.</param>
    /// <param name="contentClip">The inherited soft content clip.</param>
    public void RenderNormal(TerminalCanvas canvas, Rect contentClip)
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
                    child.Render(canvas, contentClip);
                }
            }
        }
    }

    /// <summary>Renders elevated controls after every ordinary sibling in global ownership order.</summary>
    /// <param name="canvas">The root-relative frame canvas.</param>
    public void RenderPopup(TerminalCanvas canvas)
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
    public void DisposeAll()
    {
        ExceptionDispatchInfo? failure = null;

        for (var slotIndex = _slots.Count - 1; slotIndex >= 0; slotIndex--)
        {
            var slot = _slots[slotIndex];

            while (slot.Count > 0)
            {
                var control = slot[^1];
                ExceptionAggregation.Capture(control.DisposeOwned, ref failure);

                if (!slot.Contains(control))
                {
                    continue;
                }

                var entered = EnterPublication(Owner, [control]);

                try
                {
                    ExceptionAggregation.Capture(
                        () => RemoveForDisposalWithinPublication(slot, control),
                        ref failure);
                }
                finally
                {
                    ExitPublication(entered);
                }
            }
        }

        failure?.Throw();
    }

    /// <summary>Inserts one candidate after validating the complete resulting slot.</summary>
    public void Insert(OwnedControlSlot slot, int index, ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);

        VerifySlot(slot);
        Owner.VerifyMutable();
        VerifyNotTransacting();

        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) index, (uint) slot.Count);

        var next = new List<ControlBase>(slot.Items);
        next.Insert(index, control);

        ValidateSnapshot(slot, next);
        Commit(slot, next, ReleaseReason.Detached, notifyUnavailable: true);
    }

    /// <summary>Replaces one candidate after validating the complete resulting slot.</summary>
    public void Replace(OwnedControlSlot slot, int index, ControlBase control)
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

        var next = new List<ControlBase>(slot.Items) { [index] = control };
        ValidateSnapshot(slot, next);

        Commit(slot, next, ReleaseReason.Detached, notifyUnavailable: true);
    }

    /// <summary>Replaces a complete slot after batch-wide validation.</summary>
    public void ReplaceAll(OwnedControlSlot slot, IEnumerable<ControlBase> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        VerifySlot(slot);
        Owner.VerifyMutable();
        VerifyNotTransacting();
        var next = new List<ControlBase>();

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
    public bool Remove(OwnedControlSlot slot, ControlBase control)
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
    public void RemoveAt(OwnedControlSlot slot, int index)
    {
        VerifySlot(slot);
        Owner.VerifyMutable();
        VerifyNotTransacting();

        _ = slot[index];

        RemoveAtCore(slot, index, ReleaseReason.Detached, notifyUnavailable: true);
    }

    /// <summary>Clears one complete slot atomically.</summary>
    public void Clear(OwnedControlSlot slot)
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
    public void RemoveForDisposalWithinPublication(OwnedControlSlot slot, ControlBase control)
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

    private static void RemoveAtCore(
        OwnedControlSlot slot,
        int index,
        ReleaseReason reason,
        bool notifyUnavailable,
        bool publicationAlreadyActive = false)
    {
        var next = new List<ControlBase>(slot.Items);

        next.RemoveAt(index);

        Commit(slot, next, reason, notifyUnavailable, publicationAlreadyActive);
    }

    private void ValidateSnapshot(OwnedControlSlot slot, List<ControlBase> next)
    {
        Debug.Assert(slot is not null, "Snapshot validation requires a registered slot.");
        Debug.Assert(next is not null, "Snapshot validation requires an owned candidate list.");

        if (next.Count > slot.Capacity)
        {
            throw new InvalidOperationException("The owned-control slot is at capacity.");
        }

        var unique = new HashSet<ControlBase>(ReferenceEqualityComparer.Instance);

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

    private void ValidateCandidate(ControlBase control)
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

    private static void Commit(
        OwnedControlSlot slot,
        List<ControlBase> next,
        ReleaseReason reason,
        bool notifyUnavailable,
        bool publicationAlreadyActive = false)
    {
        CommitCompound(
            structuralContinuation: null,
            [(slot, next, reason, notifyUnavailable)],
            publicationAlreadyActive);
    }

    /// <summary>Commits complete snapshots for several owned slots as one guarded structural boundary.</summary>
    /// <param name="structuralContinuation">Non-throwing framework state synchronization that must
    /// become visible before lifecycle publication.</param>
    /// <param name="snapshots">The distinct slots and their complete proposed ordered contents.</param>
    /// <remarks>Every snapshot is copied and validated before mutation. All ownership and inherited
    /// context commits precede the continuation and every lifecycle or slot callback. Callback
    /// failures are aggregated without rolling back the coherent compound graph.</remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="structuralContinuation"/>, <paramref name="snapshots"/>, a slot, a sequence,
    /// or a sequence element is null.
    /// </exception>
    /// <exception cref="ArgumentException">A slot repeats or a candidate cannot belong to its owner.</exception>
    /// <exception cref="InvalidOperationException">An owner is off-dispatcher or an ownership transaction is active.</exception>
    /// <exception cref="ObjectDisposedException">An owner or candidate is disposed.</exception>
    internal static void CommitCompound(
        Action structuralContinuation,
        params (OwnedControlSlot Slot, IEnumerable<ControlBase> Controls)[] snapshots)
        => CommitCompound(
            structuralContinuation,
            ReleaseReason.Detached,
            notifyUnavailable: true,
            publicationAlreadyActive: false,
            snapshots);

    /// <summary>Commits compound disposal snapshots while the containing owner already holds the
    /// structural publication guard.</summary>
    /// <param name="structuralContinuation">Non-throwing framework state synchronization.</param>
    /// <param name="snapshots">The distinct slots and their complete proposed ordered contents.</param>
    internal static void CommitCompoundForOwnerDisposal(
        Action structuralContinuation,
        params (OwnedControlSlot Slot, IEnumerable<ControlBase> Controls)[] snapshots)
        => CommitCompound(
            structuralContinuation,
            ReleaseReason.Disposed,
            notifyUnavailable: true,
            publicationAlreadyActive: true,
            snapshots);

    private static void CommitCompound(
        Action structuralContinuation,
        ReleaseReason reason,
        bool notifyUnavailable,
        bool publicationAlreadyActive,
        (OwnedControlSlot Slot, IEnumerable<ControlBase> Controls)[] snapshots)
    {
        ArgumentNullException.ThrowIfNull(structuralContinuation);
        ArgumentNullException.ThrowIfNull(snapshots);
        var prepared = new (OwnedControlSlot Slot, List<ControlBase> Next, ReleaseReason Reason, bool NotifyUnavailable)[snapshots.Length];

        for (var index = 0; index < snapshots.Length; index++)
        {
            var (slot, controls) = snapshots[index];
            ArgumentNullException.ThrowIfNull(slot);
            ArgumentNullException.ThrowIfNull(controls);
            var next = new List<ControlBase>();

            foreach (var control in controls)
            {
                if (control is null)
                {
                    throw new ArgumentNullException(nameof(snapshots), "An owned-control snapshot contains null.");
                }

                next.Add(control);
            }

            prepared[index] = (slot, next, reason, notifyUnavailable);
        }

        CommitCompound(structuralContinuation, prepared, publicationAlreadyActive);
    }

    private static void CommitCompound(
        Action? structuralContinuation,
        (OwnedControlSlot Slot, List<ControlBase> Next, ReleaseReason Reason, bool NotifyUnavailable)[] snapshots,
        bool publicationAlreadyActive)
    {
        var distinctSlots = new HashSet<OwnedControlSlot>(ReferenceEqualityComparer.Instance);
        var distinctControls = new HashSet<ControlBase>(ReferenceEqualityComparer.Instance);
        var changes = new List<OwnedControlMutation>();

        foreach (var (slot, next, reason, notifyUnavailable) in snapshots)
        {
            var registry = slot.Registry;
            registry.VerifySlot(slot);
            registry.Owner.VerifyMutable();

            if (!publicationAlreadyActive)
            {
                registry.VerifyNotTransacting();
            }

            if (!distinctSlots.Add(slot))
            {
                throw new ArgumentException("A compound ownership transaction cannot repeat a slot.", nameof(snapshots));
            }

            registry.ValidateSnapshot(slot, next);

            foreach (var control in next)
            {
                if (!distinctControls.Add(control))
                {
                    throw new ArgumentException(
                        "A compound ownership transaction cannot place one control in several slots.",
                        nameof(snapshots));
                }
            }

            if (SameOrder(slot.Items, next))
            {
                continue;
            }

            var previous = new List<ControlBase>(slot.Items);
            var removed = previous.FindAll(control => !ContainsIdentity(next, control));
            var added = next.FindAll(control => !ContainsIdentity(previous, control));
            changes.Add(new OwnedControlMutation(slot, next, removed, added, reason, notifyUnavailable));
        }

        if (changes.Count == 0)
        {
            return;
        }

        ExceptionDispatchInfo? failure = null;
        var committed = false;
        var invalidated = false;
        List<OwnedControlRegistry>? entered = null;

        if (!publicationAlreadyActive)
        {
            var changingRoots = changes.SelectMany(change => change.Removed.Concat(change.Added));
            entered = EnterCompoundPublication(changes.Select(change => change.Slot.Registry.Owner), changingRoots);
        }

        try
        {
            foreach (var change in changes)
            {
                if (!change.NotifyUnavailable)
                {
                    continue;
                }

                foreach (var control in change.Removed)
                {
                    ExceptionAggregation.Capture(() => control.NotifyUnavailable(change.Reason), ref failure);
                }
            }

            var previousAppearance = new Dictionary<ControlBase, AppearanceSnapshot>();
            foreach (var control in changes.SelectMany(change => change.Removed))
            {
                AppearanceSnapshot.CaptureSubtree(control, previousAppearance);
            }

            foreach (var control in changes.SelectMany(change => change.Added))
            {
                AppearanceSnapshot.CaptureSubtree(control, previousAppearance);
            }

            var plans = new List<ContextTransitionPlan>();
            var themeTransitions = new List<ThemeTransition>();
            var currentAppearance = new Dictionary<ControlBase, AppearanceSnapshot>(previousAppearance.Count);
            var attached = new List<ControlBase>();
            var detached = new List<ControlBase>();

            foreach (var control in changes.SelectMany(change => change.Removed))
            {
                AddPlan(ContextTransitionPlan.Create(
                    control,
                    null,
                    UnicodePolicy.Default,
                    null,
                    null,
                    null,
                    null,
                    previousAppearance,
                    currentParentAmbientFace: null,
                    propagateContext: true));
            }

            foreach (var change in changes)
            {
                var owner = change.Slot.Registry.Owner;
                var ownerAmbientFace = AppearanceSnapshot.ResolveParentAmbient(owner);

                foreach (var control in change.Added)
                {
                    AddPlan(ContextTransitionPlan.Create(
                        control,
                        owner.Dispatcher,
                        owner.CellPolicy,
                        owner.FocusOwner,
                        owner.CaptureOwner,
                        owner.ModalityOwner,
                        owner.InheritedTheme,
                        previousAppearance,
                        ownerAmbientFace,
                        propagateContext: true));
                }
            }

            committed = true;
            foreach (var change in changes)
            {
                change.Slot.Items.Clear();
                change.Slot.Items.AddRange(change.Next);
            }

            foreach (var change in changes)
            {
                foreach (var control in change.Removed)
                {
                    control.CommitOwnership(null, null);
                }
            }

            foreach (var change in changes)
            {
                var owner = change.Slot.Registry.Owner;

                foreach (var control in change.Added)
                {
                    control.CommitOwnership(owner, change.Slot);
                }
            }

            foreach (var plan in plans)
            {
                plan.Commit();
            }

            foreach (var change in changes)
            {
                var owner = change.Slot.Registry.Owner;

                foreach (var control in change.Added)
                {
                    control.SetCapabilities(owner.CapabilityContext);
                    control.SetCellMetrics(owner.CellMetricsContext);
                }
            }

            if (structuralContinuation is not null)
            {
                ExceptionAggregation.Capture(structuralContinuation, ref failure);
            }

            var appearanceChanges = AppearanceChange.CreateChanges(
                themeTransitions,
                previousAppearance,
                currentAppearance);

            foreach (var change in changes)
            {
                var owner = change.Slot.Registry.Owner;

                foreach (var control in change.Removed)
                {
                    ExceptionAggregation.Capture(() => control.PublishParentChanged(owner, null), ref failure);
                }
            }

            foreach (var change in changes)
            {
                var owner = change.Slot.Registry.Owner;

                foreach (var control in change.Added)
                {
                    ExceptionAggregation.Capture(() => control.PublishParentChanged(null, owner), ref failure);
                }
            }

            foreach (var change in appearanceChanges)
            {
                ExceptionAggregation.Capture(
                    () => change.Control.PublishAppearanceChanged(change),
                    ref failure);
            }

            foreach (var control in detached)
            {
                ExceptionAggregation.Capture(control.PublishDetached, ref failure);
            }

            foreach (var control in attached)
            {
                ExceptionAggregation.Capture(control.PublishAttached, ref failure);
            }

            InvalidateOnce();

            foreach (var change in changes)
            {
                ExceptionAggregation.Capture(change.Slot.PublishChanged, ref failure);
            }

            void AddPlan(ContextTransitionPlan plan)
            {
                plans.Add(plan);
                themeTransitions.AddRange(plan.ThemeTransitions);
                attached.AddRange(plan.Attached);
                detached.AddRange(plan.Detached);

                foreach (var entry in plan.CurrentAppearance)
                {
                    currentAppearance.Add(entry.Key, entry.Value);
                }
            }
        }
        finally
        {
            if (committed)
            {
                InvalidateOnce();
            }

            if (entered is not null)
            {
                ExitPublication(entered);
            }
        }

        failure?.Throw();
        return;

        void InvalidateOnce()
        {
            if (invalidated)
            {
                return;
            }

            invalidated = true;

            // A newly spliced root can still carry its construction-default Pending == All
            // (or any other unretired invalidation), which the owner's own Invalidate would
            // otherwise never learn about under an Invalidation.None slot impact - breaking
            // the propagation invariant that a parent becomes dirty whenever any owned
            // descendant needs work. Union the slot's own impact with whatever the
            // added roots still owe, floor to Render when anything was removed so stale cells
            // are repainted, and normalize to the earliest single named phase, since Expand
            // only closes over dependents for the named singletons.
            var byOwner = new Dictionary<ControlBase, Invalidation>(ReferenceEqualityComparer.Instance);

            foreach (var change in changes)
            {
                var owner = change.Slot.Registry.Owner;
                var invalidation = ControlBase.InvalidationFor(change.Slot.Options.Impact);

                foreach (var control in change.Added)
                {
                    invalidation |= control.Pending;
                }

                if (change.Removed.Count > 0)
                {
                    invalidation |= Invalidation.Render;
                }

                byOwner[owner] = byOwner.GetValueOrDefault(owner) | invalidation;
            }

            foreach (var (owner, invalidation) in byOwner)
            {
                var normalized =
                    (invalidation & Invalidation.Measure) != Invalidation.None ? Invalidation.Measure
                    : (invalidation & Invalidation.Arrange) != Invalidation.None ? Invalidation.Arrange
                    : (invalidation & Invalidation.Render) != Invalidation.None ? Invalidation.Render
                    : Invalidation.None;

                owner.Invalidate(normalized);
            }
        }
    }

    private static List<OwnedControlRegistry> EnterCompoundPublication(
        IEnumerable<ControlBase> owners,
        IEnumerable<ControlBase>? candidates)
    {
        var entered = new List<OwnedControlRegistry>();
        var unique = new HashSet<OwnedControlRegistry>(ReferenceEqualityComparer.Instance);

        foreach (var owner in owners)
        {
            var root = owner;

            while (root.Parent is { } parent)
            {
                root = parent;
            }

            EnterRegistry(root.OwnedControls, unique, entered);
        }

        if (candidates is not null)
        {
            foreach (var candidate in candidates)
            {
                EnterRegistry(candidate.OwnedControls, unique, entered);
            }
        }

        return entered;
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

    [Pure]
    private static bool SameOrder(List<ControlBase> left, List<ControlBase> right) =>
        left.Count == right.Count && !left.Where((t, index) => !ReferenceEquals(t, right[index])).Any();

    [Pure]
    private static bool ContainsIdentity(List<ControlBase> controls, ControlBase candidate) =>
        controls.Any(control => ReferenceEquals(control, candidate));
}
