// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

/// <summary>Arranges typed menu items and coordinates their keyboard selection and radio groups.</summary>
public sealed class Menu: Container
{
    private int _selectedIndex = -1;

    /// <summary>Initializes an empty horizontal menu with typed managed items.</summary>
    public Menu() => Items = new MenuItems(this);

    /// <summary>Raised after an owned item invokes through keyboard, pointer, or programmatic input.</summary>
    public event EventHandler<MenuItemInvokedEventArgs>? ItemInvoked;

    /// <summary>Gets the typed managed menu items.</summary>
    public MenuItems Items { get; }

    /// <summary>Gets or sets horizontal or vertical menu layout.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The menu orientation is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = Orientation.Horizontal;

    /// <summary>Gets or sets non-negative cells between participating items.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public int Spacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = 1;

    /// <summary>Gets or selects the active non-separator item index, or -1 for no selection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current item range.</exception>
    /// <exception cref="ArgumentException">The target is a separator.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value is < -1 or >= 0 && value >= Children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The selected index is outside the menu.");
            }

            if (value >= 0 && ItemAt(value) is MenuSeparator)
            {
                throw new ArgumentException("A separator cannot become selected.", nameof(value));
            }

            Select(value, focus: false);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var main = 0;
        var cross = 0;
        var count = 0;

        foreach (var child in Children)
        {
            var item = RequireEntry(child);
            var desiredSize = MeasureChild(item, Orientation == Orientation.Horizontal
                ? new Constraint(width: null, constraint.Height)
                : new Constraint(constraint.Width, height: null));
            var desiredMain = Orientation == Orientation.Horizontal ? desiredSize.Width : desiredSize.Height;
            var desiredCross = Orientation == Orientation.Horizontal ? desiredSize.Height : desiredSize.Width;
            main = Add(main, desiredMain);
            cross = Math.Max(cross, desiredCross);
            count++;
        }

        main = Add(main, SpacingExtent(count));
        return Orientation == Orientation.Horizontal ? new Size(main, cross) : new Size(cross, main);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var position = Orientation == Orientation.Horizontal ? bounds.X : bounds.Y;

        foreach (var child in Children)
        {
            var item = RequireEntry(child);
            var desired = Orientation == Orientation.Horizontal ? item.DesiredSize.Width : item.DesiredSize.Height;
            var slot = Orientation == Orientation.Horizontal
                ? new Rect(position, bounds.Y, Math.Min(desired, Math.Max(0, bounds.Right - position)), bounds.Height)
                : new Rect(bounds.X, position, bounds.Width, Math.Min(desired, Math.Max(0, bounds.Bottom - position)));
            ArrangeChild(item, slot, ResolvedAxes.Both);
            position = Add(position, Add(desired, Spacing));
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs.Handled || eventArgs is not KeyEventArgs { Stroke.Action: KeyAction.Press } key)
        {
            return;
        }

        var previous = Orientation == Orientation.Horizontal ? Code.Left : Code.Up;
        var next = Orientation == Orientation.Horizontal ? Code.Right : Code.Down;
        var target = key.Stroke.Code == previous ? FindAvailable(_selectedIndex, -1) :
            key.Stroke.Code == next ? FindAvailable(_selectedIndex, 1) : -1;

        if (target < 0)
        {
            return;
        }

        Select(target, focus: true);
        eventArgs.Handled = true;
    }

    /// <summary>Selects one radio item and clears matching siblings.</summary>
    /// <param name="item">The non-null owned radio item.</param>
    internal void SelectRadio(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!ReferenceEquals(item.Parent, this) || item.Kind != MenuItemKind.Radio)
        {
            throw new ArgumentException("The radio item must belong to this menu.", nameof(item));
        }

        var candidates = Items
            .OfType<MenuItem>()
            .Where(candidate => candidate.Kind == MenuItemKind.Radio &&
                string.Equals(candidate.GroupName, item.GroupName, StringComparison.Ordinal))
            .ToArray();
        var versions = new int[candidates.Length];

        for (var index = 0; index < candidates.Length; index++)
        {
            versions[index] = candidates[index].StageChecked(ReferenceEquals(candidates[index], item));
        }

        var failure = (ExceptionDispatchInfo?) null;

        for (var index = 0; index < candidates.Length; index++)
        {
            var expected = ReferenceEquals(candidates[index], item);

            if (candidates[index].IsCheckedCommitCurrent(versions[index], expected))
            {
                CaptureFailure(candidates[index].PublishChecked, ref failure);
            }
        }

        failure?.Throw();
    }

    /// <summary>Gets one checked typed child by index.</summary>
    /// <param name="index">The valid zero-based child index.</param>
    /// <returns>The exact owned item.</returns>
    internal Control ItemAt(int index) => RequireEntry(Children[index]);

    /// <summary>Adds one typed item and tracks its invocation.</summary>
    /// <param name="item">The non-null detached item.</param>
    internal void Add(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        AddEntry(item);
    }

    /// <summary>Adds one typed separator.</summary>
    /// <param name="separator">The non-null detached separator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    internal void Add(MenuSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        AddEntry(separator);
    }

    private void AddEntry(Control item)
    {
        Debug.Assert(item is MenuItem or MenuSeparator, "Menu entries are constrained by typed collection overloads.");
        Children.Add(item);

        if (_selectedIndex < 0 && item is MenuItem)
        {
            Select(Children.Count - 1, focus: false);
        }

        if (item is MenuItem { Kind: MenuItemKind.Radio, IsChecked: true } radio)
        {
            SelectRadio(radio);
        }
    }

    /// <summary>Removes one typed item and its subscription.</summary>
    /// <param name="item">The non-null item.</param>
    /// <returns>True when ownership was removed.</returns>
    internal bool Remove(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return RemoveEntry(item);
    }

    /// <summary>Removes one typed separator.</summary>
    /// <param name="separator">The non-null separator.</param>
    /// <returns>True when ownership was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    internal bool Remove(MenuSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        return RemoveEntry(separator);
    }

    private bool RemoveEntry(Control item)
    {
        var index = Children.IndexOf(item);

        if (index < 0)
        {
            return false;
        }

        _ = Children.Remove(item);
        Select(FindAvailable(Math.Min(index, Children.Count - 1), 1), focus: false);
        return true;
    }

    /// <summary>Clears items and subscriptions.</summary>
    internal void ClearItems()
    {
        Children.Clear();
        Select(-1, focus: false);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            ItemInvoked = null;
        }
    }

    /// <summary>Forwards one item invocation after the item's own subscribers complete.</summary>
    /// <param name="eventArgs">The non-null committed invocation payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    internal void NotifyItemInvoked(MenuItemInvokedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var index = Children.IndexOf(eventArgs.Item);

        if (index >= 0)
        {
            Select(index, focus: false);
        }

        ItemInvoked?.Invoke(this, eventArgs);
    }

    private void Select(int index, bool focus)
    {
        VerifyMutable();

        if (_selectedIndex == index)
        {
            return;
        }

        if (_selectedIndex >= 0 && _selectedIndex < Children.Count)
        {
            ((MenuItem) ItemAt(_selectedIndex)).CommitSelection(false);
        }

        _selectedIndex = index;

        if (index >= 0)
        {
            var item = (MenuItem) ItemAt(index);
            item.CommitSelection(true);

            if (focus)
            {
                _ = item.RequestMenuFocus();
            }
        }

        NotifyPropertyChanged(nameof(SelectedIndex), ChangeImpact.Render);
    }

    private int FindAvailable(int start, int direction)
    {
        if (Children.Count == 0)
        {
            return -1;
        }

        for (var offset = 1; offset <= Children.Count; offset++)
        {
            var index = (start + (direction * offset) + Children.Count) % Children.Count;
            var item = ItemAt(index);

            if (item is MenuItem menuItem && menuItem.EffectiveIsEnabled && menuItem.EffectiveIsVisible)
            {
                return index;
            }
        }

        return -1;
    }

    private static Control RequireEntry(Control child)
    {
        return child is MenuItem or MenuSeparator
            ? child
            : throw new InvalidOperationException("Menus may own only MenuItem and MenuSeparator controls through Items.");
    }

    private int SpacingExtent(int count)
    {
        Debug.Assert(count >= 0, "Menu participant count is non-negative.");

        return count < 2 ? 0 : Add(0, Spacing * (count - 1));
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "Menu accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "Menu accumulation uses non-negative extents.");

        return (int) Math.Min(int.MaxValue, (long) left + right);
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
