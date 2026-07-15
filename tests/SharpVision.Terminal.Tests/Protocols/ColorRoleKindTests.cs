// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>Verifies the deferred role color kind.</summary>
public sealed class ColorRoleKindTests
{
    /// <summary>Verifies that <see cref="Color.Role(int)"/> stores the id and reports <see cref="ColorKind.Role"/>.</summary>
    [Fact]
    public void Role_StoresIdAndReportsRoleKind()
    {
        var color = Color.Role(5);

        color.Kind.ShouldBe(ColorKind.Role);
        color.RoleId.ShouldBe(5);
    }

    /// <summary>Verifies that boundary ids round-trip through <see cref="Color.RoleId"/>.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void Role_WhenIdInRange_RoundTrips(int id) => Color.Role(id).RoleId.ShouldBe(id);

    /// <summary>Verifies that ids outside 0 through 255 throw <see cref="ArgumentOutOfRangeException"/>.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void Role_WhenIdOutOfRange_Throws(int id) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Color.Role(id));

    /// <summary>Verifies that <see cref="Color.RoleId"/> throws <see cref="InvalidOperationException"/> for a non-role color.</summary>
    [Fact]
    public void RoleId_WhenNotRoleKind_Throws() =>
        Should.Throw<InvalidOperationException>(() => Color.Rgb(1, 2, 3).RoleId);

    /// <summary>Verifies that role colors compare equal by id and are distinct from an indexed color with the same numeric value.</summary>
    [Fact]
    public void Role_EqualityById()
    {
        Color.Role(4).ShouldBe(Color.Role(4));
        Color.Role(4).ShouldNotBe(Color.Role(5));
        Color.Role(4).ShouldNotBe(Color.Indexed(4));
    }
}
