// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes the protected item-presentation role for observable contract tests.</summary>
internal sealed class ProbeItemsControl: ItemsControl
{
    /// <summary>Initializes an optionally composed item owner.</summary>
    /// <param name="initialize">Whether to install a default stack host immediately.</param>
    internal ProbeItemsControl(bool initialize = true)
    {
        if (initialize)
        {
            Initialize(new Stack());
        }
    }

    /// <summary>Gets the host retained only by this test probe.</summary>
    internal Container? Host { get; private set; }

    /// <summary>Gets committed item snapshots observed by the protected callback.</summary>
    internal List<IReadOnlyList<ControlBase>> Changes { get; } = [];

    /// <summary>Gets or sets whether the next change callback fails.</summary>
    internal bool ThrowOnItemsChanged { get; set; }

    /// <summary>Gets the protected logical item-control count.</summary>
    internal int Count => ItemControlCount;

    /// <summary>Initializes the private presentation host.</summary>
    /// <param name="host">The detached host.</param>
    internal void Initialize(Container host)
    {
        InitializeItemsHost(host);
        Host = host;
    }

    /// <summary>Gets one realized control.</summary>
    /// <param name="index">The item position.</param>
    /// <returns>The realized control.</returns>
    internal ControlBase At(int index) => GetItemControl(index);

    /// <summary>Gets the identity position of one realized control.</summary>
    /// <param name="control">The candidate.</param>
    /// <returns>The item position, or -1.</returns>
    internal int IndexOf(ControlBase control) => IndexOfItemControl(control);

    /// <summary>Inserts one realized control.</summary>
    /// <param name="index">The insertion position.</param>
    /// <param name="control">The detached control.</param>
    internal void Insert(int index, ControlBase control) => InsertItemControl(index, control);

    /// <summary>Removes one realized control by identity.</summary>
    /// <param name="control">The candidate.</param>
    /// <returns>Whether it was removed.</returns>
    internal bool Remove(ControlBase control) => RemoveItemControl(control);

    /// <summary>Removes one realized control by position.</summary>
    /// <param name="index">The item position.</param>
    internal void RemoveAt(int index) => RemoveItemControlAt(index);

    /// <summary>Replaces one realized control by position.</summary>
    /// <param name="index">The item position.</param>
    /// <param name="control">The detached replacement.</param>
    internal void Replace(int index, ControlBase control) => ReplaceItemControl(index, control);

    /// <summary>Clears every realized control.</summary>
    internal void Clear() => ClearItemControls();

    /// <summary>Atomically replaces every realized control.</summary>
    /// <param name="controls">The complete borrowed candidate sequence.</param>
    internal void ReplaceAll(IEnumerable<ControlBase> controls) => ReplaceItemControls(controls);

    /// <summary>Gets controls directly owned by the semantic owner.</summary>
    /// <returns>A new identity-preserving snapshot.</returns>
    internal IReadOnlyList<ControlBase> GetOwnedOrder()
    {
        List<ControlBase> result = [];
        VisitChildren(result.Add);
        return result;
    }

    /// <inheritdoc/>
    protected override void OnItemControlsChanged()
    {
        var snapshot = new ControlBase[ItemControlCount];

        for (var index = 0; index < snapshot.Length; index++)
        {
            snapshot[index] = GetItemControl(index);
        }

        Changes.Add(snapshot);

        if (ThrowOnItemsChanged)
        {
            throw new InvalidOperationException("The item callback failed.");
        }
    }
}
