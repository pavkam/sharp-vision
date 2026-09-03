// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines a focusable two- or three-state toggle with optional content.</summary>
[PublicAPI]
public sealed class CheckBox: InputBase, IStyled<CheckBoxStyle>
{
    private bool? _isChecked = false;
    private readonly CallbackTransitionStream _stateTransitions = new();
    private readonly StyleSlot<CheckBoxStyle> _style;

    /// <summary>Initializes an unchecked two-state CheckBox that centers its desired mark and caption
    /// vertically by default.</summary>
    public CheckBox()
    {
        EnablePressActivation();
        EnableCaption();
        EnableCommand();
        _style = InitializeStyle(CheckBoxStyle.Definition);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public CheckBoxStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public CheckBoxStyle ActualStyle => _style.Actual;

    /// <summary>Initializes an unchecked two-state CheckBox with text content.</summary>
    /// <param name="text">The non-null text content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public CheckBox(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Raised after a true state commits.</summary>
    public event EventHandler<CheckChangedEventArgs>? Checked;

    /// <summary>Raised after a false state commits.</summary>
    public event EventHandler<CheckChangedEventArgs>? Unchecked;

    /// <summary>Raised after an indeterminate state commits.</summary>
    public event EventHandler<CheckChangedEventArgs>? Indeterminate;

    /// <summary>Raised after the state-specific event for every committed transition.</summary>
    public event EventHandler<CheckChangedEventArgs>? StateChanged;

    /// <summary>Gets or sets false, true, or null when three-state mode permits it.</summary>
    /// <exception cref="ArgumentException">Null is assigned while three-state mode is disabled.</exception>
    /// <exception cref="InvalidOperationException">The attached CheckBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public bool? IsChecked
    {
        get => _isChecked;
        set => SetChecked(value, ActivationCause.Programmatic);
    }

    /// <summary>Gets or sets whether activation includes an indeterminate state.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public bool ThreeState
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            if (!value && _isChecked is null)
            {
                field = false;
                _isChecked = false;
                InvalidateVisualState();
                var eventArgs = new CheckChangedEventArgs(previous: null, current: false, ActivationCause.Programmatic);
                var transition = BeginPropertyTransition(
                    _stateTransitions,
                    InvalidationImpact.None,
                    nameof(ThreeState));
                PublishTransitionProperty(
                    ref transition,
                    nameof(IsChecked),
                    InvalidationImpact.None);
                transition.PublishCurrent(Unchecked, this, eventArgs);
                transition.PublishCurrent(StateChanged, this, eventArgs);
                transition.ThrowIfFailed();
                return;
            }

            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    }

    /// <summary>Activates an available CheckBox through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public void PerformClick() => _ = TryActivate(ActivationCause.Programmatic);

    /// <summary>Activates an available CheckBox through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        var command = CaptureCommand();
        bool? next = _isChecked switch
        {
            false => true,
            true when ThreeState => null,
            _ => false
        };
        SetChecked(next, cause);
        ExecuteCommandIfAny(command);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => MeasureSelectionMarkCaption(
        constraint,
        ActualStyle.MarkWidth,
        ActualStyle.MarkGap,
        ActualStyle.AffixGap);

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => ArrangeSelectionMarkCaption(
        bounds,
        ActualStyle.MarkWidth,
        ActualStyle.MarkGap,
        ActualStyle.MarkPlacement,
        ActualStyle.AffixGap);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = ActualStyle;
        RenderSelectionMark(canvas, Mark().AsSpan(), style.MarkWidth, style.MarkPlacement, style.AffixGap);
    }

    /// <inheritdoc/>
    protected override bool IsCheckedState => _isChecked == true;

    /// <inheritdoc/>
    protected override bool IsIndeterminateState => _isChecked is null;

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Checked = null;
            Unchecked = null;
            Indeterminate = null;
            StateChanged = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }

    private void SetChecked(bool? value, ActivationCause cause)
    {
        if (value is null && !ThreeState)
        {
            throw new ArgumentException(
                "An indeterminate value requires three-state mode.",
                nameof(value));
        }

        ArgumentOutOfRangeException.ThrowIfNotDefined(cause, nameof(cause), "The activation cause is unknown.");
        VerifyMutable();

        var previous = _isChecked;

        if (previous == value)
        {
            return;
        }

        _isChecked = value;
        InvalidateVisualState();
        var transition = BeginPropertyTransition(
            _stateTransitions,
            InvalidationImpact.None,
            nameof(IsChecked));

        var eventArgs = new CheckChangedEventArgs(previous, value, cause);

        if (value == true)
        {
            transition.PublishCurrent(Checked, this, eventArgs);
        }
        else if (value == false)
        {
            transition.PublishCurrent(Unchecked, this, eventArgs);
        }
        else
        {
            transition.PublishCurrent(Indeterminate, this, eventArgs);
        }

        transition.PublishCurrent(StateChanged, this, eventArgs);
        transition.ThrowIfFailed();
    }

    [Pure]
    private string Mark() =>
        new CheckMark(ActualStyle.MarkStyle, ActualStyle.Glyphs).Format(_isChecked, CellPolicy.AmbiguousWidth);
}
