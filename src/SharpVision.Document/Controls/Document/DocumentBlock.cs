// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Defines one block-level node of a <see cref="Document"/>: content that occupies whole
/// lines and stacks vertically against its siblings.</summary>
/// <remarks>
/// The block hierarchy is closed to this assembly and consists of <see cref="DocumentParagraph"/>,
/// <see cref="DocumentHeading"/>, <see cref="DocumentList"/>, <see cref="DocumentBlockQuote"/>,
/// <see cref="DocumentCodeBlock"/>, <see cref="DocumentSeparator"/>, <see cref="DocumentBlockControl"/>,
/// <see cref="DocumentCallout"/>, and <see cref="DocumentTable"/>. This mirrors HTML's own
/// block/inline split while allowing a form document to mount a real retained control where the
/// structure calls for one.
/// </remarks>
[PublicAPI]
public abstract class DocumentBlock: DocumentNode
{
    private protected DocumentBlock()
    {
    }
}
