// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

/// <summary>Defines a focusable two- or three-state toggle with optional content.</summary>
[PublicAPI]
public sealed class CheckBox: Pressable<CheckBoxStyle>
{
    private bool? _isChecked = false;

    /// <summary>Initializes an unchecked two-state CheckBox.</summary>
    public CheckBox() : base(CheckBoxStyle.Definition) =>
        HorizontalAlignment = HorizontalAlignment.Left;

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
                ExceptionDispatchInfo? failure = null;
                ExceptionAggregation.Capture(
                    () => NotifyPropertyChanged(nameof(ThreeState), InvalidationImpact.None),
                    ref failure);
                ExceptionAggregation.Capture(
                    () => NotifyPropertyChanged(nameof(IsChecked), InvalidationImpact.None),
                    ref failure);
                ExceptionAggregation.Capture(() => Unchecked?.Invoke(this, eventArgs), ref failure);
                ExceptionAggregation.Capture(() => StateChanged?.Invoke(this, eventArgs), ref failure);
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
        bool? next = _isChecked switch
        {
            false => true,
            true when ThreeState => null,
            _ => false
        };
        SetChecked(next, cause);
        ExecuteCommandIfAny();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = TextControl;

        if (content is null)
        {
            return new Size(MarkWidth, 1);
        }

        var desired = MeasureChild(
            content,
            new Constraint(constraint.Width.Subtract(MarkWidth + 1), constraint.Height));

        return content.Visibility == Visibility.Collapsed
            ? new Size(MarkWidth, 1)
            : new Size(
                (MarkWidth + 1).Add(desired.Width.Add(content.Margin.Horizontal)),
                Math.Max(1, desired.Height.Add(content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (TextControl is { } content)
        {
            var consumed = Math.Min(MarkWidth + 1, bounds.Width);
            ArrangeChild(
                content,
                new Rect(bounds.X + consumed, bounds.Y, bounds.Width - consumed, bounds.Height),
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

        _ = canvas.Draw(
            Mark().AsSpan(),
            new Point(Bounds.X, Bounds.Y),
            style,
            background: BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override bool CheckedState => _isChecked == true;

    /// <inheritdoc/>
    protected override bool IndeterminateState => _isChecked is null;

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

    private void SetChecked(bool? value, ActivationCause cause)
    {
        if (value is null && !ThreeState)
        {
            throw new ArgumentException(
                "An indeterminate value requires three-state mode.",
                nameof(value));
        }

        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), cause, "The activation cause is unknown.");
        }

        var previous = _isChecked;

        if (!SetVisualStateProperty(ref _isChecked, value, nameof(IsChecked)))
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

        StateChanged?.Invoke(this, eventArgs);
    }

    private int MarkWidth => ActualStyle.MarkWidth;

    private string Mark()
    {
        var selection = ControlGlyphs.Selection;

        var style = ActualStyle;
        return style.MarkStyle switch
        {
            CheckBoxMarkStyle.Brackets => _isChecked switch
            {
                true => $"[{Mark(style.Glyphs.Checked, selection.CheckBoxBracketChecked.Fallback)}]",
                false => $"[{Mark(style.Glyphs.Unchecked, selection.CheckBoxBracketUnchecked.Fallback)}]",
                null => $"[{Mark(style.Glyphs.Indeterminate, selection.CheckBoxBracketIndeterminate.Fallback)}]"
            },
            CheckBoxMarkStyle.Tick => _isChecked switch
            {
                true => Mark(style.Glyphs.Checked, selection.CheckBoxTickChecked.Fallback),
                false => Mark(style.Glyphs.Unchecked, selection.CheckBoxTickUnchecked.Fallback),
                null => Mark(style.Glyphs.Indeterminate, selection.CheckBoxTickIndeterminate.Fallback)
            },
            CheckBoxMarkStyle.Square => _isChecked switch
            {
                true => Mark(style.Glyphs.Checked, selection.CheckBoxSquareChecked.Fallback),
                false => Mark(style.Glyphs.Unchecked, selection.CheckBoxSquareUnchecked.Fallback),
                null => Mark(style.Glyphs.Indeterminate, selection.CheckBoxSquareIndeterminate.Fallback)
            },
            _ => throw new UnreachableException()
        };
    }

    private string Mark(Rune value, Rune fallback) =>
        value.Resolve(fallback, CellPolicy.AmbiguousWidth).ToString();
}
