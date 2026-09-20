// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes a minimal <see cref="ItemCollection{TItem}"/> derivation over
/// <see cref="ProbeItemsControl"/>, using only the base class's default accessor-driven behavior
/// with no owner-specific overrides beyond recording the <see cref="OnInserting"/> and
/// <see cref="OnInserted"/> hook calls a test needs to prove their relative ordering against
/// validation.</summary>
internal sealed class ProbeItemCollection: ItemCollection<ProbeControl>
{
    /// <summary>Initializes a typed collection view over one non-null probe owner.</summary>
    /// <param name="owner">The owning probe item control.</param>
    internal ProbeItemCollection(ProbeItemsControl owner)
        : base(owner)
    {
    }

    /// <summary>Gets the (index, item) pairs observed by <see cref="OnInserting"/>, in call order.</summary>
    internal List<(int Index, ProbeControl Item)> InsertingCalls { get; } = [];

    /// <summary>Gets the (index, item) pairs observed by <see cref="OnInserted"/>, in call order.</summary>
    internal List<(int Index, ProbeControl Item)> InsertedCalls { get; } = [];

    /// <inheritdoc/>
    protected override void OnInserting(int index, ProbeControl item)
    {
        InsertingCalls.Add((index, item));
        base.OnInserting(index, item);
    }

    /// <inheritdoc/>
    protected override void OnInserted(int index, ProbeControl item)
    {
        InsertedCalls.Add((index, item));
        base.OnInserted(index, item);
    }
}
