// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using Terminal.Clipboard;

/// <summary>Contains one terminal-initiated Kitty clipboard paste notification.</summary>
/// <remarks>
/// MIME types and password bytes are owned copies. The password is the terminal's one-time
/// credential for reading one of the advertised types without another permission prompt.
/// </remarks>
[PublicAPI]
public sealed class ClipboardPasteEventArgs: EventArgs
{
    /// <summary>Initializes one owned terminal-initiated paste notification.</summary>
    /// <param name="selection">The clipboard or primary selection that triggered the paste.</param>
    /// <param name="mimeTypes">The non-null advertised MIME types in terminal order.</param>
    /// <param name="password">The optional one-time password bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mimeTypes"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="selection"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="mimeTypes"/> contains a null, empty, or whitespace-bearing value.</exception>
    public ClipboardPasteEventArgs(
        Selection selection,
        IReadOnlyList<string> mimeTypes,
        ReadOnlyMemory<byte> password)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(
            selection,
            nameof(selection),
            "The clipboard selection is unknown.");
        ArgumentNullException.ThrowIfNull(mimeTypes);

        var ownedMimeTypes = new string[mimeTypes.Count];

        for (var index = 0; index < ownedMimeTypes.Length; index++)
        {
            var mimeType = mimeTypes[index];

            if (string.IsNullOrWhiteSpace(mimeType) || mimeType.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException(
                    "Every MIME type must be a non-empty token without whitespace.",
                    nameof(mimeTypes));
            }

            ownedMimeTypes[index] = mimeType;
        }

        Selection = selection;
        MimeTypes = Array.AsReadOnly(ownedMimeTypes);
        Password = password.ToArray();
    }

    /// <summary>Gets the clipboard or primary selection that triggered the paste.</summary>
    public Selection Selection { get; }

    /// <summary>Gets the immutable advertised MIME types in terminal order.</summary>
    public IReadOnlyList<string> MimeTypes { get; }

    /// <summary>Gets the owned one-time password bytes, or an empty value when none was supplied.</summary>
    public ReadOnlyMemory<byte> Password { get; }
}
