// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one run of inline-markup text flowing inside a <see cref="DocumentParagraph"/>
/// or <see cref="DocumentHeading"/>.</summary>
/// <remarks>
/// <see cref="Text"/> uses the same inline-markup syntax as <see cref="Display.Text.Content"/> - bold,
/// dim, italic, strikethrough, underline, reverse, and color tags such as
/// <c>"a &lt;b&gt;bold&lt;/b&gt; word"</c>. Markup styling applies at exact character boundaries, so a
/// tag may open and close in the middle of a word. Call <see cref="Display.Text.Escape"/> on text
/// that must render literally.
/// </remarks>
[PublicAPI]
public sealed class DocumentTextRun: DocumentInline
{
    /// <summary>Initializes an empty run.</summary>
    public DocumentTextRun() => Text = string.Empty;

    /// <summary>Initializes a run with non-null inline-markup text.</summary>
    /// <param name="text">The non-null markup string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public DocumentTextRun(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets or sets the non-null inline-markup text.</summary>
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
}
