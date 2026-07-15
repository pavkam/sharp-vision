// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

/// <summary>Defines a focusable mutually exclusive selection control.</summary>
public sealed class RadioButton: Pressable
{
    private bool _isChecked;
    private int _checkedVersion;

    /// <summary>Initializes an unselected RadioButton.</summary>
    public RadioButton()
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
            VerifyMutable();

            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            var failure = (ExceptionDispatchInfo?) null;

            if (IsChecked)
            {
                CaptureFailure(
                    () => RadioGroup.Select(this, ActivationCause.Programmatic),
                    ref failure);
            }

            CaptureFailure(
                () => NotifyPropertyChanged(nameof(GroupName), ChangeImpact.None),
                ref failure);
            failure?.Throw();
        }
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
        var content = Content;

        if (content is null)
        {
            return new Size(1, 1);
        }

        var desired = MeasureChild(content, new Constraint(Subtract(constraint.Width, 2), constraint.Height));

        return content.Visibility == Visibility.Collapsed
            ? new Size(1, 1)
            : new Size(
                Add(2, Add(desired.Width, content.Margin.Horizontal)),
                Math.Max(1, Add(desired.Height, content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } content)
        {
            var consumed = Math.Min(2, bounds.Width);
            ArrangeChild(
                content,
                new Rect(bounds.X + consumed, bounds.Y, bounds.Width - consumed, bounds.Height),
                ResolvedAxes.Both);
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var glyph = new Rune(IsChecked ? '◉' : '○');
        Span<char> buffer = stackalloc char[2];
        var length = glyph.EncodeToUtf16(buffer);
        var style = ResolvedStyle;

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

        var reverse = key.Stroke.Code is Code.Left or Code.Up;

        if (reverse || key.Stroke.Code is Code.Right or Code.Down)
        {
            eventArgs.Handled = RadioGroup.Move(this, reverse);
        }
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(Control? previous, Control? current)
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
    internal void PublishChecked() =>
        NotifyPropertyChanged(nameof(IsChecked), ChangeImpact.None);

    /// <summary>Requests focus through this member's protected manager boundary.</summary>
    /// <returns>True when focus is acquired or already owned.</returns>
    /// <exception cref="InvalidOperationException">The attached member is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The member is disposed.</exception>
    internal bool RequestGroupFocus() => RequestFocus();

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
        Debug.Assert(left >= 0, "RadioButton accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "RadioButton accumulation uses non-negative extents.");

        var value = (long) left + right;
        return value >= int.MaxValue ? int.MaxValue : (int) value;
    }

    private static int? Subtract(int? value, int extent)
    {
        Debug.Assert(extent >= 0, "RadioButton subtraction extent is non-negative.");

        return value.HasValue
            ? Math.Max(0, value.Value - extent)
            : null;
    }

    private static void CaptureFailure(System.Action action, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }
}
