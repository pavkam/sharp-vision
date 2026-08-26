// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

/// <summary>Describes popup content whose direct public disposal has begun.</summary>
internal sealed class OwnedContentDisposalEventArgs: EventArgs
{
    /// <summary>Initializes the notification for one owned content control.</summary>
    /// <param name="content">The content whose disposal was requested.</param>
    internal OwnedContentDisposalEventArgs(ControlBase content) => Content = content;

    /// <summary>Gets the content whose disposal was requested.</summary>
    internal ControlBase Content { get; }
}
