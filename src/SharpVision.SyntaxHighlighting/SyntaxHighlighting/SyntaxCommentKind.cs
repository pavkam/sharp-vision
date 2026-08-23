// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Distinguishes the two comment shapes a syntax definition can declare.</summary>
[PublicAPI]
public enum SyntaxCommentKind
{
    /// <summary>A comment that a start marker alone begins and the end of line ends.</summary>
    SingleLine,

    /// <summary>A comment that an explicit start marker begins and end marker ends.</summary>
    MultiLine,
}
