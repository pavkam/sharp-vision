// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;
/// <summary>Verifies built-in Windows VT program-specification validation.</summary>
public sealed class WindowsVtProgramSpecTests
{
    /// <summary>Verifies a missing or blank program identifier is rejected.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenNameIsInvalid_Throws(string? name)
    {
        // Arrange
        const string source = "\u001b[0m";

        // Act
        var exception = Should.Throw<ArgumentException>(() => new WindowsVtProgramSpec(name!, source));

        // Assert
        exception.ParamName.ShouldBe("name");
    }

    /// <summary>Verifies a missing or empty raw program source is rejected.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_WhenSourceIsInvalid_Throws(string? source)
    {
        // Arrange
        const string name = "sgr0";

        // Act
        var exception = Should.Throw<ArgumentException>(() => new WindowsVtProgramSpec(name, source!));

        // Assert
        exception.ParamName.ShouldBe("source");
    }
}
