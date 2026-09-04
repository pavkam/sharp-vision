// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Defines an inline semantic element that owns an ordered nested inline flow.</summary>
[PublicAPI]
public abstract class DocumentInlineContainer: DocumentInline
{
    /// <summary>Initializes an empty semantic inline container.</summary>
    protected DocumentInlineContainer() => Inlines = new DocumentInlineCollection(this);

    /// <summary>Gets the exclusively owned inline children in source order.</summary>
    public DocumentInlineCollection Inlines { get; }
}
