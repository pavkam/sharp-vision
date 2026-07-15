// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


/// <summary>Defines one focusable command, check, radio, or separator entry in a <see cref="Menu"/>.</summary>
public sealed class MenuItem: Pressable
{
    private bool _isChecked;

    /// <summary>Initializes an ordinary command item with empty header text.</summary>
    public MenuItem() : base(capacity: 0)
    {
    }

    /// <summary>Raised after an eligible item commits its optional check state and activation.</summary>
    public event EventHandler<MenuItemInvokedEventArgs>? Invoked;

    /// <summary>Gets or sets the non-null visible header text.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets the command, check, radio, or separator behavior.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public MenuItemKind Kind
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The menu item kind is unknown.");
            }

            if (Set(ref field, value, Invalidation.Measure) && value == MenuItemKind.Separator)
            {
                _ = CommitChecked(false);
            }

            CanFocus = value != MenuItemKind.Separator;
            IsHitTestVisible = value != MenuItemKind.Separator;
        }
    }

    /// <summary>Gets or sets the optional non-empty radio-group name.</summary>
    /// <exception cref="ArgumentException">The value is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public string? GroupName
    {
        get;
        set
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
            }

            _ = Set(ref field, value, Invalidation.None);
        }
    }

    /// <summary>Gets or sets the checked state for check and radio items.</summary>
    /// <exception cref="InvalidOperationException">The item is not a check or radio item, or is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (Kind is not (MenuItemKind.Check or MenuItemKind.Radio))
            {
                throw new InvalidOperationException("Only check and radio menu items have a checked state.");
            }

            VerifyMutable();

            if (Kind == MenuItemKind.Radio && value && Parent is Menu menu)
            {
                menu.SelectRadio(this);
                return;
            }

            _ = CommitChecked(value);
        }
    }

    /// <summary>Activates this item through the programmatic path when it is available.</summary>
    /// <exception cref="InvalidOperationException">The attached item is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public void PerformInvoke()
    {
        VerifyMutable();

        if (Kind != MenuItemKind.Separator && EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        switch (Kind)
        {
            case MenuItemKind.Check:
                _ = CommitChecked(!_isChecked);
                break;
            case MenuItemKind.Radio:
                if (Parent is Menu menu)
                {
                    menu.SelectRadio(this);
                }
                else
                {
                    _ = CommitChecked(true);
                }

                break;
            case MenuItemKind.Separator:
                return;
            case MenuItemKind.Command:
                break;
            default:
                break;
        }

        Invoked?.Invoke(this, new MenuItemInvokedEventArgs(this, cause));
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint.Height;
        return Kind == MenuItemKind.Separator
            ? new Size(3, 1)
            : new Size(Add(PrefixWidth, Terminal.Unicode.Width.Measure(Header).Cells), 1);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;

        if (Kind == MenuItemKind.Separator)
        {
            for (var x = Bounds.X; x < Bounds.Right; x++)
            {
                _ = canvas.Draw("─".AsSpan(), new Point(x, Bounds.Y), style, background: BackgroundMode.Transparent);
            }

            return;
        }

        var marker = Kind switch
        {
            MenuItemKind.Check => _isChecked ? "[x] " : "[ ] ",
            MenuItemKind.Radio => _isChecked ? "◉ " : "○ ",
            MenuItemKind.Command => string.Empty,
            MenuItemKind.Separator => string.Empty,
            _ => throw new InvalidOperationException("The validated menu item kind is unknown."),
        };
        _ = canvas.Draw(marker.AsSpan(), new Point(Bounds.X, Bounds.Y), style, background: BackgroundMode.Transparent);
        _ = canvas.Draw(Header.AsSpan(), new Point(Bounds.X + PrefixWidth, Bounds.Y), style, background: BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override bool IsCheckedState => _isChecked;

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Invoked = null;
        }
    }

    /// <summary>Commits the checked state for coordinated owner transactions.</summary>
    internal bool CommitChecked(bool value) => Set(ref _isChecked, value, Invalidation.Render, nameof(IsChecked));

    /// <summary>Commits selected visual state from the containing menu.</summary>
    internal void CommitSelection(bool value) => SetSelectedState(value);

    private int PrefixWidth => Kind == MenuItemKind.Check ? 4 : Kind == MenuItemKind.Radio ? 2 : 0;

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "MenuItem accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "MenuItem accumulation uses non-negative extents.");

        return (int) Math.Min(int.MaxValue, (long) left + right);
    }
}
