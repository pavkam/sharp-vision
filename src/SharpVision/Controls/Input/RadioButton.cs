// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

using DisplayText = Display.Text;

/// <summary>Defines a focusable mutually exclusive selection control.</summary>
[PublicAPI]
public sealed class RadioButton: Pressable
{
    private static readonly StyleContract<RadioButtonStyle> _styleContract = new(
        ThemeRole.Input,
        static profile => new RadioButtonStyle(
            RadioButtonStyle.Default.MarkStyle,
            RadioButtonStyle.Default.Glyphs,
            RadioButtonStyle.WithCheckedAccent(profile.WithoutChrome())),
        static (previous, _, current, _) =>
            previous.MarkWidth != current.MarkWidth
                ? InvalidationImpact.Measure
                : previous.MarkStyle != current.MarkStyle || previous.Glyphs != current.Glyphs
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None,
        static style => style.Appearance);
    private bool _isChecked;
    private int _checkedVersion;
    private RadioButtonStyle? _actualStyleCache;
    private RadioButtonStyle? _actualStyleCacheKey;
    private Theme? _actualStyleCacheTheme;

    /// <summary>Initializes an unselected RadioButton.</summary>
    public RadioButton()
    {
    }

    /// <summary>Initializes an unselected RadioButton with text content.</summary>
    /// <param name="text">The non-null text content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public RadioButton(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Content = new DisplayText(text);
    }

    /// <summary>Raised after this member becomes selected.</summary>
    public event EventHandler<RadioButtonSelectionChangedEventArgs>? Checked;

    /// <summary>Raised after this member loses selection.</summary>
    public event EventHandler<RadioButtonSelectionChangedEventArgs>? Unchecked;

    /// <summary>Raised on the newly selected or explicitly cleared member.</summary>
    public event EventHandler<RadioButtonSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets or sets whether this member is selected.</summary>
    /// <exception cref="InvalidOperationException">The attached member is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            VerifyMutable();

            if (value)
            {
                this.SelectInGroup(ActivationCause.Programmatic);
            }
            else
            {
                this.ClearGroup(ActivationCause.Programmatic);
            }
        }
    }

    /// <inheritdoc/>
    public override bool IsTabStop => TabStop && CanFocus && this.IsRovingTabStop();

    /// <summary>Gets or sets an optional ordinal group name scoped to the attached root.</summary>
    /// <exception cref="InvalidOperationException">The attached member is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>
    public string? GroupName
    {
        get;
        set
        {
            VerifyMutable();

            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            ExceptionDispatchInfo? failure = null;

            if (IsChecked)
            {
                ExceptionAggregation.Capture(
                    () => this.SelectInGroup(ActivationCause.Programmatic),
                    ref failure);
            }

            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(GroupName), InvalidationImpact.None),
                ref failure);
            failure?.Throw();
        }
    }

    /// <summary>Gets or sets the complete local presentation, or null to use the semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached member is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>
    public RadioButtonStyle? Style
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
    /// resolving the default style rebuilds two themed <see cref="ThemeProfile"/> instances from
    /// scratch (see #179), and this property is read from multiple per-frame paths.
    /// </remarks>
    public RadioButtonStyle ActualStyle =>
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

    /// <summary>Activates an available RadioButton through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached member is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>
    public void PerformClick()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <summary>Selects an available member through the programmatic path.</summary>
    /// <exception cref="InvalidOperationException">The attached member is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) => this.SelectInGroup(cause);

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
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        if (eventArgs.Handled || eventArgs is not KeyEventArgs { Stroke.Action: KeyAction.Press } key)
        {
            return;
        }

        var reverse = key.Stroke.Code is Code.Left or Code.Up;

        if (reverse || key.Stroke.Code is Code.Right or Code.Down)
        {
            eventArgs.Handled = this.MoveGroup(reverse);
        }
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(Control? previous, Control? current)
    {
        base.OnParentChanged(previous, current);

        if (current is not null && IsChecked)
        {
            this.SelectInGroup(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override bool IsCheckedState => IsChecked;

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Checked = null;
            Unchecked = null;
            SelectionChanged = null;
        }
    }

    /// <summary>Stages a coordinated checked value without publishing a partial group.</summary>
    /// <param name="value">The checked value to commit.</param>
    /// <returns>The new commit version, or zero when the value is unchanged.</returns>
    internal int StageChecked(bool value)
    {
        VerifyMutable();

        if (_isChecked == value)
        {
            return 0;
        }

        _isChecked = value;
        _checkedVersion++;
        InvalidateVisualState();
        return _checkedVersion;
    }

    /// <summary>Gets whether one staged checked commit remains current after callbacks.</summary>
    /// <param name="version">The positive staged commit version.</param>
    /// <param name="value">The expected staged value.</param>
    /// <returns>True when no reentrant selection replaced the commit.</returns>
    internal bool IsCheckedCommitCurrent(int version, bool value) =>
        version > 0 && _checkedVersion == version && _isChecked == value;

    /// <summary>Publishes the property notification for one still-current staged commit.</summary>
    /// <exception cref="InvalidOperationException">The attached member is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>
    internal void PublishChecked()
    {
        NotifyPropertyChanged(nameof(IsChecked), InvalidationImpact.None);
        NotifyPropertyChanged(nameof(IsTabStop), InvalidationImpact.None);
    }

    /// <summary>Requests focus through this member's protected manager boundary.</summary>
    /// <returns>True when focus is acquired or already owned.</returns>
    /// <exception cref="InvalidOperationException">The attached member is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>
    internal bool RequestGroupFocus() => RequestFocus();

    /// <summary>Raises Checked after a complete group commit.</summary>
    internal void RaiseChecked(RadioButtonSelectionChangedEventArgs eventArgs) =>
        Checked?.Invoke(this, eventArgs);

    /// <summary>Raises SelectionChanged after non-stale specific events.</summary>
    internal void RaiseSelectionChanged(RadioButtonSelectionChangedEventArgs eventArgs) =>
        SelectionChanged?.Invoke(this, eventArgs);

    /// <summary>Raises Unchecked after a complete group commit.</summary>
    internal void RaiseUnchecked(RadioButtonSelectionChangedEventArgs eventArgs) =>
        Unchecked?.Invoke(this, eventArgs);

    private int MarkWidth => ActualStyle.MarkWidth;

    private string Mark()
    {
        var selection = ControlGlyphs.Selection;
        var style = ActualStyle;

        return style.MarkStyle switch
        {
            RadioButtonMarkStyle.Circle => Mark(
                IsChecked ? style.Glyphs.Checked : style.Glyphs.Unchecked,
                IsChecked ? selection.RadioChecked.Fallback : selection.RadioUnchecked.Fallback),
            RadioButtonMarkStyle.Parentheses => IsChecked
                ? $"({Mark(style.Glyphs.Checked, selection.RadioParenthesesChecked.Fallback)})"
                : $"({Mark(style.Glyphs.Unchecked, selection.RadioParenthesesUnchecked.Fallback)})",
            _ => throw new UnreachableException()
        };
    }

    private string Mark(Rune value, Rune fallback) =>
        value.Resolve(fallback, CellPolicy.AmbiguousWidth).ToString();
}
