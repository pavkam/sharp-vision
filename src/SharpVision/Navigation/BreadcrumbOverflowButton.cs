// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Menus;

/// <summary>Presents the private one-cell overflow trigger and its owner-guarded vertical menu.</summary>
internal sealed class BreadcrumbOverflowButton: InputBase
{
    private readonly ContextMenu _contextMenu;
    private readonly List<BreadcrumbOverflowProjection> _projections = [];
    private long _collectionGeneration = -1;
    private long _overflowGeneration = -1;

    /// <summary>Initializes one trigger for an exact breadcrumb owner.</summary>
    internal BreadcrumbOverflowButton(Breadcrumb owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        EnableChromeAuthoring();
        EnableCaption();
        Text = "…";
        UseMnemonic = false;
        IsFocusable = false;
        IsTabStop = false;
        Border = ControlStyle.NoBorder;
        Shadow = ControlStyle.NoShadow;
        var menu = new Menu { Orientation = Orientation.Vertical, MinWidth = Length.Cells(1) };
        _contextMenu = new ContextMenu(menu);
        ContextMenu = _contextMenu;
    }

    /// <summary>Gets whether a projected menu can be opened.</summary>
    internal bool HasItems => _projections.Count > 0;

    /// <summary>Replaces projections when source identity or guard generations change.</summary>
    internal void SetSources(
        Breadcrumb owner,
        IReadOnlyList<BreadcrumbItem> sources,
        long collectionGeneration,
        long overflowGeneration)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(sources);

        if (_collectionGeneration == collectionGeneration &&
            _overflowGeneration == overflowGeneration &&
            sources.Count == _projections.Count)
        {
            var same = true;

            for (var index = 0; index < sources.Count; index++)
            {
                same &= string.Equals(_projections[index].Item.Text, sources[index].Text, StringComparison.Ordinal);
            }

            if (same)
            {
                return;
            }
        }

        _contextMenu.Close();
        _contextMenu.Items.Clear();

        foreach (var projection in _projections)
        {
            projection.Dispose();
        }

        _projections.Clear();
        _collectionGeneration = collectionGeneration;
        _overflowGeneration = overflowGeneration;

        foreach (var source in sources)
        {
            var projection = new BreadcrumbOverflowProjection(
                owner,
                source,
                collectionGeneration,
                overflowGeneration);
            _projections.Add(projection);
            _contextMenu.Items.Add(projection.Item);
        }
    }

    /// <summary>Opens the overflow menu immediately below this trigger.</summary>
    internal void Open()
    {
        if (HasItems && Bounds.Width > 0 && Bounds.Height > 0)
        {
            _contextMenu.Show(Bounds.Bottom, Bounds.X);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => ArrangeCaption(bounds);

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            foreach (var projection in _projections)
            {
                projection.Retire(disposeItem: false);
            }

            _projections.Clear();
        }

        base.OnUnavailable(reason);
    }
}
