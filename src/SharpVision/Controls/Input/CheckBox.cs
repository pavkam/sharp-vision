// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

/// <summary>Defines a focusable two- or three-state toggle with optional content.</summary>
[PublicAPI]
public sealed class CheckBox: InputBase, IStyled<CheckBoxStyle>
{
    private bool? _isChecked = false;
    private int _stateVersion;
    private readonly StyleSlot<CheckBoxStyle> _style;

    /// <summary>Initializes an unchecked two-state CheckBox.</summary>
    public CheckBox()
    {
        EnablePressActivation();
        EnableCaption();
        EnableCommand();
        _style = InitializeStyle(CheckBoxStyle.Definition);
        HorizontalAlignment = HorizontalAlignment.Left;
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
                var version = ++_stateVersion;
                InvalidateVisualState();
                var eventArgs = new CheckChangedEventArgs(previous: null, current: false, ActivationCause.Programmatic);
                ExceptionDispatchInfo? failure = null;
                ExceptionAggregation.Capture(
                    () => NotifyPropertyChanged(nameof(ThreeState), InvalidationImpact.None),
                    ref failure);
                ExceptionAggregation.Capture(
                    () => NotifyPropertyChanged(nameof(IsChecked), InvalidationImpact.None),
                    ref failure);
                ExceptionAggregation.Capture(() =>
                {
                    if (IsStateCommitCurrent(version, false))
                    {
                        Unchecked?.Invoke(this, eventArgs);
                    }
                }, ref failure);
                ExceptionAggregation.Capture(() =>
                {
                    if (IsStateCommitCurrent(version, false))
                    {
                        StateChanged?.Invoke(this, eventArgs);
                    }
                }, ref failure);
                failure?.Throw();
                return;
            }

            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    }

    /// <summary>Activates an available CheckBox through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public void PerformClick()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

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

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved before the mark
    /// glyph and outside the caption's own alignment box.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved after the
    /// caption and outside the caption's own alignment box.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = TextControl;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        var affixInset = affixes.StartCells + affixes.EndCells;

        if (content is null)
        {
            return new Size(MarkWidth.Add(affixInset), 1);
        }

        var desired = MeasureChild(
            content,
            new Constraint(constraint.Width.Subtract(MarkWidth + 1 + affixInset), constraint.Height));

        return content.Visibility == Visibility.Collapsed
            ? new Size(MarkWidth.Add(affixInset), 1)
            : new Size(
                (MarkWidth + 1).Add(affixInset).Add(desired.Width.Add(content.Margin.Horizontal)),
                Math.Max(1, desired.Height.Add(content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (TextControl is { } content)
        {
            var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
            var deflated = DeflateForAffixes(bounds, affixes);
            var consumed = Math.Min(MarkWidth + 1, deflated.Width);
            ArrangeChild(
                content,
                new Rect(deflated.X + consumed, deflated.Y, deflated.Width - consumed, deflated.Height),
                ResolvedAxes.Both);
        }
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;

        if (this.HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(Bounds, style);
        }

        var content = ContentBounds;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        _ = canvas.Draw(
            Mark().AsSpan(),
            new Point(content.X + affixes.StartCells, content.Y),
            style,
            background: BackgroundMode.Transparent);
        RenderAffixes(canvas, content, affixes, StartAffix, EndAffix, style);
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
        var version = ++_stateVersion;
        InvalidateVisualState();
        NotifyPropertyChanged(nameof(IsChecked), InvalidationImpact.None);

        if (!IsStateCommitCurrent(version, value))
        {
            return;
        }

        var eventArgs = new CheckChangedEventArgs(previous, value, cause);

        if (value == true)
        {
            Checked?.Invoke(this, eventArgs);
        }
        else if (value == false)
        {
            Unchecked?.Invoke(this, eventArgs);
        }
        else
        {
            Indeterminate?.Invoke(this, eventArgs);
        }

        if (IsStateCommitCurrent(version, value))
        {
            StateChanged?.Invoke(this, eventArgs);
        }
    }

    [Pure]
    private bool IsStateCommitCurrent(int version, bool? value) =>
        !IsDisposed && _stateVersion == version && _isChecked == value;

    private int MarkWidth => ActualStyle.MarkWidth;

    [Pure]
    private string Mark() =>
        new CheckMark(ActualStyle.MarkStyle, ActualStyle.Glyphs).Format(_isChecked, CellPolicy.AmbiguousWidth);
}
