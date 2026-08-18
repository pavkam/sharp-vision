// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;
/// <summary>Provides one consumer-owned item identity.</summary>
public sealed class ConsumerItem
{
    /// <summary>Initializes one named item.</summary>
    public ConsumerItem(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Gets the item name.</summary>
    public string Name { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
