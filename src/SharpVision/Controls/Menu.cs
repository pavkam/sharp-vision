using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;

using KeyAction = SharpVision.Terminal.Input.Action;

namespace SharpVision.Controls;

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

            _ = Set(ref field, value, Invalidation.Measure);
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
            _ = Set(ref field, value, Invalidation.Measure);
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

            if (value >= 0 && ItemAt(value).Kind == MenuItemKind.Separator)
            {
                throw new ArgumentException("A separator cannot become selected.", nameof(value));
            }

            Select(value, focus: false);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        var main = 0;
        var cross = 0;
        var count = 0;

        foreach (var child in Children)
        {
            var item = RequireItem(child);
            item.Measure(Orientation == Orientation.Horizontal
                ? new Constraint(width: null, constraint.Height)
                : new Constraint(constraint.Width, height: null));
            var desiredMain = Orientation == Orientation.Horizontal ? item.DesiredSize.Width : item.DesiredSize.Height;
            var desiredCross = Orientation == Orientation.Horizontal ? item.DesiredSize.Height : item.DesiredSize.Width;
            main = Add(main, desiredMain);
            cross = Math.Max(cross, desiredCross);
            count++;
        }

        main = Add(main, SpacingExtent(count));
        return Orientation == Orientation.Horizontal ? new Size(main, cross) : new Size(cross, main);
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds)
    {
        var position = Orientation == Orientation.Horizontal ? bounds.X : bounds.Y;

        foreach (var child in Children)
        {
            var item = RequireItem(child);
            var desired = Orientation == Orientation.Horizontal ? item.DesiredSize.Width : item.DesiredSize.Height;
            var slot = Orientation == Orientation.Horizontal
                ? new Rect(position, bounds.Y, Math.Min(desired, Math.Max(0, bounds.Right - position)), bounds.Height)
                : new Rect(bounds.X, position, bounds.Width, Math.Min(desired, Math.Max(0, bounds.Bottom - position)));
            item.Arrange(slot, widthResolved: true, heightResolved: true);
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

        foreach (var candidate in Items)
        {
            if (candidate.Kind == MenuItemKind.Radio && candidate.GroupName == item.GroupName)
            {
                _ = candidate.CommitChecked(ReferenceEquals(candidate, item));
            }
        }
    }

    /// <summary>Gets one checked typed child by index.</summary>
    /// <param name="index">The valid zero-based child index.</param>
    /// <returns>The exact owned item.</returns>
    internal MenuItem ItemAt(int index) => RequireItem(Children[index]);

    /// <summary>Adds one typed item and tracks its invocation.</summary>
    /// <param name="item">The non-null detached item.</param>
    internal void Add(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Children.Add(item);
        item.Invoked += OnItemInvoked;

        if (_selectedIndex < 0 && item.Kind != MenuItemKind.Separator)
        {
            Select(Children.Count - 1, focus: false);
        }

        if (item.Kind == MenuItemKind.Radio && item.IsChecked)
        {
            SelectRadio(item);
        }
    }

    /// <summary>Removes one typed item and its subscription.</summary>
    /// <param name="item">The non-null item.</param>
    /// <returns>True when ownership was removed.</returns>
    internal bool Remove(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = Children.IndexOf(item);

        if (index < 0)
        {
            return false;
        }

        item.Invoked -= OnItemInvoked;
        _ = Children.Remove(item);
        Select(FindAvailable(Math.Min(index, Children.Count - 1), 1), focus: false);
        return true;
    }

    /// <summary>Clears items and subscriptions.</summary>
    internal void ClearItems()
    {
        foreach (var item in Items.ToArray())
        {
            item.Invoked -= OnItemInvoked;
        }

        Children.Clear();
        Select(-1, focus: false);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            foreach (var item in Items)
            {
                item.Invoked -= OnItemInvoked;
            }

            ItemInvoked = null;
        }
    }

    private void OnItemInvoked(object? sender, MenuItemInvokedEventArgs eventArgs)
    {
        _ = sender;
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
            ItemAt(_selectedIndex).CommitSelection(false);
        }

        _selectedIndex = index;

        if (index >= 0)
        {
            var item = ItemAt(index);
            item.CommitSelection(true);

            if (focus)
            {
                _ = FocusOwner?.Focus(item);
            }
        }

        NotifyChanged(nameof(SelectedIndex), Invalidation.Render);
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

            if (item.Kind != MenuItemKind.Separator && item.EffectiveIsEnabled && item.EffectiveIsVisible)
            {
                return index;
            }
        }

        return -1;
    }

    private static MenuItem RequireItem(Control child) => child as MenuItem ??
        throw new InvalidOperationException("Menus may own only MenuItem controls through Items.");

    private int SpacingExtent(int count) => count < 2 ? 0 : Add(0, Spacing * (count - 1));

    private static int Add(int left, int right) =>
        (int) Math.Min(int.MaxValue, (long) left + right);
}
