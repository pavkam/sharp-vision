// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides a selectable component whose lifecycle overrides deliberately omit base calls.</summary>
internal sealed class TextSelectionLifecycleProbe: CompositeControlBase
{
    /// <summary>Initializes a selectable component around one retained text leaf.</summary>
    /// <param name="text">The non-null semantic text.</param>
    internal TextSelectionLifecycleProbe(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = new ControlText(text);
        InitializeContent(new Stack { Children = { Text } });
    }

    /// <summary>Gets the retained text leaf used for mounted input.</summary>
    internal ControlText Text { get; }

    /// <summary>Gets the number of focus-state callbacks.</summary>
    internal int FocusChangedCalls { get; private set; }

    /// <summary>Gets the number of pointer-capture-loss callbacks.</summary>
    internal int LostPointerCaptureCalls { get; private set; }

    /// <summary>Gets the number of unavailability callbacks.</summary>
    internal int UnavailableCalls { get; private set; }

    /// <summary>Gets whether framework selection cleanup had committed before the latest focus callback.</summary>
    internal bool FocusCleanupWasCommitted { get; private set; }

    /// <summary>Gets whether framework selection cleanup had committed before the latest capture-loss callback.</summary>
    internal bool CaptureCleanupWasCommitted { get; private set; }

    /// <summary>Gets whether framework selection cleanup had committed before the latest unavailable callback.</summary>
    internal bool UnavailableCleanupWasCommitted { get; private set; }

    /// <summary>Gets or sets whether the next focus-state callback throws.</summary>
    internal bool ThrowOnFocusChanged { get; set; }

    /// <summary>Gets or sets whether the next pointer-capture-loss callback throws.</summary>
    internal bool ThrowOnLostPointerCapture { get; set; }

    /// <summary>Gets or sets whether the next unavailability callback throws.</summary>
    internal bool ThrowOnUnavailable { get; set; }

    /// <summary>Gets or sets work invoked from the focus-state callback.</summary>
    internal Action<TextSelectionLifecycleProbe, bool>? FocusChanging { get; set; }

    /// <summary>Releases pointer capture through the protected consumer seam.</summary>
    internal void ReleaseProbePointer() => ReleasePointerCapture();

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        FocusChangedCalls++;
        FocusCleanupWasCommitted = focused ||
            (TextSelectionPhase == TextSelectionGesturePhase.Idle && !HasPointerCapture);
        FocusChanging?.Invoke(this, focused);

        if (ThrowOnFocusChanged)
        {
            throw new InvalidOperationException("The focus callback failed.");
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        _ = reason;
        LostPointerCaptureCalls++;
        CaptureCleanupWasCommitted =
            TextSelectionPhase == TextSelectionGesturePhase.Idle && !HasPointerCapture;

        if (ThrowOnLostPointerCapture)
        {
            throw new InvalidOperationException("The capture-loss callback failed.");
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        _ = reason;
        UnavailableCalls++;
        UnavailableCleanupWasCommitted =
            TextSelectionPhase == TextSelectionGesturePhase.Idle && !HasPointerCapture;

        if (ThrowOnUnavailable)
        {
            throw new InvalidOperationException("The unavailable callback failed.");
        }
    }
}
