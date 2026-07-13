// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Terminal.Input;

/// <summary>Defines a focusable mutually exclusive selection control.</summary>
public sealed class RadioButton: Pressable
{
    private bool _isChecked;

    /// <summary>Initializes an unselected RadioButton.</summary>
    public RadioButton() : base(capacity: 1)
    {
    }

    /// <summary>Raised after this member becomes selected.</summary>
    public event EventHandler<SelectionChangedEventArgs>? Checked;

    /// <summary>Raised after this member loses selection.</summary>
    public event EventHandler<SelectionChangedEventArgs>? Unchecked;

    /// <summary>Raised on the newly selected or explicitly cleared member.</summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

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
                RadioGroup.Select(this, ActivationCause.Programmatic);
            }
            else
            {
                RadioGroup.Clear(this, ActivationCause.Programmatic);
            }
        }
    }

    /// <summary>Gets or sets an optional ordinal group name scoped to the attached root.</summary>
    /// <exception cref="InvalidOperationException">The attached member is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>
    public string? GroupName
    {
        get;
        set
        {
            if (Set(ref field, value, Invalidation.None) && IsChecked)
            {
                RadioGroup.Select(this, ActivationCause.Programmatic);
            }
        }
    }

    /// <summary>Gets or atomically sets optional owned label content.</summary>
    /// <exception cref="ArgumentException">The value cannot be owned by this RadioButton.</exception>
    /// <exception cref="InvalidOperationException">The attached member is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member or value is disposed.</exception>
    public Control? Content
    {
        get => Children.Count == 0 ? null : Children[0];
        set => Children.SetOnly(value);
    }

    /// <summary>Selects an available member through the programmatic path.</summary>
    /// <exception cref="InvalidOperationException">The attached member is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>
    public void PerformSelect()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) => RadioGroup.Select(this, cause);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        Control? content = Content;

        if (content is null)
        {
            return new Size(1, 1);
        }

        content.Measure(new Constraint(Subtract(constraint.Width, 2), constraint.Height));
        return new Size(
            Add(2, Add(content.DesiredSize.Width, content.Margin.Horizontal)),
            Math.Max(1, Add(content.DesiredSize.Height, content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } content)
        {
            int consumed = Math.Min(2, bounds.Width);
            content.Arrange(
                new Rect(bounds.X + consumed, bounds.Y, bounds.Width - consumed, bounds.Height),
                widthResolved: true,
                heightResolved: true);
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        Rune glyph = new(IsChecked ? '◉' : '○');
        Span<char> buffer = stackalloc char[2];
        int length = glyph.EncodeToUtf16(buffer);
        TerminalStyle style = ResolvedStyle;

        if (ControlAppearance.HasOpaqueFill(this, GetVisualState()))
        {
            canvas.Clear(Bounds, style);
        }

        _ = canvas.Draw(
            buffer[..length],
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

        bool reverse = key.Stroke.Code is Code.Left or Code.Up;

        if (reverse || key.Stroke.Code is Code.Right or Code.Down)
        {
            eventArgs.Handled = RadioGroup.Move(this, reverse);
        }
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(Container? previous, Container? current)
    {
        base.OnParentChanged(previous, current);

        if (current is not null && IsChecked)
        {
            RadioGroup.Select(this, ActivationCause.Programmatic);
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

    /// <summary>Commits a coordinated checked value without recursive selection.</summary>
    internal bool Commit(bool value) =>
        Set(ref _isChecked, value, Invalidation.Render, nameof(IsChecked));

    /// <summary>Raises Checked after a complete group commit.</summary>
    internal void RaiseChecked(SelectionChangedEventArgs eventArgs) =>
        Checked?.Invoke(this, eventArgs);

    /// <summary>Raises SelectionChanged after non-stale specific events.</summary>
    internal void RaiseSelectionChanged(SelectionChangedEventArgs eventArgs) =>
        SelectionChanged?.Invoke(this, eventArgs);

    /// <summary>Raises Unchecked after a complete group commit.</summary>
    internal void RaiseUnchecked(SelectionChangedEventArgs eventArgs) =>
        Unchecked?.Invoke(this, eventArgs);

    private static int Add(int left, int right)
    {
        long value = (long) left + right;
        return value >= int.MaxValue ? int.MaxValue : (int) value;
    }

    private static int? Subtract(int? value, int extent) => value.HasValue
        ? Math.Max(0, value.Value - extent)
        : null;
}
