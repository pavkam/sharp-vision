// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Defines one content page with a header label inside a <see cref="TabControl"/>.</summary>
[PublicAPI]
public sealed class TabItem: ContentControl
{
    /// <summary>Initializes an empty tab page with no header.</summary>
    public TabItem()
    {
    }

    /// <summary>Gets or sets the non-null header label shown in the tab bar.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains terminal control characters.</exception>
    /// <exception cref="InvalidOperationException">The attached tab is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.ThrowIfContainsControls(
                nameof(value),
                "A tab header cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
}
