// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Carries the link a <see cref="Document.LinkClicked"/> notification is about.</summary>
/// <remarks>
/// The document raises this after the link's own <see cref="DocumentLink.Clicked"/> event, which lets
/// an application handle every link centrally without subscribing to each one.
/// </remarks>
[PublicAPI]
public sealed class DocumentLinkEventArgs: EventArgs
{
    /// <summary>Initializes the notification for one activated link.</summary>
    /// <param name="link">The non-null activated link.</param>
    /// <exception cref="ArgumentNullException"><paramref name="link"/> is null.</exception>
    public DocumentLinkEventArgs(DocumentLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        Link = link;
    }

    /// <summary>Gets the activated link.</summary>
    public DocumentLink Link { get; }
}
