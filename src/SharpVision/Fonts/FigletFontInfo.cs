// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

/// <summary>Exposes immutable provenance and audit metadata for one catalog font.</summary>
public readonly record struct FigletFontInfo
{
    /// <summary>Initializes one internally validated manifest entry.</summary>
    /// <param name="name">The exact catalog lookup name.</param>
    /// <param name="file">The exact archive entry name.</param>
    /// <param name="format">The lower-case source format.</param>
    /// <param name="sha256">The lower-case source-byte SHA-256.</param>
    /// <param name="bytes">The positive source-byte count.</param>
    /// <param name="notice">The preserved embedded notice.</param>
    /// <param name="license">The conservative audit classification.</param>
    internal FigletFontInfo(
        string name,
        string file,
        string format,
        string sha256,
        int bytes,
        string notice,
        string license)
    {
        Name = name;
        File = file;
        Format = format;
        Sha256 = sha256;
        Bytes = bytes;
        Notice = notice;
        License = license;
    }

    /// <summary>Gets the exact case-sensitive catalog name.</summary>
    public string Name { get; }

    /// <summary>Gets the exact embedded archive filename.</summary>
    public string File { get; }

    /// <summary>Gets the lower-case source format.</summary>
    public string Format { get; }

    /// <summary>Gets the lower-case source-byte SHA-256.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the compressed entry's original byte count.</summary>
    public int Bytes { get; }

    /// <summary>Gets the preserved FIGfont comment notice.</summary>
    public string Notice { get; }

    /// <summary>Gets the conservative audit classification.</summary>
    public string License { get; }
}
