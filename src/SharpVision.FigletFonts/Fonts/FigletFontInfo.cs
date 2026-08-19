// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Exposes immutable provenance and audit metadata for one catalog font.</summary>
[PublicAPI]
public readonly record struct FigletFontInfo
{
    /// <summary>Initializes one internally validated manifest entry.</summary>
    /// <param name="name">The exact catalog lookup name.</param>
    /// <param name="file">The exact source filename.</param>
    /// <param name="format">The lower-case source format.</param>
    /// <param name="sha256">The lower-case source-byte SHA-256.</param>
    /// <param name="bytes">The non-negative source-byte count, zero for a caller-supplied
    /// in-memory font with no backing byte sequence to audit.</param>
    /// <param name="notice">The preserved embedded notice.</param>
    /// <param name="license">The SPDX license expression.</param>
    /// <param name="resource">The exact embedded resource name, or empty for caller-supplied fonts.</param>
    /// <param name="sourceRepository">The pinned source repository, or empty for caller-supplied fonts.</param>
    /// <param name="sourceCommit">The pinned source commit, or empty for caller-supplied fonts.</param>
    internal FigletFontInfo(
        string name,
        string file,
        string format,
        string sha256,
        int bytes,
        string notice,
        string license,
        string resource,
        string sourceRepository,
        string sourceCommit)
    {
        Name = name;
        File = file;
        Format = format;
        Sha256 = sha256;
        Bytes = bytes;
        Notice = notice;
        License = license;
        Resource = resource;
        SourceRepository = sourceRepository;
        SourceCommit = sourceCommit;
    }

    /// <summary>Gets the exact case-sensitive catalog name.</summary>
    public string Name { get; }

    /// <summary>Gets the exact source filename.</summary>
    public string File { get; }

    /// <summary>Gets the lower-case source format.</summary>
    public string Format { get; }

    /// <summary>Gets the lower-case source-byte SHA-256.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the source byte count.</summary>
    [NonNegativeValue]
    public int Bytes { get; }

    /// <summary>Gets the preserved FIGfont comment notice.</summary>
    public string Notice { get; }

    /// <summary>Gets the SPDX license expression or <c>unaudited</c> for caller-supplied fonts.</summary>
    public string License { get; }

    /// <summary>Gets the pinned source repository, or an empty string for caller-supplied fonts.</summary>
    public string SourceRepository { get; }

    /// <summary>Gets the pinned source commit, or an empty string for caller-supplied fonts.</summary>
    public string SourceCommit { get; }

    /// <summary>Gets the embedded resource name, or an empty string for caller-supplied fonts.</summary>
    internal string Resource { get; }
}
