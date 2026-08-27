// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Defines one content page with a header label inside a <see cref="TabControl"/>.</summary>
/// <remarks>
/// The owning <see cref="TabControl"/> renders a page's header through a private, text-only
/// <see cref="TabHeader"/> strip control; <see cref="HeaderText"/> is the only header surface this
/// page exposes.
/// </remarks>
[PublicAPI]
public sealed class TabItem: ContentControl
{
    /// <summary>Initializes an empty tab page with no header.</summary>
    public TabItem()
    {
    }

    /// <summary>Gets or sets the non-null header text shown in the owning tab strip.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    /// <exception cref="InvalidOperationException">The attached tab is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab is disposed.</exception>
    public string HeaderText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentException.ThrowIfContainsControls(value, nameof(value), "A tab header cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = string.Empty;

    /// <summary>Gets or sets whether this page may be closed by its owner.</summary>
    /// <exception cref="InvalidOperationException">The attached tab is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab is disposed.</exception>
    public bool IsClosable
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Clears retained content geometry when this page leaves presentation.</summary>
    internal void ClearPresentedContent()
    {
        if (Content is { } content)
        {
            ArrangeChild(content, default, ResolvedAxes.Both);
        }
    }

    /// <inheritdoc/>
    internal override bool TryHandleWidthRequest(Length value) =>
        FindAncestor<TabControl>()?.TryHandleItemWidthRequest(this, value) == true;

    /// <inheritdoc/>
    internal override bool TryHandleHeightRequest(Length value) =>
        FindAncestor<TabControl>()?.TryHandleItemHeightRequest(this, value) == true;

    /// <inheritdoc/>
    internal override bool TryHandleVisibilityRequest(Visibility value) =>
        FindAncestor<TabControl>()?.TryHandleItemVisibilityRequest(this, value) == true;

    /// <inheritdoc/>
    internal override void OnDirectDisposalRequested()
    {
        FindAncestor<TabControl>()?.RemoveItemForDisposal(this);
        base.OnDirectDisposalRequested();
    }
}
