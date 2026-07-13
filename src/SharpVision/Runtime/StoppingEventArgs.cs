// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Provides cancellation for an explicit application stop request.</summary>
public sealed class StoppingEventArgs: EventArgs
{
    /// <summary>Gets or sets whether an explicit stop should be cancelled.</summary>
    public bool Cancel { get; set; }
}
