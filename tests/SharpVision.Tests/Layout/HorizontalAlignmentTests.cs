// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies horizontal alignment values remain stable public vocabulary.</summary>
public sealed class HorizontalAlignmentTests
{
    /// <summary>Verifies the enum exposes exactly the four documented placement members.</summary>
    [Fact]
    public void Values_WhenEnumerated_ContainDocumentedMembers() =>
        Enum.GetValues<HorizontalAlignment>()
            .ShouldBe([
                HorizontalAlignment.Left,
                HorizontalAlignment.Center,
                HorizontalAlignment.Right,
                HorizontalAlignment.Stretch
            ]);
}
