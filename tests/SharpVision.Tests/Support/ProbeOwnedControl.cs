// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides a non-container owner with multiple independently ordered visual slots.</summary>
internal sealed class ProbeOwnedControl: Control
{
    private readonly OwnedControlSlot _primary;
    private readonly OwnedControlSlot _secondary;

    /// <summary>Initializes two same-role slots with independent capacities.</summary>
    /// <param name="primaryCapacity">The non-negative primary-slot capacity.</param>
    internal ProbeOwnedControl(int primaryCapacity = int.MaxValue)
    {
        _primary = RegisterOwnedSlot(
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
    internal int PrimaryCount => _primary.Count;

    /// <summary>Gets the number of controls in the secondary slot.</summary>
    internal int SecondaryCount => _secondary.Count;

    /// <summary>Gets the primary-slot metadata.</summary>
    internal OwnedControlOptions PrimaryOptions => _primary.Options;

    /// <summary>Gets the number of committed primary-slot changes.</summary>
    internal int PrimaryChanges { get; private set; }

    /// <summary>Gets or sets work invoked from the primary-slot notification.</summary>
    internal Action<ProbeOwnedControl>? PrimaryChanging { get; set; }

    /// <summary>Gets or sets work invoked after this owner's parent commits.</summary>
    internal Action<ProbeOwnedControl, Control?, Control?>? ParentChanging { get; set; }

    /// <summary>Gets one primary control by index.</summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The control at the requested index.</returns>
    internal Control PrimaryAt(int index) => _primary[index];

    /// <summary>Adds one detached control to the primary slot.</summary>
    /// <param name="control">The non-null detached control.</param>
    internal void AddPrimary(Control control)
    {
        EnsurePrimaryChangeSubscription();
        _primary.Add(control);
    }

    /// <summary>Inserts one detached control into the primary slot.</summary>
    /// <param name="index">The insertion index.</param>
    /// <param name="control">The non-null detached control.</param>
    internal void InsertPrimary(int index, Control control)
    {
        EnsurePrimaryChangeSubscription();
        _primary.Insert(index, control);
    }

    /// <summary>Replaces one primary child atomically.</summary>
    /// <param name="index">The replacement index.</param>
    /// <param name="control">The non-null detached replacement.</param>
    internal void ReplacePrimary(int index, Control control)
    {
        EnsurePrimaryChangeSubscription();
        _primary[index] = control;
    }

    /// <summary>Atomically replaces the complete primary slot.</summary>
    /// <param name="controls">The non-null candidate snapshot.</param>
    internal void ReplaceAllPrimary(IEnumerable<Control> controls)
    {
        EnsurePrimaryChangeSubscription();
        _primary.ReplaceAll(controls);
    }

    /// <summary>Removes one primary control.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>Whether the control was present.</returns>
    internal bool RemovePrimary(Control control)
    {
        EnsurePrimaryChangeSubscription();
        return _primary.Remove(control);
    }

    /// <summary>Clears the complete primary slot atomically.</summary>
    internal void ClearPrimary()
    {
        EnsurePrimaryChangeSubscription();
        _primary.Clear();
    }

    /// <summary>Adds one detached control to the secondary slot.</summary>
    /// <param name="control">The non-null detached control.</param>
    internal void AddSecondary(Control control) => _secondary.Add(control);

    /// <summary>Gets all owned controls in deterministic slot registration order.</summary>
    /// <returns>A new identity-preserving snapshot.</returns>
    internal IReadOnlyList<Control> GetOwnedOrder()
    {
        List<Control> result = [];
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

        _primary.Changed += OnPrimaryChanged;
        IsPrimaryChangeSubscribed = true;
    }

    private void OnPrimaryChanged()
    {
        PrimaryChanges++;
        PrimaryChanging?.Invoke(this);
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(Control? previous, Control? current) =>
        ParentChanging?.Invoke(this, previous, current);
}
