// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies terminal key transitions are normalized into protocol-independent routed-input facts.</summary>
public sealed class KeyEventArgsTests
{
    /// <summary>Verifies every decoded transition maps to stable initial-down, inclusive-down,
    /// repeat, and up facts for controls.</summary>
    /// <param name="action">The decoded terminal transition.</param>
    /// <param name="isInitialKeyDown">Whether the transition starts a key hold.</param>
    /// <param name="isKeyDown">Whether the transition is an initial or repeated down event.</param>
    /// <param name="isRepeat">Whether the transition repeats an active key.</param>
    /// <param name="isKeyUp">Whether the transition ends a key hold.</param>
    [Theory]
    [InlineData(KeyAction.Press, true, true, false, false)]
    [InlineData(KeyAction.Repeat, false, true, true, false)]
    [InlineData(KeyAction.Release, false, false, false, true)]
    public void Constructor_WhenStrokeHasTransition_NormalizesControlInputFacts(
        KeyAction action,
        bool isInitialKeyDown,
        bool isKeyDown,
        bool isRepeat,
        bool isKeyUp)
    {
        // Arrange and act
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Down,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            action));

        // Assert
        eventArgs.IsInitialKeyDown.ShouldBe(isInitialKeyDown);
        eventArgs.IsKeyDown.ShouldBe(isKeyDown);
        eventArgs.IsRepeat.ShouldBe(isRepeat);
        eventArgs.IsKeyUp.ShouldBe(isKeyUp);
    }
}
