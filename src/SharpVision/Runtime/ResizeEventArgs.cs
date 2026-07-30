// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Provides a committed terminal resize after root layout.</summary>
[PublicAPI]
public sealed class ResizeEventArgs: EventArgs
{
    /// <summary>Initializes one committed terminal resize.</summary>
    /// <param name="dimensions">The validated cell and optional pixel dimensions.</param>
    public ResizeEventArgs(Dimensions dimensions) => Dimensions = dimensions;

    /// <summary>Gets the committed cell and optional pixel dimensions.</summary>
    public Dimensions Dimensions { get; }
}
