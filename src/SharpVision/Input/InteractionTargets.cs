// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Captures one resolved pointer interaction transaction before routing begins.</summary>
/// <remarks>
/// Geometric hit testing, modal hover eligibility, captured delivery, and focus eligibility are
/// deliberately separate facts. The complete result also retains the modal scope captured before
/// pointer-state callbacks run, so one input record can never be replayed against a later plane.
/// </remarks>
internal readonly struct InteractionTargets
{
    /// <summary>Initializes one resolved pointer interaction.</summary>
    /// <param name="physicalLeaf">The exact geometric hit-tested leaf, or null.</param>
    /// <param name="hoverTarget">The modal-eligible physical hover leaf, or null.</param>
    /// <param name="hoverBoundary">The inclusive root of the eligible hover path, or null.</param>
    /// <param name="deliveryTarget">The captured or physical routed target, or null.</param>
    /// <param name="focusTarget">The nearest eligible focus owner, or null.</param>
    /// <param name="captureOwner">The active capture owner, or null.</param>
    /// <param name="modality">The modality manager captured for this record, or null.</param>
    /// <param name="modalScope">The active scope captured for this record, or null.</param>
    public InteractionTargets(
        ControlBase? physicalLeaf,
        ControlBase? hoverTarget,
        ControlBase? hoverBoundary,
        ControlBase? deliveryTarget,
        ControlBase? focusTarget,
        ControlBase? captureOwner,
        ModalityManager? modality,
        ModalScope? modalScope)
    {
        PhysicalLeaf = physicalLeaf;
        HoverTarget = hoverTarget;
        HoverBoundary = hoverBoundary;
        DeliveryTarget = deliveryTarget;
        FocusTarget = focusTarget;
        CaptureOwner = captureOwner;
        Modality = modality;
        ModalScope = modalScope;
    }

    /// <summary>Gets the exact geometric hit-test result before modal filtering.</summary>
    public ControlBase? PhysicalLeaf { get; }

    /// <summary>Gets the physical leaf permitted to own hover state.</summary>
    public ControlBase? HoverTarget { get; }

    /// <summary>Gets the inclusive root of the permitted hover path.</summary>
    public ControlBase? HoverBoundary { get; }

    /// <summary>Gets the control that receives routed pointer input.</summary>
    public ControlBase? DeliveryTarget { get; }

    /// <summary>Gets the nearest eligible semantic focus target.</summary>
    public ControlBase? FocusTarget { get; }

    /// <summary>Gets the capture owner used for delivery selection.</summary>
    public ControlBase? CaptureOwner { get; }

    /// <summary>Gets the modality manager captured before observable pointer mutation.</summary>
    public ModalityManager? Modality { get; }

    /// <summary>Gets the active modal scope captured before observable pointer mutation.</summary>
    public ModalScope? ModalScope { get; }

    /// <summary>Gets whether the geometric hit lies outside the captured modal plane.</summary>
    public bool IsOutsideModalPlane =>
        ModalScope is not null && (PhysicalLeaf is null || HoverTarget is null);
}
