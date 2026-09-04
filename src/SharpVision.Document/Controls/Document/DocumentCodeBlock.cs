// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Represents one preformatted code block rendered verbatim.</summary>
/// <remarks>
/// <para>
/// <see cref="Text"/> is literal: markup is never parsed, so source containing angle brackets needs
/// no escaping. Line structure is preserved exactly - the block splits on line breaks and never word
/// wraps, because re-flowing code changes its meaning. A line longer than the content width is
/// clipped rather than wrapped, and the document's horizontal extent grows to match so the content
/// is reachable by scrolling.
/// </para>
/// <para>
/// Tabs expand to the next four-cell stop, matching the rest of the text stack.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class DocumentCodeBlock: DocumentBlock
{
    /// <summary>Initializes an empty code block.</summary>
    public DocumentCodeBlock() => Text = string.Empty;

    /// <summary>Initializes a code block with non-null literal text.</summary>
    /// <param name="text">The non-null literal text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public DocumentCodeBlock(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets or sets the non-null literal text.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public string Text
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Gets or sets the optional fenced-code language identifier.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public string Language
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
        }
    } = string.Empty;
}
