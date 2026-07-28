// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty;

/// <summary>Identifies the lifecycle state of a Kitty clipboard transaction.</summary>
[PublicAPI]
public enum KittyTransactionState
{
    /// <summary>The request has been created but no response was accepted.</summary>
    Created,

    /// <summary>A read request was accepted with status OK.</summary>
    Accepted,

    /// <summary>One or more read data packets were accepted.</summary>
    Receiving,

    /// <summary>The transaction completed successfully.</summary>
    Completed,

    /// <summary>The terminal or protocol failed the transaction.</summary>
    Failed,

    /// <summary>The caller cancelled the transaction.</summary>
    Cancelled,

    /// <summary>The configured response deadline elapsed.</summary>
    TimedOut,

    /// <summary>The transaction was disposed.</summary>
    Disposed
}
