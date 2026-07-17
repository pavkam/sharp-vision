// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

/// <summary>Proves a third party can author a stateful single-content pressable control.</summary>
public sealed class ExternalToggleChip: Pressable
{
    private bool _isChecked;

    /// <summary>Initializes an unchecked external toggle with no content.</summary>
    public ExternalToggleChip()
    {
    }

    /// <summary>Gets whether the latest activation selected the chip.</summary>
    public bool IsChecked => _isChecked;

    /// <summary>Gets the number of completed semantic activations.</summary>
    public int ActivationCount { get; private set; }

    /// <summary>Gets the number of implicit pointer-capture cancellations.</summary>
    public int CaptureCancellationCount { get; private set; }

    /// <summary>Gets the latest capture-cancellation reason, or null before cancellation.</summary>
    public PointerCaptureLossReason? LastCaptureCancellation { get; private set; }

    /// <summary>Toggles through the same semantic path as keyboard and pointer activation.</summary>
    /// <exception cref="InvalidOperationException">The attached chip is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chip is disposed.</exception>
    public void PerformToggle()
        => Activate(ActivationCause.Programmatic);

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), cause, "The activation cause is unknown.");
        }

        if (SetVisualStateProperty(ref _isChecked, !_isChecked, nameof(IsChecked)))
        {
            ActivationCount++;
        }
    }

    /// <inheritdoc/>
    protected override bool IsCheckedState => IsChecked;

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        LastCaptureCancellation = reason;
        CaptureCancellationCount++;
    }
}
