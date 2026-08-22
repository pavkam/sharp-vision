// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents.Markdown;

/// <summary>Configures optional Markdown syntax without changing CommonMark defaults.</summary>
[PublicAPI]
public sealed class MarkdownOptions
{
    /// <summary>Gets or sets the enabled extension flags.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown bits.</exception>
    public MarkdownExtension Extensions
    {
        get;
        set
        {
            if ((value & ~MarkdownExtension.All) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The Markdown extension set is unknown.");
            }

            field = value;
        }
    }
}
