// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Windows.Input;

using DisplayText = Display.Text;

/// <summary>Defines a focusable text-captioned control with reusable press interaction.</summary>
[PublicAPI]
public abstract class PressableBase: ControlBase
{
    private readonly OwnedControlSlot _textSlot;
    private readonly PressBehavior _interaction;
    private ICommand? _command;

    /// <summary>Initializes an empty focusable text-captioned control.</summary>
    protected PressableBase()
    {
        _textSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.Content,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: null,
                InvalidationImpact.Measure),
            capacity: 1);
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

    /// <summary>Gets or sets the non-null caption text.</summary>
    /// <remarks>
    /// The default implementation is backed by a lazily materialized owned <see cref="DisplayText"/>
    /// child, created on the first non-default assignment: a control that never sets text never pays
    /// for one. Notifies exactly once per committed change and is silent on same-value assignment.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public virtual string Text
    {
        get => TextControl?.Content ?? string.Empty;
        set
        {
            VerifyMutable();
            ArgumentNullException.ThrowIfNull(value);

            if (string.Equals(Text, value, StringComparison.Ordinal))
            {
                return;
            }

            if (TextControl is null)
            {
                TextControl = new DisplayText(value);
                _textSlot.ReplaceAll([TextControl]);
            }
            else
            {
                TextControl.Content = value;
            }

            NotifyPropertyChanged(nameof(Text), InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets the lazily materialized owned caption child, or null before <see cref="Text"/>
    /// is first assigned.</summary>
    protected internal DisplayText? TextControl { get; private set; }

    /// <summary>Gets whether <paramref name="candidate"/> is this control's own owned caption child.</summary>
    /// <param name="candidate">The control to test.</param>
    internal bool OwnsCaption(ControlBase candidate) => ReferenceEquals(TextControl, candidate);

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

    /// <summary>
    /// Invokes <see cref="Command"/> with <see cref="CommandParameter"/> when a command is bound
    /// and allows execution.
    /// </summary>
    /// <remarks>
    /// A concrete control's <see cref="Activate"/> override calls this after committing its own
    /// state and raising its own events, so a command that cannot execute never suppresses the
    /// control's own activation semantics (a toggle still toggles; a menu item still invokes).
    /// </remarks>
    protected void ExecuteCommandIfAny()
    {
        var command = Command;
        var parameter = CommandParameter;

        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    /// <inheritdoc/>
    protected override string? AccessKeyText => TextControl?.Content;

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

    /// <summary>Measures the owned caption child, or an empty size before one is materialized.</summary>
    /// <param name="constraint">The available layout constraint.</param>
    /// <returns>The caption child's desired size including its margin, or <see langword="default"/>.</returns>
    protected override Size MeasureOverride(Constraint constraint)
    {
        if (TextControl is not { } content)
        {
            return default;
        }

        var desired = MeasureChild(content, constraint);

        return content.Visibility == Visibility.Collapsed
            ? default
            : new Size(
                desired.Width.SaturatingAdd(content.Margin.Horizontal),
                desired.Height.SaturatingAdd(content.Margin.Vertical));
    }

    /// <summary>Arranges the owned caption child to fill the available bounds, if materialized.</summary>
    /// <param name="bounds">The bounds to arrange within.</param>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (TextControl is { } content)
        {
            ArrangeChild(content, bounds, ResolvedAxes.Both);
        }
    }

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
