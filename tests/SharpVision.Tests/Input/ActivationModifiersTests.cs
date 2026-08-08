// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

using SharpVision.Terminal.Input;

/// <summary>Verifies the shared keyboard-activation modifier mask every gated activation site
/// relies on, so a future change to the allowance has one home to update instead of drifting
/// site by site.</summary>
public sealed class ActivationModifiersTests
{
    /// <summary>Verifies Shift and the lock keys, alone or combined, never block activation.</summary>
    [Theory]
    [InlineData(Modifiers.None)]
    [InlineData(Modifiers.Shift)]
    [InlineData(Modifiers.CapsLock)]
    [InlineData(Modifiers.NumLock)]
    [InlineData(Modifiers.Shift | Modifiers.CapsLock)]
    public void IsActivationEligible_WhenOnlyShiftOrLockKeysAreHeld_ReturnsTrue(Modifiers modifiers) =>
        modifiers.IsActivationEligible().ShouldBeTrue();

    /// <summary>Verifies Control, Alt, Super, Hyper, and Meta - alone or combined with an
    /// otherwise-eligible modifier - block activation.</summary>
    [Theory]
    [InlineData(Modifiers.Control)]
    [InlineData(Modifiers.Alt)]
    [InlineData(Modifiers.Super)]
    [InlineData(Modifiers.Hyper)]
    [InlineData(Modifiers.Meta)]
    [InlineData(Modifiers.Control | Modifiers.Shift)]
    public void IsActivationEligible_WhenAnyBlockingModifierIsHeld_ReturnsFalse(Modifiers modifiers) =>
        modifiers.IsActivationEligible().ShouldBeFalse();
}
