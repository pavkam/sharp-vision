// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Surfaces;

/// <summary>Provides a cancellable pre-commit request to close one floating surface.</summary>
[PublicAPI]
public sealed class SurfaceCloseRequestedEventArgs: EventArgs
{
    /// <summary>Gets or sets whether the request is rejected by the application.</summary>
    public bool Cancel { get; set; }
}
