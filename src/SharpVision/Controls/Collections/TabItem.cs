// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Defines one content page with a header label inside a <see cref="TabControl"/>.</summary>
/// <remarks>
/// The owning <see cref="TabControl"/> renders a page's header through a private, text-only
/// <see cref="TabHeader"/> strip control, so only <see cref="HeaderedContentControl.HeaderText"/>
/// reaches the tab strip today; a caller-assigned rich <see cref="HeaderedContentControl.Header"/>
/// is retained on the page but is not yet shown in the strip.
/// </remarks>
[PublicAPI]
public sealed class TabItem: HeaderedContentControl
{
    /// <summary>Initializes an empty tab page with no header.</summary>
    public TabItem()
    {
    }

    /// <summary>Gets or sets whether this page may be closed by its owner.</summary>
    /// <exception cref="InvalidOperationException">The attached tab is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab is disposed.</exception>
    public bool Closable
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
}
