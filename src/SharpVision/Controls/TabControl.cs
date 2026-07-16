// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Arranges typed tab pages and coordinates header rendering and keyboard selection.</summary>
public sealed class TabControl: ItemsControl
{
    private readonly Stack _stack;
    private int _selectedIndex = -1;

    /// <summary>Initializes an empty tab control with typed managed pages.</summary>
    public TabControl()
    {
        _stack = new Stack { Orientation = Orientation.Vertical };
        InitializeItemsHost(_stack);
        Items = new TabItems(this);
        CanFocus = true;
    }

    /// <summary>Raised after the selected tab index changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets the typed managed tab pages.</summary>
    public TabItems Items { get; }

    /// <summary>Gets or selects the active page index, or -1 for no selection.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || (value >= 0 && value >= ItemControlCount))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The selected index is outside the tab control.");
            }

            Select(value);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        for (var i = 0; i < ItemControlCount; i++)
        {
            ((TabItem) GetItemControl(i)).Visibility = i == _selectedIndex ? Visibility.Visible : Visibility.Collapsed;
        }

        var contentConstraint = new Constraint(constraint.Width, constraint.Height.HasValue ? Math.Max(0, constraint.Height.Value - 2) : null);
        var content = base.MeasureOverride(contentConstraint);

        var headerWidth = 0;
        for (var i = 0; i < ItemControlCount; i++)
        {
            headerWidth = (int) Math.Min(int.MaxValue, (long) headerWidth + Terminal.Unicode.Width.Measure(((TabItem) GetItemControl(i)).Header).Cells + 3);
        }

        return new Size(Math.Max(headerWidth, content.Width), (int) Math.Min(int.MaxValue, 2L + content.Height));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (bounds.Height < 2) { return; }

        for (var i = 0; i < ItemControlCount; i++)
        {
            ((TabItem) GetItemControl(i)).Visibility = i == _selectedIndex ? Visibility.Visible : Visibility.Collapsed;
        }

        base.ArrangeOverride(new Rect(bounds.X, bounds.Y + 2, bounds.Width, bounds.Height - 2));
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height < 2) { return; }
        var style = ResolvedStyle;
        var x = Bounds.X;
        for (var i = 0; i < ItemControlCount; i++)
        {
            var item = (TabItem) GetItemControl(i);
            var sel = i == _selectedIndex;
            var label = $" {item.Header} ";
            var cells = Terminal.Unicode.Width.Measure(label).Cells;
            if (x + cells > Bounds.Right) { break; }
            if (sel) { canvas.Clear(new Rect(x, Bounds.Y, cells, 1), style); }
            _ = canvas.Draw(label.AsSpan(), new Point(x, Bounds.Y), style, background: sel ? BackgroundMode.Opaque : BackgroundMode.Transparent);
            if (i < ItemControlCount - 1) { _ = canvas.Draw("│".AsSpan(), new Point(x + cells, Bounds.Y), style, background: BackgroundMode.Transparent); x += cells + 1; }
            else { x += cells; }
        }

        for (var lx = Bounds.X; lx < Bounds.Right; lx++)
        {
            _ = canvas.Draw("─".AsSpan(), new Point(lx, Bounds.Y + 1), style, background: BackgroundMode.Transparent);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (eventArgs.Handled || eventArgs is not KeyEventArgs { Stroke.Action: KeyAction.Press } key) { return; }
        if (key.Stroke.Code == Code.Left && _selectedIndex > 0) { Select(_selectedIndex - 1); eventArgs.Handled = true; }
        else if (key.Stroke.Code == Code.Right && _selectedIndex < ItemControlCount - 1) { Select(_selectedIndex + 1); eventArgs.Handled = true; }
    }

    internal int ItemCount => ItemControlCount;
    internal TabItem ItemAt(int index) => (TabItem) GetItemControl(index);
    internal void AddItem(TabItem item) { ArgumentNullException.ThrowIfNull(item); InsertItemControl(ItemControlCount, item); if (_selectedIndex < 0) { Select(ItemControlCount - 1); } }

    internal bool RemoveItem(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var idx = IndexOfItemControl(item);
        if (idx < 0) { return false; }
        _ = RemoveItemControl(item);
        if (_selectedIndex >= ItemControlCount) { Select(ItemControlCount - 1); }
        else if (_selectedIndex == idx) { Select(Math.Min(idx, ItemControlCount - 1)); }
        return true;
    }

    internal void ClearItems() { ClearItemControls(); Select(-1); }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason) { base.OnUnavailable(reason); if (reason == ReleaseReason.Disposed) { SelectionChanged = null; } }

    private void Select(int index)
    {
        VerifyMutable();
        if (_selectedIndex == index) { return; }
        _selectedIndex = index;
        NotifyPropertyChanged(nameof(SelectedIndex), ChangeImpact.Measure);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
