// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides a non-container owner with multiple independently ordered visual slots.</summary>
internal sealed class ProbeOwnedControl: ControlBase
{
    private readonly OwnedControlSlot _secondary;

    /// <summary>Initializes two same-role slots with independent capacities.</summary>
    /// <param name="primaryCapacity">The non-negative primary-slot capacity.</param>
    internal ProbeOwnedControl(int primaryCapacity = int.MaxValue)
    {
        PrimarySlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "primary",
                InvalidationImpact.Measure),
            primaryCapacity);
        _secondary = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "secondary",
                InvalidationImpact.Render),
            capacity: 1);
    }

    /// <summary>Gets the number of controls in the primary slot.</summary>
    internal int PrimaryCount => PrimarySlot.Count;

    /// <summary>Gets the number of controls in the secondary slot.</summary>
    internal int SecondaryCount => _secondary.Count;

    /// <summary>Gets the primary-slot metadata.</summary>
    internal OwnedControlOptions PrimaryOptions => PrimarySlot.Options;

    /// <summary>Gets the primary slot for compound ownership-transaction tests.</summary>
    internal OwnedControlSlot PrimarySlot { get; }

    /// <summary>Gets the number of committed primary-slot changes.</summary>
    internal int PrimaryChanges { get; private set; }

    /// <summary>Gets immutable committed deltas published by the primary slot.</summary>
    internal List<OwnedControlChange> PrimaryChangeLog { get; } = [];

    /// <summary>Gets or sets work invoked from the primary-slot notification.</summary>
    internal Action<ProbeOwnedControl>? PrimaryChanging { get; set; }

    /// <summary>Gets or sets work invoked after this owner's parent commits.</summary>
    internal Action<ProbeOwnedControl, ControlBase?, ControlBase?>? ParentChanging { get; set; }

    /// <summary>Gets or sets work invoked after terminal disposal starts and before structural
    /// publication begins.</summary>
    internal Action<ProbeOwnedControl>? DirectDisposalRequesting { get; set; }

    /// <summary>Gets one primary control by index.</summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The control at the requested index.</returns>
    internal ControlBase PrimaryAt(int index) => PrimarySlot[index];

    /// <summary>Adds one detached control to the primary slot.</summary>
    /// <param name="control">The non-null detached control.</param>
    internal void AddPrimary(ControlBase control)
    {
        EnsurePrimaryChangeSubscription();
        PrimarySlot.Add(control);
    }

    /// <summary>Inserts one detached control into the primary slot.</summary>
    /// <param name="index">The insertion index.</param>
    /// <param name="control">The non-null detached control.</param>
    internal void InsertPrimary(int index, ControlBase control)
    {
        EnsurePrimaryChangeSubscription();
        PrimarySlot.Insert(index, control);
    }

    /// <summary>Replaces one primary child atomically.</summary>
    /// <param name="index">The replacement index.</param>
    /// <param name="control">The non-null detached replacement.</param>
    internal void ReplacePrimary(int index, ControlBase control)
    {
        EnsurePrimaryChangeSubscription();
        PrimarySlot[index] = control;
    }

    /// <summary>Atomically replaces the complete primary slot.</summary>
    /// <param name="controls">The non-null candidate snapshot.</param>
    internal void ReplaceAllPrimary(IEnumerable<ControlBase> controls)
    {
        EnsurePrimaryChangeSubscription();
        PrimarySlot.ReplaceAll(controls);
    }

    /// <summary>Moves one primary child while retaining ownership.</summary>
    /// <param name="oldIndex">The existing position.</param>
    /// <param name="newIndex">The destination position.</param>
    internal void MovePrimary(int oldIndex, int newIndex)
    {
        EnsurePrimaryChangeSubscription();
        var next = new List<ControlBase>();

        for (var index = 0; index < PrimarySlot.Count; index++)
        {
            next.Add(PrimarySlot[index]);
        }

        var control = next[oldIndex];
        next.RemoveAt(oldIndex);
        next.Insert(newIndex, control);
        PrimarySlot.ReplaceAll(next);
    }

    /// <summary>Removes one primary control.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>Whether the control was present.</returns>
    internal bool RemovePrimary(ControlBase control)
    {
        EnsurePrimaryChangeSubscription();
        return PrimarySlot.Remove(control);
    }

    /// <summary>Clears the complete primary slot atomically.</summary>
    internal void ClearPrimary()
    {
        EnsurePrimaryChangeSubscription();
        PrimarySlot.Clear();
    }

    /// <summary>Adds one detached control to the secondary slot.</summary>
    /// <param name="control">The non-null detached control.</param>
    internal void AddSecondary(ControlBase control) => _secondary.Add(control);

    /// <summary>Gets all owned controls in deterministic slot registration order.</summary>
    /// <returns>A new identity-preserving snapshot.</returns>
    internal IReadOnlyList<ControlBase> GetOwnedOrder()
    {
        List<ControlBase> result = [];
        VisitChildren(result.Add);
        return result;
    }

    private bool IsPrimaryChangeSubscribed { get; set; }

    private void EnsurePrimaryChangeSubscription()
    {
        if (IsPrimaryChangeSubscribed)
        {
            return;
        }

        PrimarySlot.Changed += OnPrimaryChanged;
        IsPrimaryChangeSubscribed = true;
    }

    private void OnPrimaryChanged(OwnedControlChange change)
    {
        PrimaryChanges++;
        PrimaryChangeLog.Add(change);
        PrimaryChanging?.Invoke(this);
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(ControlBase? previous, ControlBase? current) =>
        ParentChanging?.Invoke(this, previous, current);

    /// <inheritdoc/>
    internal override void OnDirectDisposalRequested() => DirectDisposalRequesting?.Invoke(this);
}
