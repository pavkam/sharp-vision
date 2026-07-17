// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Captures one resolved pointer interaction transaction before routing begins.</summary>
/// <remarks>
/// Physical hit testing, captured delivery, and focus eligibility are deliberately separate facts.
/// The pointer manager commits physical path state from <see cref="PhysicalLeaf"/>, routes to
/// <see cref="DeliveryTarget"/>, and requests focus only for <see cref="FocusTarget"/>.
/// </remarks>
internal readonly struct InteractionTargets
{
    /// <summary>Initializes one resolved pointer interaction.</summary>
    /// <param name="physicalLeaf">The exact hit-tested leaf, or null.</param>
    /// <param name="deliveryTarget">The captured or physical routed target, or null.</param>
    /// <param name="focusTarget">The nearest eligible focus owner, or null.</param>
    /// <param name="captureOwner">The active capture owner, or null.</param>
    internal InteractionTargets(
        Control? physicalLeaf,
        Control? deliveryTarget,
        Control? focusTarget,
        Control? captureOwner)
    {
        PhysicalLeaf = physicalLeaf;
        DeliveryTarget = deliveryTarget;
        FocusTarget = focusTarget;
        CaptureOwner = captureOwner;
    }

    /// <summary>Gets the exact physical hit-test result.</summary>
    internal Control? PhysicalLeaf { get; }

    /// <summary>Gets the control that receives routed pointer input.</summary>
    internal Control? DeliveryTarget { get; }

    /// <summary>Gets the nearest eligible semantic focus target.</summary>
    internal Control? FocusTarget { get; }

    /// <summary>Gets the capture owner used for delivery selection.</summary>
    internal Control? CaptureOwner { get; }
}
