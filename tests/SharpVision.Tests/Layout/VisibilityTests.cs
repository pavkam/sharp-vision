// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies visibility values remain stable public vocabulary.</summary>
public sealed class VisibilityTests
{
    /// <summary>Verifies visibility values remain stable public vocabulary.</summary>
    [Fact]
    public void Values_WhenEnumerated_ContainDocumentedMembers() =>
        Enum.GetValues<Visibility>()
            .ShouldBe([Visibility.Visible, Visibility.Hidden, Visibility.Collapsed]);
}
