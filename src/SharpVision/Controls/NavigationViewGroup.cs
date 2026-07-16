// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines a collapsible labeled group of retained navigation items.</summary>
public sealed class NavigationViewGroup: Pressable
{
    private readonly OwnedControlSlot _childrenSlot;
    private readonly Stack _stack;

    /// <summary>Initializes an expanded navigation group with no header.</summary>
    public NavigationViewGroup()
    {
        _stack = new Stack { Padding = new Thickness(2, 0, 0, 0) };
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

    /// <summary>Raised after a changed expansion state commits.</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>Raised before expansion or item structure changes for owner repair bookkeeping.</summary>
    internal event EventHandler? StructureChanging;

    /// <summary>Raised after item structure changes commit.</summary>
    internal event EventHandler? StructureChanged;

    /// <summary>Gets or sets the non-null group label.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control.</exception>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Terminal.Unicode.Width.Measure(value).Controls > 0)
            {
                throw new ArgumentException("A navigation group header cannot contain terminal controls.", nameof(value));
            }

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
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            StructureChanging?.Invoke(this, EventArgs.Empty);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
            _stack.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
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
        StructureChanging?.Invoke(this, EventArgs.Empty);
        _stack.Children.Add(item);
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes one identical sub-item without disposing it.</summary>
    /// <param name="item">The non-null candidate.</param>
    public bool RemoveItem(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_stack.Children.Contains(item))
        {
            return false;
        }

        StructureChanging?.Invoke(this, EventArgs.Empty);
        var removed = _stack.Children.Remove(item);
        Debug.Assert(removed, "A contained navigation group item must remove successfully.");
        StructureChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Removes every retained sub-item without disposing it.</summary>
    public void ClearItems()
    {
        if (_stack.Children.Count == 0)
        {
            VerifyMutable();
            return;
        }

        StructureChanging?.Invoke(this, EventArgs.Empty);
        _stack.Children.Clear();
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        _ = cause;
        IsExpanded = !IsExpanded;
    }

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
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            ExpandedChanged = null;
            StructureChanging = null;
            StructureChanged = null;
        }
    }
}
