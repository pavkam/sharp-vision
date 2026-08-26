// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Exposes catalog and provenance metadata for one syntax definition, without parsing it.
/// The default value is an empty metadata record.</summary>
[PublicAPI]
public readonly record struct SyntaxDefinitionInfo
{
#pragma warning disable IDE0032 // Default structs need null-coalescing getters over nullable backing storage.
    private readonly string? _name;
    private readonly string? _file;
    private readonly string? _section;
    private readonly IReadOnlyList<string>? _extensions;
    private readonly IReadOnlyList<string>? _mimeTypes;
    private readonly IReadOnlyList<string>? _alternativeNames;
    private readonly string? _author;
    private readonly string? _license;
    private readonly string? _sha256;
    private readonly string? _sourceRepository;
    private readonly string? _sourceCommit;
#pragma warning restore IDE0032

    /// <summary>Initializes one internally validated catalog entry.</summary>
    /// <param name="name">The exact catalog lookup name.</param>
    /// <param name="file">The exact source filename.</param>
    /// <param name="section">The logical grouping, such as <c>Sources</c> or <c>Markup</c>.</param>
    /// <param name="extensions">The file-glob patterns identifying documents of this language.</param>
    /// <param name="mimeTypes">The MIME types identifying documents of this language.</param>
    /// <param name="alternativeNames">Additional names this definition also matches under.</param>
    /// <param name="author">The declared author.</param>
    /// <param name="priority">The relative file-detection priority, or null when unspecified.</param>
    /// <param name="license">The declared, redistribution-audited license, or empty for a caller-supplied definition.</param>
    /// <param name="sha256">The lower-case source-byte SHA-256, or empty for a caller-supplied definition.</param>
    /// <param name="bytes">The non-negative source-byte count.</param>
    /// <param name="sourceRepository">
    /// The repository a caller can inspect for provenance: the pinned upstream repository for a
    /// redistributed embedded definition, this project's own repository for a first-party embedded
    /// definition original to SharpVision, or empty for a caller-supplied definition.
    /// </param>
    /// <param name="sourceCommit">
    /// The pinned upstream commit a redistributed embedded definition's bytes were copied from, or
    /// empty for a first-party embedded definition (which has no external commit to pin) or a
    /// caller-supplied definition.
    /// </param>
    internal SyntaxDefinitionInfo(
        string name,
        string file,
        string section,
        IReadOnlyList<string> extensions,
        IReadOnlyList<string> mimeTypes,
        IReadOnlyList<string> alternativeNames,
        string author,
        int? priority,
        string license,
        string sha256,
        int bytes,
        string sourceRepository,
        string sourceCommit)
    {
        _name = name;
        _file = file;
        _section = section;
        _extensions = new SyntaxReadOnlyList<string>(extensions);
        _mimeTypes = new SyntaxReadOnlyList<string>(mimeTypes);
        _alternativeNames = new SyntaxReadOnlyList<string>(alternativeNames);
        _author = author;
        Priority = priority;
        _license = license;
        _sha256 = sha256;
        Bytes = bytes;
        _sourceRepository = sourceRepository;
        _sourceCommit = sourceCommit;
    }

    /// <summary>Gets the exact case-sensitive catalog name.</summary>
    public string Name => _name ?? string.Empty;

    /// <summary>Gets the exact source filename.</summary>
    public string File => _file ?? string.Empty;

    /// <summary>Gets the logical grouping, such as <c>Sources</c> or <c>Markup</c>.</summary>
    public string Section => _section ?? string.Empty;

    /// <summary>Gets the file-glob patterns identifying documents of this language.</summary>
    public IReadOnlyList<string> Extensions => _extensions ?? SyntaxReadOnlyList<string>.Empty;

    /// <summary>Gets the MIME types identifying documents of this language.</summary>
    public IReadOnlyList<string> MimeTypes => _mimeTypes ?? SyntaxReadOnlyList<string>.Empty;

    /// <summary>Gets additional names this definition also matches under.</summary>
    public IReadOnlyList<string> AlternativeNames => _alternativeNames ?? SyntaxReadOnlyList<string>.Empty;

    /// <summary>Gets the declared author.</summary>
    public string Author => _author ?? string.Empty;

    /// <summary>Gets the relative file-detection priority, or null when unspecified. This internal
    /// metadata keeps catalog selection correct without expanding the public provenance surface.</summary>
    internal int? Priority { get; }

    /// <summary>Gets the declared, redistribution-audited license, or an empty string for a caller-supplied definition.</summary>
    public string License => _license ?? string.Empty;

    /// <summary>Gets the lower-case source-byte SHA-256, or an empty string for a caller-supplied definition.</summary>
    public string Sha256 => _sha256 ?? string.Empty;

    /// <summary>Gets the source byte count.</summary>
    public int Bytes { get; }

    /// <summary>
    /// Gets the repository a caller can inspect for provenance: the pinned upstream repository for
    /// a redistributed embedded definition, this project's own repository for a first-party
    /// embedded definition original to SharpVision, or an empty string for a caller-supplied
    /// definition.
    /// </summary>
    public string SourceRepository => _sourceRepository ?? string.Empty;

    /// <summary>
    /// Gets the pinned upstream commit a redistributed embedded definition's bytes were copied
    /// from, or an empty string for a first-party embedded definition (which has no external
    /// commit to pin) or a caller-supplied definition.
    /// </summary>
    public string SourceCommit => _sourceCommit ?? string.Empty;
}
