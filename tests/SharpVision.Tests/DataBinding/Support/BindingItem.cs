// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding.Support;

/// <summary>Provides one identity-bearing collection item for binding tests.</summary>
internal sealed class BindingItem
{
    /// <summary>Initializes one named item.</summary>
    internal BindingItem(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Gets the stable display name.</summary>
    internal string Name { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
