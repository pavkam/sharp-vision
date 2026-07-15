// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

using System.Reflection;

/// <summary>Verifies the consumer specimens compile without privileged product access.</summary>
public sealed class AssemblyBoundaryTests
{
    /// <summary>Verifies the product assembly does not grant this project friend access.</summary>
    [Fact]
    public void ProductAssembly_WhenInspected_DoesNotFriendConsumerTests()
    {
        var friendship = typeof(Control).Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false)
            .Cast<InternalsVisibleToAttribute>();

        friendship.ShouldNotContain(
            static attribute => string.Equals(
                new AssemblyName(attribute.AssemblyName).Name,
                "SharpVision.Consumer.Tests",
                StringComparison.Ordinal));
    }

    /// <summary>Verifies the public showcase compiles without privileged product access.</summary>
    [Fact]
    public void ProductAssembly_WhenInspected_DoesNotFriendShowcase()
    {
        var friendship = typeof(Control).Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false)
            .Cast<InternalsVisibleToAttribute>();

        friendship.ShouldNotContain(
            static attribute => string.Equals(
                new AssemblyName(attribute.AssemblyName).Name,
                "SharpVision.Showcase",
                StringComparison.Ordinal));
    }
}
