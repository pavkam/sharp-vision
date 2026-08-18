// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;

/// <summary>Verifies <see cref="InputOptions.MouseCoordinates"/> validation at the option boundary.</summary>
public sealed class InputOptionsTests
{
    /// <summary>Verifies an undefined enum value is rejected at construction instead of being
    /// silently accepted, matching the sibling <c>TerminalOptions.Coordinates</c> and
    /// <c>ConsoleRunOptions.MouseCoordinates</c> properties.</summary>
    [Fact]
    public void MouseCoordinates_WhenValueIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new InputOptions { MouseCoordinates = (MouseCoordinates) 9999 });

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies every defined value is accepted.</summary>
    [Theory]
    [InlineData(MouseCoordinates.Default)]
    [InlineData(MouseCoordinates.Utf8)]
    [InlineData(MouseCoordinates.Sgr)]
    public void MouseCoordinates_WhenValueIsDefined_DoesNotThrow(MouseCoordinates value)
    {
        var options = Should.NotThrow(() => new InputOptions { MouseCoordinates = value });

        options.MouseCoordinates.ShouldBe(value);
    }
}
