// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one thematic break drawn as a horizontal rule across the content width.</summary>
/// <remarks>
/// The rule occupies a single line and spans the full width available at its nesting level, so a
/// separator inside a block quote stops at the quote's own indent rather than the document's edge.
/// </remarks>
[PublicAPI]
public sealed class DocumentSeparator: DocumentBlock;
