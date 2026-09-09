// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes a minimal <see cref="ItemCollection{TItem}"/> derivation over
/// <see cref="ProbeItemsControl"/>, using only the base class's default accessor-driven behavior
/// with no owner-specific overrides.</summary>
internal sealed class ProbeItemCollection: ItemCollection<ProbeControl>
{
    /// <summary>Initializes a typed collection view over one non-null probe owner.</summary>
    /// <param name="owner">The owning probe item control.</param>
    internal ProbeItemCollection(ProbeItemsControl owner)
        : base(owner)
    {
    }
}
