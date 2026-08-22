// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Forces a line break inside a <see cref="DocumentParagraph"/> or
/// <see cref="DocumentHeading"/>, independent of word wrapping.</summary>
/// <remarks>
/// The break ends the current line wherever it appears. Two consecutive breaks therefore leave one
/// blank line, and a trailing break leaves a blank final line.
/// </remarks>
[PublicAPI]
public sealed class DocumentLineBreak: DocumentInline;
