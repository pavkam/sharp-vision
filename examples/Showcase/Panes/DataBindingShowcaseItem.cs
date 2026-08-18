// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Provides one stable item identity for observable collection binding.</summary>
public sealed class DataBindingShowcaseItem
{
    /// <summary>Initializes one named item.</summary>
    internal DataBindingShowcaseItem(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
