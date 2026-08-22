// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Identifies what one <see cref="DocumentFlowToken"/> contributes to a line.</summary>
internal enum DocumentFlowTokenKind
{
    /// <summary>Non-whitespace content that must not be split across lines.</summary>
    Word,

    /// <summary>Whitespace, which may be dropped when a wrap lands on it.</summary>
    Space,

    /// <summary>An atomic retained control.</summary>
    Control,

    /// <summary>A hard break that ends the current line wherever it appears.</summary>
    Break
}
