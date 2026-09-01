// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

using System.Runtime.ExceptionServices;

/// <summary>Displays one persistent in-flow notification with retained caller content.</summary>
[PublicAPI]
public sealed class InfoBar: ContentControl, IStyled<InfoBarStyle>
{
    private readonly CallbackTransitionStream _dismissTransitions = new();
    private readonly StyleSlot<InfoBarStyle> _style;
    private bool _isOpen = true;
    private int _dismissDepth;

    /// <summary>Initializes an open, dismissible informational bar.</summary>
    public InfoBar() => _style = InitializeStyle(InfoBarStyle.Definition);

    /// <summary>Gets or sets the optional title rendered above retained content.</summary>
    /// <exception cref="ArgumentException">The value contains a terminal control cluster.</exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public string? Title
    {
        get;
        set
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfContainsControls(
                    value,
                    nameof(value),
                    "An InfoBar title cannot contain terminal controls.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the optional grapheme adornment rendered before the title.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public Affix? Adornment
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets whether this retained notification occupies layout and accepts input.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (value)
            {
                Open();
            }
            else
            {
                Dismiss();
            }
        }
    }

    /// <summary>Gets or sets whether the private close affordance accepts input.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public bool IsDismissible
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    } = true;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public InfoBarStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved complete InfoBar presentation.</summary>
    public InfoBarStyle ActualStyle => _style.Actual;

    /// <summary>Raised before a close commits, allowing cancellation.</summary>
    public event EventHandler<InfoBarDismissRequestedEventArgs>? DismissRequested;

    /// <summary>Raised after a successful close and required availability cleanup commit.</summary>
    public event EventHandler? Dismissed;

    /// <summary>Requests cancellable dismissal of this bar.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public void Dismiss()
    {
        VerifyMutable();

        if (!IsOpen || _dismissDepth > 0)
        {
            return;
        }

        var token = _dismissTransitions.Commit(this);
        var request = new InfoBarDismissRequestedEventArgs();
        ExceptionDispatchInfo? failure = null;
        _dismissDepth++;

        try
        {
            InvokeRequested(token, request, ref failure);
        }
        finally
        {
            _dismissDepth--;
        }

        if (request.Cancel || !token.IsCurrent)
        {
            failure?.Throw();
            return;
        }

        _isOpen = false;
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure),
            ref failure);

        if (token.IsCurrent)
        {
            InvokeDismissed(token, ref failure);
        }

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) =>
        IsOpen ? base.MeasureOverride(constraint) : default;

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        _ = _dismissTransitions.Commit(this);
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            DismissRequested = null;
            Dismissed = null;
        }
    }

    private void Open()
    {
        VerifyMutable();

        if (IsOpen)
        {
            return;
        }

        _ = _dismissTransitions.Commit(this);
        _isOpen = true;
        NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure);
    }

    private void InvokeRequested(
        CallbackTransitionToken token,
        InfoBarDismissRequestedEventArgs eventArgs,
        ref ExceptionDispatchInfo? failure)
    {
        if (DismissRequested is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            if (!token.IsCurrent)
            {
                break;
            }

            ExceptionAggregation.Capture(
                () => ((EventHandler<InfoBarDismissRequestedEventArgs>) handler)(this, eventArgs),
                ref failure);
        }
    }

    private void InvokeDismissed(
        CallbackTransitionToken token,
        ref ExceptionDispatchInfo? failure)
    {
        if (Dismissed is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            if (!token.IsCurrent)
            {
                break;
            }

            ExceptionAggregation.Capture(
                () => ((EventHandler) handler)(this, EventArgs.Empty),
                ref failure);
        }
    }
}
