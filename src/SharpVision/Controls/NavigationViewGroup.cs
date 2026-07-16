// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Defines a collapsible labeled group of retained navigation items.</summary>
public sealed class NavigationViewGroup: Control
{
    private readonly OwnedControlSlot _childrenSlot;
    private readonly Stack _stack;

    /// <summary>Initializes an expanded navigation group with no header.</summary>
    public NavigationViewGroup()
    {
        _stack = new Stack();
        _childrenSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "group-items",
                ChangeImpact.Measure),
            capacity: 1);
        _childrenSlot.Add(_stack);
        CanFocus = true;
    }

    /// <summary>Gets or sets the non-null group label.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets whether sub-items participate below the group header.</summary>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public bool IsExpanded
    {
        get;
        set
        {
            if (SetProperty(ref field, value, ChangeImpact.Measure))
            {
                _stack.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    } = true;

    /// <summary>Gets the number of retained sub-items.</summary>
    internal int ItemCount => _stack.Children.Count;

    /// <summary>Gets one retained sub-item by index.</summary>
    internal NavigationViewItem ItemAt(int index) => (NavigationViewItem) _stack.Children[index];

    /// <summary>Adds one non-null detached sub-item.</summary>
    /// <param name="item">The item whose ownership transfers to this group.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void AddItem(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Padding = new Thickness(2, 0, 0, 0);
        _stack.Children.Add(item);
    }

    /// <summary>Removes one identical sub-item without disposing it.</summary>
    /// <param name="item">The non-null candidate.</param>
    public bool RemoveItem(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _stack.Children.Remove(item);
    }

    /// <summary>Removes every retained sub-item without disposing it.</summary>
    public void ClearItems() => _stack.Children.Clear();

    /// <inheritdoc/>
    protected override bool OwnsPointerState => true;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var headerCells = (int) Math.Min(int.MaxValue, 4L + Terminal.Unicode.Width.Measure(Header).Cells);
        var childrenDesired = MeasureChild(_stack, constraint);
        var childrenHeight = IsExpanded ? childrenDesired.Height : 0;
        return new Size(
            Math.Max(headerCells, childrenDesired.Width),
            (int) Math.Min(int.MaxValue, 1L + childrenHeight));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var childBounds = IsExpanded && bounds.Height > 1
            ? new Rect(bounds.X, bounds.Y + 1, bounds.Width, bounds.Height - 1)
            : default;
        ArrangeChild(_stack, childBounds, ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var glyph = IsExpanded ? "▼" : "▶";
        _ = canvas.Draw(
            $" {glyph} {Header}".AsSpan(),
            new Point(Bounds.X, Bounds.Y),
            ResolvedStyle,
            background: BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!eventArgs.Handled &&
            eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press, Code: Code.Enter } })
        {
            IsExpanded = !IsExpanded;
            eventArgs.Handled = true;
        }
    }
}
