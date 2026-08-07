// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Observes and optionally fails the Popup post-content-availability opening seam.</summary>
internal sealed class PopupContentAvailableProbe: Popup
{
    /// <summary>Gets the number of post-content-availability calls.</summary>
    internal int ContentAvailableCalls { get; private set; }

    /// <summary>Gets or sets the optional failure raised from the post-content-availability seam.</summary>
    internal Exception? ContentAvailableFailure { get; set; }

    /// <inheritdoc/>
    internal override void OnContentAvailable()
    {
        ContentAvailableCalls++;

        if (ContentAvailableFailure is { } failure)
        {
            throw failure;
        }
    }
}
