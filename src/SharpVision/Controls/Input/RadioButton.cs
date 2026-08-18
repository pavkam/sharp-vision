// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

/// <summary>Defines a focusable mutually exclusive selection control.</summary>
[PublicAPI]
public sealed class RadioButton: InputBase, IStyled<RadioButtonStyle>
{
    private bool _isChecked;
    private int _checkedVersion;
    private readonly StyleSlot<RadioButtonStyle> _style;

    /// <summary>Initializes an unselected RadioButton.</summary>
    public RadioButton()
    {
        EnablePressActivation();
        EnableCaption();
        EnableCommand();
        _style = InitializeStyle(RadioButtonStyle.Definition);
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached RadioButton is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The RadioButton is disposed.</exception>
    public RadioButtonStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public RadioButtonStyle ActualStyle => _style.Actual;

    /// <summary>Initializes an unselected RadioButton with text content.</summary>
    /// <param name="text">The non-null text content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public RadioButton(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
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
    public override bool CanTabStop => IsTabStop && CanFocus && this.IsRovingTabStop();

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
    protected override void Activate(ActivationCause cause)
    {
        this.SelectInGroup(cause);

        // Unlike SelectInGroup, which is a hard no-op when this member is already the sole
        // checked one in its group, the command executes on every activation - re-selecting
        // the current member still counts as an activation.
        ExecuteCommandIfAny();
    }

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved before the mark
    /// glyph and outside the caption's own alignment box.</summary>
    /// <exception cref="InvalidOperationException">The attached RadioButton is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The RadioButton is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved after the
    /// caption and outside the caption's own alignment box.</summary>
    /// <exception cref="InvalidOperationException">The attached RadioButton is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The RadioButton is disposed.</exception>
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
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);

        if (eventArgs.IsHandled || eventArgs is not KeyEventArgs { Stroke.Action: KeyAction.Press } key)
        {
            return;
        }

        var reverse = key.Stroke.Code is Code.Left or Code.Up;

        if (reverse || key.Stroke.Code is Code.Right or Code.Down)
        {
            eventArgs.IsHandled = this.MoveGroup(reverse);
        }
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(ControlBase? previous, ControlBase? current)
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
        NotifyPropertyChanged(nameof(CanTabStop), InvalidationImpact.None);
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
