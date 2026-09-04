// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Defines one inline node of a <see cref="Document"/>: content that flows within a line and
/// wraps with its neighbors.</summary>
/// <remarks>
/// The inline hierarchy is closed to this assembly and includes semantic containers, text, links,
/// breaks, code spans, and <see cref="DocumentInlineControl"/>. Inline content appears inside a
/// <see cref="DocumentParagraph"/> or <see cref="DocumentHeading"/>; a line can wrap between two
/// inlines as readily as within one, so adjacent runs read as one continuous stretch of text.
/// </remarks>
[PublicAPI]
public abstract class DocumentInline: DocumentNode
{
    private protected DocumentInline()
    {
    }
}
