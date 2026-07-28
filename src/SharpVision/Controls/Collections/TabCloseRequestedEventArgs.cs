// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Provides a cancellable request to close one closeable tab page.</summary>
public sealed class TabCloseRequestedEventArgs: EventArgs
{
    /// <summary>Initializes a close request for the supplied tab.</summary>
    /// <param name="item">The non-null tab page being requested for closure.</param>
    public TabCloseRequestedEventArgs(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
    }

    /// <summary>Gets the tab page being requested for closure.</summary>
    public TabItem Item { get; }

    /// <summary>Gets or sets whether the request is rejected by the application.</summary>
    public bool Cancel { get; set; }
}
