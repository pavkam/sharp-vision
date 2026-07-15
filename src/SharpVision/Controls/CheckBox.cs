// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;



/// <summary>Defines a focusable two- or three-state toggle with optional content.</summary>
public sealed class CheckBox: Pressable
{
    private bool? _isChecked = false;

    /// <summary>Initializes an unchecked two-state CheckBox.</summary>
    public CheckBox() : base(capacity: 1)
    {
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
            if (!Set(ref field, value, Invalidation.None))
            {
                return;
            }

            if (!value && _isChecked is null)
            {
                SetChecked(false, ActivationCause.Programmatic);
            }
        }
    }

    /// <summary>Gets or atomically sets the optional owned label content.</summary>
    /// <exception cref="ArgumentException">The value cannot be owned by this CheckBox.</exception>
    /// <exception cref="InvalidOperationException">The attached CheckBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox or value is disposed.</exception>
    public Control? Content
    {
        get => Children.Count == 0 ? null : Children[0];
        set => Children.SetOnly(value);
    }

    /// <summary>Gets or sets the validated state glyphs.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public Marks Marks
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = Marks.Default;

    /// <summary>Gets or sets the built-in mark family used before the optional label.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached CheckBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public CheckBoxMarks MarkStyle
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The checkbox mark style is unknown.");
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Toggles an available CheckBox through the programmatic activation path.</summary>
    /// <exception cref="InvalidOperationException">The attached CheckBox is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The CheckBox is disposed.</exception>
    public void PerformToggle()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        bool? next = _isChecked switch
        {
            false => true,
            true when IsThreeState => null,
            _ => false,
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

        content.Measure(new Constraint(Subtract(constraint.Width, MarkWidth + 1), constraint.Height));
        return new Size(
            Add(MarkWidth + 1, Add(content.DesiredSize.Width, content.Margin.Horizontal)),
            Math.Max(1, Add(content.DesiredSize.Height, content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } content)
        {
            var consumed = Math.Min(MarkWidth + 1, bounds.Width);
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

        var style = ResolvedStyle;

        if (ControlAppearance.HasOpaqueFill(this, GetVisualState()))
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

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "CheckBox accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "CheckBox accumulation uses non-negative extents.");

        var value = (long) left + right;
        return value >= int.MaxValue ? int.MaxValue : (int) value;
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

        if (!Set(ref _isChecked, value, Invalidation.Render, nameof(IsChecked)))
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

    private static int? Subtract(int? value, int extent)
    {
        Debug.Assert(extent >= 0, "CheckBox subtraction extent is non-negative.");

        return value.HasValue
            ? Math.Max(0, value.Value - extent)
            : null;
    }

    private int MarkWidth => MarkStyle == CheckBoxMarks.Brackets ? 3 : 1;

    private string Mark()
    {
        return MarkStyle switch
        {
            CheckBoxMarks.Brackets => _isChecked switch
            {
                true => "[x]",
                false => "[ ]",
                null => "[-]",
            },
            CheckBoxMarks.Tick => _isChecked switch
            {
                true => Mark(new Rune('✓'), new Rune('x')),
                false => Mark(new Rune('○'), new Rune('o')),
                null => Mark(new Rune('−'), new Rune('-')),
            },
            CheckBoxMarks.Square => _isChecked switch
            {
                true => Mark(Marks.Checked, new Rune('x')),
                false => Mark(Marks.Unchecked, new Rune('o')),
                null => Mark(Marks.Indeterminate, new Rune('-')),
            },
            _ => throw new UnreachableException(),
        };
    }

    private string Mark(Rune value, Rune fallback) => CellGlyph.Resolve(value, fallback, CellPolicy.AmbiguousWidth).ToString();
}
