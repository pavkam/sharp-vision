// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Identifies how one <see cref="SyntaxFoldRange"/> was detected.</summary>
[PublicAPI]
public enum SyntaxFoldRangeKind
{
    /// <summary>Detected from a matching pair of <c>beginRegion</c>/<c>endRegion</c> rules.</summary>
    Region,

    /// <summary>Detected purely from a decrease in leading-whitespace indentation.</summary>
    Indentation,
}
