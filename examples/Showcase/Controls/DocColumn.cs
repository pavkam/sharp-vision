// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

/// <summary>Stacks children vertically with standard showcase spacing.</summary>
internal sealed class DocColumn: CompositeControl
{
    /// <summary>Initializes a vertical column of showcase specimens.</summary>
    /// <param name="children">The children in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> or one of its entries is null.</exception>
    internal DocColumn(params Control[] children)
    {
        ArgumentNullException.ThrowIfNull(children);

        var column = new Stack { Spacing = 1 };

        foreach (var child in children)
        {
            ArgumentNullException.ThrowIfNull(child);
            column.Children.Add(child);
        }

        InitializeContent(column);
    }
}
