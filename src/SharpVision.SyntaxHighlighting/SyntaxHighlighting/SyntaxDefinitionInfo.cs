// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Exposes catalog and provenance metadata for one syntax definition, without parsing it.</summary>
[PublicAPI]
public readonly record struct SyntaxDefinitionInfo
{
    /// <summary>Initializes one internally validated catalog entry.</summary>
    /// <param name="name">The exact catalog lookup name.</param>
    /// <param name="file">The exact source filename.</param>
    /// <param name="section">The logical grouping, such as <c>Sources</c> or <c>Markup</c>.</param>
    /// <param name="extensions">The file-glob patterns identifying documents of this language.</param>
    /// <param name="mimeTypes">The MIME types identifying documents of this language.</param>
    /// <param name="alternativeNames">Additional names this definition also matches under.</param>
    /// <param name="author">The declared author.</param>
    /// <param name="license">The declared, redistribution-audited license, or empty for a caller-supplied definition.</param>
    /// <param name="sha256">The lower-case source-byte SHA-256, or empty for a caller-supplied definition.</param>
    /// <param name="bytes">The non-negative source-byte count.</param>
    /// <param name="sourceRepository">The pinned source repository, or empty for a caller-supplied definition.</param>
    /// <param name="sourceCommit">The pinned source commit, or empty for a caller-supplied definition.</param>
    internal SyntaxDefinitionInfo(
        string name,
        string file,
        string section,
        IReadOnlyList<string> extensions,
        IReadOnlyList<string> mimeTypes,
        IReadOnlyList<string> alternativeNames,
        string author,
        string license,
        string sha256,
        int bytes,
        string sourceRepository,
        string sourceCommit)
    {
        Name = name;
        File = file;
        Section = section;
        Extensions = extensions;
        MimeTypes = mimeTypes;
        AlternativeNames = alternativeNames;
        Author = author;
        License = license;
        Sha256 = sha256;
        Bytes = bytes;
        SourceRepository = sourceRepository;
        SourceCommit = sourceCommit;
    }

    /// <summary>Gets the exact case-sensitive catalog name.</summary>
    public string Name { get; }

    /// <summary>Gets the exact source filename.</summary>
    public string File { get; }

    /// <summary>Gets the logical grouping, such as <c>Sources</c> or <c>Markup</c>.</summary>
    public string Section { get; }

    /// <summary>Gets the file-glob patterns identifying documents of this language.</summary>
    public IReadOnlyList<string> Extensions { get; }

    /// <summary>Gets the MIME types identifying documents of this language.</summary>
    public IReadOnlyList<string> MimeTypes { get; }

    /// <summary>Gets additional names this definition also matches under.</summary>
    public IReadOnlyList<string> AlternativeNames { get; }

    /// <summary>Gets the declared author.</summary>
    public string Author { get; }

    /// <summary>Gets the declared, redistribution-audited license, or an empty string for a caller-supplied definition.</summary>
    public string License { get; }

    /// <summary>Gets the lower-case source-byte SHA-256, or an empty string for a caller-supplied definition.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the source byte count.</summary>
    public int Bytes { get; }

    /// <summary>Gets the pinned source repository, or an empty string for a caller-supplied definition.</summary>
    public string SourceRepository { get; }

    /// <summary>Gets the pinned source commit, or an empty string for a caller-supplied definition.</summary>
    public string SourceCommit { get; }
}
