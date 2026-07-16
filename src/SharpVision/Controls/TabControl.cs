// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Coordinates typed retained tab pages, header activation, selection repair, and overflow reveal.</summary>
public sealed class TabControl: ItemsControl
{
    private readonly TabPresenter _presenter;

    /// <summary>Initializes an empty TabControl with one private retained presenter.</summary>
    public TabControl()
    {
        Items = new TabItems(this);
        _presenter = new TabPresenter(this);
        InitializeItemsHost(_presenter);
        _ = AddHandler(Events.Key, OnKeyRouted);
    }

    /// <summary>Raised after the selected page identity and retained header state commit.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets the mutable typed semantic page collection.</summary>
    public TabItems Items { get; }

    /// <summary>Gets or sets the selected eligible page index; -1 clears selection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than -1 or outside Items.</exception>
    /// <exception cref="InvalidOperationException">The target page is disabled or not visible.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int SelectedIndex
    {
        get => SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
        set
        {
            if (value < -1 || value >= Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "SelectedIndex is outside Items.");
            }

            if (value >= 0 && !IsEligible(Items[value]))
            {
                throw new InvalidOperationException("A disabled or hidden tab cannot be selected.");
            }

            VerifyMutable();
            CommitSelection(value < 0 ? null : Items[value], raiseEvent: true);
        }
    }

    /// <summary>Gets the selected page identity, or null when selection is cleared.</summary>
    public TabItem? SelectedItem { get; private set; }

    /// <summary>Gets the current non-negative clipped header-strip origin in terminal cells.</summary>
    public int HeaderOffset => _presenter.HeaderOffset;

    /// <summary>Inserts one validated page on behalf of the owned typed collection.</summary>
    internal void InsertTab(TabItems sender, int index, TabItem item)
    {
        VerifyCollection(sender);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) index, (uint) Items.Count, nameof(index));
        var previousIndex = SelectedIndex;
        InsertItemControl(index, item);
        Items.InsertAttached(index, item);
        Subscribe(item);

        if (SelectedItem is null)
        {
            CommitSelection(FindEligible(0, 1, wrap: false), raiseEvent: true);
        }
        else if (SelectedIndex != previousIndex)
        {
            NotifyPropertyChanged(nameof(SelectedIndex), ChangeImpact.Measure);
        }
    }

    /// <summary>Removes one page on behalf of the owned typed collection without disposing it.</summary>
    internal void RemoveTab(TabItems sender, int index)
    {
        VerifyCollection(sender);
        var removed = Items[index];
        var wasSelected = ReferenceEquals(removed, SelectedItem);
        var previousIndex = SelectedIndex;
        Unsubscribe(removed);
        RemoveItemControlAt(index);
        Items.RemoveAttached(index);

        if (wasSelected)
        {
            CommitSelection(FindNearest(index), raiseEvent: true);
        }
        else if (SelectedItem is not null && SelectedIndex != previousIndex)
        {
            NotifyPropertyChanged(nameof(SelectedIndex), ChangeImpact.Measure);
        }
    }

    /// <summary>Replaces one page on behalf of the owned typed collection without disposing the previous page.</summary>
    internal void ReplaceTab(TabItems sender, int index, TabItem item)
    {
        VerifyCollection(sender);
        var previous = Items[index];

        if (ReferenceEquals(previous, item))
        {
            VerifyMutable();
            return;
        }

        var wasSelected = ReferenceEquals(previous, SelectedItem);
        Unsubscribe(previous);

        try
        {
            ReplaceItemControl(index, item);
        }
        catch
        {
            Subscribe(previous);
            throw;
        }

        Items.ReplaceAttached(index, item);
        Subscribe(item);

        if (wasSelected)
        {
            CommitSelection(IsEligible(item) ? item : FindNearest(index), raiseEvent: true);
        }
        else if (SelectedItem is null)
        {
            CommitSelection(FindEligible(0, 1, wrap: false), raiseEvent: true);
        }
    }

    /// <summary>Clears every page on behalf of the owned typed collection without disposing them.</summary>
    internal void ClearTabs(TabItems sender)
    {
        VerifyCollection(sender);

        if (Items.Count == 0)
        {
            VerifyMutable();
            return;
        }

        foreach (var item in Items)
        {
            Unsubscribe(item);
        }

        ClearItemControls();
        Items.ClearAttached();
        CommitSelection(null, raiseEvent: true);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            SelectionChanged = null;
        }
    }

    private static bool IsEligible(TabItem item) => item.IsEnabled && item.Visibility == Visibility.Visible;

    private void CommitSelection(TabItem? next, bool raiseEvent)
    {
        if (ReferenceEquals(SelectedItem, next))
        {
            return;
        }

        var previous = SelectedItem;
        previous?.CommitSelection(false);
        SelectedItem = next;
        next?.CommitSelection(true);
        NotifyPropertyChanged(nameof(SelectedIndex), ChangeImpact.Measure);
        NotifyPropertyChanged(nameof(SelectedItem), ChangeImpact.None);

        if (raiseEvent)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private TabItem? FindEligible(int start, int direction, bool wrap)
    {
        if (Items.Count == 0)
        {
            return null;
        }

        var index = start;

        for (var visited = 0; visited < Items.Count; visited++)
        {
            if (wrap)
            {
                index = ((index % Items.Count) + Items.Count) % Items.Count;
            }
            else if (index < 0 || index >= Items.Count)
            {
                return null;
            }

            if (IsEligible(Items[index]))
            {
                return Items[index];
            }

            index += direction;
        }

        return null;
    }

    private TabItem? FindNearest(int index) =>
        FindEligible(index, 1, wrap: false) ?? FindEligible(index - 1, -1, wrap: false);

    private void OnHeaderActivated(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;
        var item = (TabItem) sender!;

        if (IsEligible(item))
        {
            CommitSelection(item, raiseEvent: true);
        }
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not nameof(IsEnabled) and not nameof(Visibility))
        {
            return;
        }

        var item = (TabItem) sender!;

        if (ReferenceEquals(item, SelectedItem) && !IsEligible(item))
        {
            CommitSelection(FindNearest(Items.IndexOf(item)), raiseEvent: true);
        }
        else if (SelectedItem is null && IsEligible(item))
        {
            CommitSelection(item, raiseEvent: true);
        }
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble || eventArgs.Stroke.Action != KeyAction.Press || Items.Count == 0)
        {
            return;
        }

        var current = FindItem(eventArgs.OriginalSource) ?? SelectedItem;
        var currentIndex = current is null ? -1 : Items.IndexOf(current);
        var target = eventArgs.Stroke.Code == Code.Left
            ? FindEligible(currentIndex - 1, -1, wrap: true)
            : eventArgs.Stroke.Code == Code.Right
                ? FindEligible(currentIndex + 1, 1, wrap: true)
                : eventArgs.Stroke.Code == Code.Home
                    ? FindEligible(0, 1, wrap: false)
                    : eventArgs.Stroke.Code == Code.End
                        ? FindEligible(Items.Count - 1, -1, wrap: false)
                        : null;

        if (target is null)
        {
            return;
        }

        CommitSelection(target, raiseEvent: true);
        _ = FocusOwner?.Focus(target.HeaderPart);
        Invalidate(ChangeImpact.Arrange);
        eventArgs.Handled = true;
    }

    private static TabItem? FindItem(Control? source)
    {
        for (var current = source; current is not null; current = current.Parent)
        {
            if (current is TabItem item)
            {
                return item;
            }
        }

        return null;
    }

    private void Subscribe(TabItem item)
    {
        item.HeaderActivated += OnHeaderActivated;
        item.PropertyChanged += OnItemPropertyChanged;
    }

    private void Unsubscribe(TabItem item)
    {
        item.HeaderActivated -= OnHeaderActivated;
        item.PropertyChanged -= OnItemPropertyChanged;
    }

    private void VerifyCollection(TabItems sender)
    {
        if (!ReferenceEquals(sender, Items))
        {
            throw new InvalidOperationException("The tab collection does not belong to this control.");
        }
    }
}
