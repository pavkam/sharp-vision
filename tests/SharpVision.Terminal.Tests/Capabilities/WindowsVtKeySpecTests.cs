// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Input;

/// <summary>Verifies built-in Windows VT key-specification validation.</summary>
public sealed class WindowsVtKeySpecTests
{
    /// <summary>Verifies a missing or empty key sequence is rejected.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_WhenSequenceIsInvalid_Throws(string? sequence)
    {
        // Arrange
        const Code code = Code.Up;

        // Act
        var exception = Should.Throw<ArgumentException>(() => new WindowsVtKeySpec(sequence!, code));

        // Assert
        exception.ParamName.ShouldBe("sequence");
    }

    /// <summary>Verifies an undefined logical key is rejected.</summary>
    [Fact]
    public void Constructor_WhenCodeIsUndefined_Throws()
    {
        // Arrange
        var code = (Code) int.MaxValue;

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new WindowsVtKeySpec("\u001b[A", code));

        // Assert
        exception.ParamName.ShouldBe("code");
    }

    /// <summary>Verifies unknown modifier flags are rejected.</summary>
    [Fact]
    public void Constructor_WhenModifiersContainUnknownFlag_Throws()
    {
        // Arrange
        var modifiers = (Modifiers) (1 << 12);

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new WindowsVtKeySpec("\u001b[A", Code.Up, modifiers));

        // Assert
        exception.ParamName.ShouldBe("modifiers");
    }
}
