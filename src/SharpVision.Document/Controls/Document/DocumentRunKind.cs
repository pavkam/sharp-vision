// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Identifies how one laid-out run produces cells.</summary>
internal enum DocumentRunKind
{
    /// <summary>A slice of a parsed run's display text, painted with its own markup spans.</summary>
    Text,

    /// <summary>One glyph repeated across the run's cells, used for rules, quote bars, and the blank
    /// advance a tab expands to.</summary>
    Repeat,

    /// <summary>A retained control positioned by the document presenter.</summary>
    Control
}
