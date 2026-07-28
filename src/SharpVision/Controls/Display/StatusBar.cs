// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Displays concise contextual status in ordered leading and trailing item groups.</summary>
[PublicAPI]
public sealed class StatusBar: ItemsControl
{
    private readonly StatusBarHost _host;

    /// <summary>Initializes an empty one-cell status strip with one cell between adjacent items.</summary>
    public StatusBar()
    {
        _host = new StatusBarHost();
        InitializeItemsHost(_host);
        Items = new StatusBarItemCollection(this);
        Height = Length.Cells(1);
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    /// <summary>Gets the typed managed status-item collection.</summary>
    public StatusBarItemCollection Items { get; }

    /// <summary>Gets or sets non-negative terminal cells between adjacent visible items.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public int Spacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _host.Spacing = value;
            }
        }
    } = 1;

    /// <summary>Gets one checked typed status item by zero-based position.</summary>
    /// <param name="index">The valid zero-based item position.</param>
    /// <returns>The exact owned status item.</returns>
    internal StatusBarItem ItemAt(int index) => (StatusBarItem) GetItemControl(index);

    /// <summary>Gets the current semantic item count.</summary>
    internal int ItemCount => ItemControlCount;

    /// <summary>Adds one detached status item to the end of the collection.</summary>
    /// <param name="item">The non-null detached item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    internal void Add(StatusBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        InsertItemControl(ItemControlCount, item);
    }

    /// <summary>Removes one identical owned item without disposing it.</summary>
    /// <param name="item">The non-null item.</param>
    /// <returns>True when ownership was removed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    internal bool Remove(StatusBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return RemoveItemControl(item);
    }

    /// <summary>Removes all items without disposing the detached instances.</summary>
    internal void ClearItems() => ClearItemControls();
}
