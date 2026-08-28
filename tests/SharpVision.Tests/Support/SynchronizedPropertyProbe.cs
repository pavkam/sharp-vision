// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes dependent-state synchronization ordering for ControlBase regression tests.</summary>
internal sealed class SynchronizedPropertyProbe: ControlBase
{
    /// <summary>Gets or sets the semantic value whose retained projection must stay aligned.</summary>
    internal int Value
    {
        get;
        set => _ = SetPropertyAndSynchronize(
            ref field,
            value,
            InvalidationImpact.Render,
            () =>
            {
                ForwardedValue = Value;
                Synchronizing?.Invoke();
            });
    }

    /// <summary>Gets the retained value synchronized before publication.</summary>
    internal int ForwardedValue { get; private set; }

    /// <summary>Gets or sets optional work invoked from the dependent-state boundary.</summary>
    internal Action? Synchronizing { get; set; }

    /// <summary>Gets or sets a mutable reference whose instance identity defines a transition.</summary>
    internal CultureInfo ReferenceValue
    {
        get;
        set => _ = SetPropertyAndSynchronize(
            ref field,
            value,
            InvalidationImpact.Render,
            () =>
            {
                ForwardedReferenceValue = ReferenceValue;
                SynchronizingReference?.Invoke();
            },
            ReferenceEqualityComparer.Instance);
    } = CultureInfo.InvariantCulture;

    /// <summary>Gets the reference retained by the synchronized projection.</summary>
    internal CultureInfo? ForwardedReferenceValue { get; private set; }

    /// <summary>Gets or sets optional work invoked from reference synchronization.</summary>
    internal Action? SynchronizingReference { get; set; }
}
