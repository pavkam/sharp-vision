// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using DisplayText = Display.Text;

/// <summary>Defines a focusable two- or three-state toggle with optional content.</summary>
[PublicAPI]
public sealed class CheckBox: Pressable
{
    private static readonly StyleContract<CheckBoxStyle> _styleContract = new(
        ThemeRole.Input,
        static profile => new CheckBoxStyle(
            CheckBoxStyle.Default.MarkStyle,
            CheckBoxStyle.Default.Glyphs,
            profile.WithoutChrome()),
        static (previous, _, current, _) =>
            previous.MarkWidth != current.MarkWidth
                ? InvalidationImpact.Measure
                : previous.MarkStyle != current.MarkStyle || previous.Glyphs != current.Glyphs
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None,
        static style => style.Appearance);
    private bool? _isChecked = false;
    private CheckBoxStyle? _actualStyleCache;
    private CheckBoxStyle? _actualStyleCacheKey;
    private Theme? _actualStyleCacheTheme;

    /// <summary>Initializes an unchecked two-state CheckBox.</summary>
    public CheckBox() => HorizontalAlignment = HorizontalAlignment.Left;

    /// <summary>Initializes an unchecked two-state CheckBox with text content.</summary>
    /// <param name="text">The non-null text content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public CheckBox(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Content = new DisplayText(text);
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
    public bool IsThreeState
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
                    () => NotifyPropertyChanged(nameof(IsThreeState), InvalidationImpact.None),
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

    /// <summary>Gets or sets the complete local presentation, or null to use the semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public CheckBoxStyle? Style
    {
        get;
        set => _ = SetControlStyle(
            ref field,
            value,
            _styleContract.Resolve,
            _styleContract.CompareStructure,
            _styleContract.Appearance,
            nameof(Style),
            nameof(ActualStyle));
    }

    /// <summary>Gets the complete local presentation or library mark mechanics completed with the semantic input profile.</summary>
    /// <remarks>
    /// Memoized against the exact (<see cref="Style"/>, <see cref="Theme"/>) pair that produced it:
    /// resolving the default style rebuilds a themed <see cref="ThemeProfile"/> from scratch (see
    /// #179), and this property is read from multiple per-frame paths.
    /// </remarks>
    public CheckBoxStyle ActualStyle =>
        ResolveContractStyle(
            _styleContract,
            ref _actualStyleCache,
            ref _actualStyleCacheKey,
            ref _actualStyleCacheTheme,
            Style,
            Theme);

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => _styleContract.Role;

    /// <inheritdoc/>
    protected override ThemeProfile AppearanceProfile => ActualStyle.Appearance;

    /// <inheritdoc/>
    protected override ThemeProfile GetAppearanceProfile(Theme? theme) =>
        GetContractAppearanceProfile(_styleContract, Style, theme);

    /// <inheritdoc/>
    protected override InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        GetContractThemeChangeImpact(
            _styleContract,
            Style,
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace);

    /// <inheritdoc/>
    protected override string? GetThemeResolvedStylePropertyName(Theme? previous, Theme? current) =>
        GetContractResolvedStylePropertyName(_styleContract, Style, previous, current, nameof(ActualStyle));

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
            true when IsThreeState => null,
            _ => false
        };
        SetChecked(next, cause);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = Content;

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
        if (Content is { } content)
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

        if (ControlAppearance.HasOpaqueFill(this, GetAppearanceState()))
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

    private void SetChecked(bool? value, ActivationCause cause)
    {
        if (value is null && !IsThreeState)
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
        CellGlyphResolver.Resolve(value, fallback, CellPolicy.AmbiguousWidth).ToString();
}
