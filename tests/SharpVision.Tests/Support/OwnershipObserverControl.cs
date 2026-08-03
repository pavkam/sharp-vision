// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records structural and lifecycle publication from owned-control transactions.</summary>
internal sealed class OwnershipObserverControl: ControlBase
{
    /// <summary>Gets or sets work invoked from the parent-change callback.</summary>
    internal Action<OwnershipObserverControl, ControlBase?, ControlBase?>? ParentChanging { get; set; }

    /// <summary>Gets or sets work invoked from the attachment callback.</summary>
    internal Action<OwnershipObserverControl>? Attaching { get; set; }

    /// <summary>Gets or sets work invoked from the detachment callback.</summary>
    internal Action<OwnershipObserverControl>? Detaching { get; set; }

    /// <summary>Gets or sets work invoked from the disposing callback.</summary>
    internal Action<OwnershipObserverControl>? Disposing { get; set; }

    /// <summary>Gets or sets work invoked after one unavailability reason is recorded.</summary>
    internal Action<OwnershipObserverControl, ReleaseReason>? BecomingUnavailable { get; set; }

    /// <summary>Gets the committed parent-change callback count.</summary>
    internal int ParentChangedCalls { get; private set; }

    /// <summary>Gets the committed attachment callback count.</summary>
    internal int AttachedCalls { get; private set; }

    /// <summary>Gets the committed detachment callback count.</summary>
    internal int DetachedCalls { get; private set; }

    /// <summary>Gets the disposal callback count.</summary>
    internal int DisposingCalls { get; private set; }

    /// <summary>Gets reasons published while this control became unavailable.</summary>
    internal List<ReleaseReason> UnavailableReasons { get; } = [];

    /// <summary>Gets or sets whether the next parent-detach callback throws.</summary>
    internal bool ThrowWhenParentClears { get; set; }

    /// <summary>Gets or sets whether the detachment callback throws.</summary>
    internal bool ThrowOnDetached { get; set; }

    /// <summary>Gets or sets whether the disposing callback throws.</summary>
    internal bool ThrowOnDisposing { get; set; }

    /// <summary>Gets the inherited theme identity.</summary>
    internal Theme? InheritedThemeValue => Theme;

    /// <summary>Gets the inherited cell-width policy.</summary>
    internal Policy InheritedCellPolicy => CellPolicy;

    /// <summary>Gets the inherited focus manager.</summary>
    internal FocusManager? InheritedFocusOwner => FocusOwner;

    /// <summary>Gets the inherited capture manager.</summary>
    internal PointerManager? InheritedCaptureOwner => CaptureOwner;

    /// <summary>Gets the inherited modality manager.</summary>
    internal ModalityManager? InheritedModalityOwner => ModalityOwner;

    /// <summary>Requests keyboard focus through the protected consumer seam.</summary>
    /// <returns>Whether focus was acquired or already owned.</returns>
    internal bool RequestObserverFocus() => RequestFocus();

    /// <summary>Requests pointer capture through the protected consumer seam.</summary>
    /// <returns>Whether capture was acquired or already owned.</returns>
    internal bool CaptureObserverPointer() => CapturePointer();

    /// <inheritdoc/>
    protected override void OnParentChanged(ControlBase? previous, ControlBase? current)
    {
        ParentChangedCalls++;
        ParentChanging?.Invoke(this, previous, current);

        if (current is null && ThrowWhenParentClears)
        {
            throw new InvalidOperationException("The parent callback failed.");
        }
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        AttachedCalls++;
        Attaching?.Invoke(this);
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        DetachedCalls++;
        Detaching?.Invoke(this);

        if (ThrowOnDetached)
        {
            throw new InvalidOperationException("The detachment callback failed.");
        }
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        DisposingCalls++;
        Disposing?.Invoke(this);

        if (ThrowOnDisposing)
        {
            throw new InvalidOperationException("The disposal callback failed.");
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        UnavailableReasons.Add(reason);
        BecomingUnavailable?.Invoke(this, reason);
    }
}
