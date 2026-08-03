// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

/// <summary>Stacks children horizontally with standard showcase spacing.</summary>
internal sealed class DocRow: CompositeControlBase
{
    /// <summary>Initializes a horizontal row of showcase specimens.</summary>
    /// <param name="children">The children in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> or one of its entries is null.</exception>
    internal DocRow(params ControlBase[] children)
    {
        ArgumentNullException.ThrowIfNull(children);

        var row = new Stack { Orientation = Orientation.Horizontal, Spacing = 2 };

        foreach (var child in children)
        {
            ArgumentNullException.ThrowIfNull(child);
            row.Children.Add(child);
        }

        InitializeContent(row);
    }
}
