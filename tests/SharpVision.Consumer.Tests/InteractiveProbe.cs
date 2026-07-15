// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

/// <summary>Provides an externally authored interactive leaf that observes lifecycle and capture cleanup.</summary>
public sealed class InteractiveProbe: Control
{
    /// <summary>Initializes a focusable one-cell interaction target.</summary>
    public InteractiveProbe()
    {
        CanFocus = true;
        Width = Length.Cells(1);
        Height = Length.Cells(1);
    }

    /// <summary>Gets the number of completed attachment hooks.</summary>
    public int AttachedCount { get; private set; }

    /// <summary>Gets the number of completed detachment hooks.</summary>
    public int DetachedCount { get; private set; }

    /// <summary>Gets the number of completed disposal hooks.</summary>
    public int DisposingCount { get; private set; }

    /// <summary>Gets the number of implicit pointer-capture cancellations.</summary>
    public int CaptureCancellationCount { get; private set; }

    /// <summary>Gets the latest implicit pointer-capture release reason, or null before cancellation.</summary>
    public ReleaseReason? LastCaptureCancellation { get; private set; }

    /// <summary>Gets whether the capture manager still reported ownership inside the latest cancellation hook.</summary>
    public bool HadCaptureDuringCancellation { get; private set; }

    /// <summary>Gets whether this control currently owns pointer capture.</summary>
    public bool HasCapture => HasPointerCapture;

    /// <summary>Gets or sets whether implicit cancellation attempts to reacquire pointer capture.</summary>
    public bool RecaptureDuringCancellation { get; set; }

    /// <summary>Gets the result of the latest capture request made by the cancellation hook.</summary>
    public bool? RecaptureDuringCancellationResult { get; private set; }

    /// <summary>Requests keyboard focus through the inherited manager boundary.</summary>
    /// <returns>True when focus was acquired or already owned; otherwise false.</returns>
    public bool TryFocus() => RequestFocus();

    /// <summary>Requests exclusive pointer capture through the inherited manager boundary.</summary>
    /// <returns>True when capture was acquired or already owned; otherwise false.</returns>
    public bool TryCapture() => CapturePointer();

    /// <summary>Explicitly releases this control's pointer capture.</summary>
    public void ReleaseCapture() => ReleasePointerCapture();

    /// <summary>Requests another complete layout transaction.</summary>
    public void RefreshLayout() => Invalidate(ChangeImpact.Measure);

    /// <summary>Requests appearance regeneration for the current semantic visual state.</summary>
    public void RefreshVisualState() => InvalidateVisualState();

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AttachedCount++;
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        base.OnDetached();
        DetachedCount++;
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        base.OnDisposing();
        DisposingCount++;
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureCancelled(ReleaseReason reason)
    {
        base.OnPointerCaptureCancelled(reason);
        LastCaptureCancellation = reason;
        HadCaptureDuringCancellation = HasPointerCapture;
        CaptureCancellationCount++;

        if (RecaptureDuringCancellation)
        {
            RecaptureDuringCancellationResult = CapturePointer();
        }

        NotifyPropertyChanged(nameof(CaptureCancellationCount), ChangeImpact.Render);
    }
}
