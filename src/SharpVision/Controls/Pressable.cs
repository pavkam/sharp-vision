// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Windows.Input;

/// <summary>Defines a focusable single-content control with reusable press interaction.</summary>
[PublicAPI]
public abstract class Pressable: ContentControl
{
    private readonly PressBehavior _interaction;
    private ICommand? _command;

    /// <summary>Initializes an empty focusable single-content control.</summary>
    protected Pressable()
    {
        _interaction = new PressBehavior(
            () => Bounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate);
        Focusable = true;
        TabStop = true;
    }

    /// <summary>Gets or sets the optional command a concrete control invokes on activation.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ICommand? Command
    {
        get => _command;
        set
        {
            VerifyMutable();

            if (EqualityComparer<ICommand?>.Default.Equals(_command, value))
            {
                return;
            }

            _command?.CanExecuteChanged -= OnCanExecuteChanged;
            _ = SetProperty(ref _command, value, InvalidationImpact.Render);
            _command?.CanExecuteChanged += OnCanExecuteChanged;
        }
    }

    /// <summary>Gets or sets the borrowed parameter passed to command queries and execution.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public object? CommandParameter
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Completes one validated activation in a concrete control.</summary>
    /// <param name="cause">The input path that completed activation.</param>
    protected abstract void Activate(ActivationCause cause);

    /// <inheritdoc/>
    protected override string? AccessKeyText => Content is IAccessKeyCaption caption ? caption.Text : null;

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        _ = key;

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            return false;
        }

        _ = FocusAccessKeyTarget();
        Activate(ActivationCause.Keyboard);
        return true;
    }

    /// <inheritdoc/>
    internal override VisualState AmbientAppearanceState => GetAppearanceState();

    /// <inheritdoc/>
    internal override bool StateAffectsAmbientAppearance => true;

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        _interaction.Handle(eventArgs);
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _interaction.FocusChanged(focused);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _interaction.CaptureLost();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _interaction.Unavailable();

        if (reason == ReleaseReason.Disposed && _command is not null)
        {
            _command.CanExecuteChanged -= OnCanExecuteChanged;
            _command = null;
        }
    }

    private void OnCanExecuteChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (IsDisposed)
        {
            return;
        }

        var dispatcher = Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Post(() =>
            {
                if (!IsDisposed)
                {
                    Invalidate(Invalidation.Render);
                }
            });
            return;
        }

        Invalidate(Invalidation.Render);
    }
}
