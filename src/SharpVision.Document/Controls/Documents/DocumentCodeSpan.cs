// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one literal inline code span.</summary>
[PublicAPI]
public sealed class DocumentCodeSpan: DocumentInline
{
    /// <summary>Initializes an empty inline code span.</summary>
    public DocumentCodeSpan() => Text = string.Empty;

    /// <summary>Initializes an inline code span with literal text.</summary>
    /// <param name="text">The non-null literal code text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public DocumentCodeSpan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets or sets the non-null literal code text.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
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
}
