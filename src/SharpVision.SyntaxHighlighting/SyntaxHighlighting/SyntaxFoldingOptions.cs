// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Describes a syntax definition's <c>&lt;general&gt;/&lt;folding&gt;</c> declaration.</summary>
[PublicAPI]
public readonly record struct SyntaxFoldingOptions
{
    /// <summary>Initializes the folding declaration.</summary>
    /// <param name="indentationSensitive">
    /// Whether every context not itself marked <c>noIndentationBasedFolding</c> also produces
    /// indentation-based fold ranges alongside any explicit <c>beginRegion</c>/<c>endRegion</c>
    /// ranges.
    /// </param>
    internal SyntaxFoldingOptions(bool indentationSensitive) => IndentationSensitive = indentationSensitive;

    /// <summary>
    /// Gets whether indentation-based folding is enabled for this definition's contexts by
    /// default.
    /// </summary>
    public bool IndentationSensitive { get; }
}
