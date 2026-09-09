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
        EnableChromeAuthoring();
        _host = new StatusBarHost();
        InitializeItemsHost(_host);
        Items = new StatusBarItemCollection(this);
        Height = Length.Cells(1);
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        BarAppearance.Rebase((theme ?? ThemeCatalog.Dark).GetStyleSet(ControlStyle.Default));

    /// <inheritdoc/>
    internal override bool ProvidesContinuousBackground => true;

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

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () => _host.Spacing = Spacing);
        }
    } = 1;

    /// <summary>Moves one owned item to a different position, preserving its identity.</summary>
    /// <param name="oldIndex">The current zero-based item position.</param>
    /// <param name="newIndex">The destination zero-based item position.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current items.
    /// </exception>
    internal void MoveItem(int oldIndex, int newIndex)
    {
        VerifyMutable();

        if ((uint) oldIndex >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex), oldIndex,
                "The source index is outside the status bar.");
        }

        if ((uint) newIndex >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex), newIndex,
                "The destination index is outside the status bar.");
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        MoveItemControl(oldIndex, newIndex);
    }
}
