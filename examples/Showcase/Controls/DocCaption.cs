// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

/// <summary>Helpers for authored showcase captions and mnemonic markers.</summary>
internal static class DocCaption
{
    /// <summary>Removes mnemonic markers while preserving escaped literal ampersands.</summary>
    /// <param name="caption">The non-null authored access-text caption.</param>
    /// <returns>The visible caption text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="caption"/> is null.</exception>
    internal static string PlainCaption(string caption)
    {
        ArgumentNullException.ThrowIfNull(caption);
        var result = new StringBuilder(caption.Length);

        for (var index = 0; index < caption.Length; index++)
        {
            if (caption[index] != '&')
            {
                _ = result.Append(caption[index]);
                continue;
            }

            if (index + 1 >= caption.Length)
            {
                _ = result.Append('&');
                continue;
            }

            if (index + 1 < caption.Length && caption[index + 1] == '&')
            {
                _ = result.Append('&');
                index++;
            }
        }

        return result.ToString();
    }
}
