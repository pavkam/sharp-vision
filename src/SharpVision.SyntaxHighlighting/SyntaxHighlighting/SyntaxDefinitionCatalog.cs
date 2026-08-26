// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

using System.Security.Cryptography;
using System.Text.Json;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>
/// Provides case-sensitive lazy access to a named collection of KDE syntax definitions, and
/// resolves cross-definition <c>IncludeRules</c> and context switches (such as
/// <c>Normal##JavaScript</c>) among the definitions it contains.
/// </summary>
/// <remarks>
/// <see cref="Default"/> is the embedded collection of 160 permissively licensed definitions
/// documented in the package's own <c>THIRD-PARTY-NOTICES.md</c>: 159 audited and redistributed
/// from upstream KDE/syntax-highlighting, plus one first-party definition (C#) original to
/// SharpVision itself, written from scratch because upstream's own C# definition carries no
/// stated license and cannot be redistributed.
/// <see cref="FromDirectory"/> builds a catalog from caller-supplied files with the same lookup,
/// parsing, and compilation surface, mirroring how upstream Kate itself picks up additional
/// syntax definitions from the local file system.
/// </remarks>
[PublicAPI]
public sealed class SyntaxDefinitionCatalog
{
    private const string _manifestResource = "SharpVision.SyntaxHighlighting.Resources.syntax.manifest.json";

    private readonly Dictionary<string, SyntaxDefinitionInfo> _entries;
    private readonly IReadOnlyList<string> _detectionNames;
    private readonly Func<SyntaxDefinitionCatalog, SyntaxDefinitionInfo, string> _readXml;
    private readonly Dictionary<string, Lazy<SyntaxDefinition>> _definitions = new(StringComparer.Ordinal);
    private readonly Lock _definitionGate = new();
    private readonly Lock _grammarGate = new();
    private readonly SyntaxGrammarCompiler _grammarCompiler;
    private int _embeddedResourceReadCount;
    private int _definitionParseCount;
    private int _grammarCompilationCount;

    private SyntaxDefinitionCatalog(
        Dictionary<string, SyntaxDefinitionInfo> entries,
        Func<SyntaxDefinitionCatalog, SyntaxDefinitionInfo, string> readXml,
        IReadOnlyDictionary<string, SyntaxDefinition>? definitions = null)
    {
        _entries = entries;
        _readXml = readXml;
        Names = Array.AsReadOnly(entries.Keys.Order(StringComparer.Ordinal).ToArray());
        _detectionNames = Array.AsReadOnly(
            Names.OrderByDescending(name => entries[name].Priority ?? 0).ToArray());
        _grammarCompiler = new SyntaxGrammarCompiler(ResolveDefinitionByName);

        if (definitions is not null)
        {
            foreach (var (name, definition) in definitions)
            {
                _definitions[name] = new Lazy<SyntaxDefinition>(() => definition);
            }
        }
    }

    /// <summary>Gets the process-wide immutable audited embedded catalog.</summary>
    public static SyntaxDefinitionCatalog Default { get; } = CreateEmbedded();

    /// <summary>Gets the immutable snapshot of exact case-sensitive names in ordinal order.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Gets how many embedded definition resource streams this instance opened. This seam exists
    /// for tests to prove that inventory access is lazy and that loading one definition reads only
    /// its own resource, mirroring <c>FigletCatalog.EmbeddedResourceReadCount</c>.
    /// </summary>
    internal int EmbeddedResourceReadCount => Volatile.Read(ref _embeddedResourceReadCount);

    /// <summary>Gets how many definition parses this catalog performed, so tests can prove parsed
    /// definitions are retained instead of reparsed after inventory construction.</summary>
    internal int DefinitionParseCount => Volatile.Read(ref _definitionParseCount);

    /// <summary>Gets how many grammar compilations this catalog started, so tests can prove one
    /// compilation owns each name even when concurrent callers race the first lookup.</summary>
    internal int GrammarCompilationCount => Volatile.Read(ref _grammarCompilationCount);

    /// <summary>Builds a catalog from every <c>.xml</c> file directly inside one directory.</summary>
    /// <param name="path">The non-null directory to scan; not searched recursively.</param>
    /// <returns>A new catalog over the discovered files, keyed by each file's own declared language name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The directory contains no <c>.xml</c> files, or two files declare the same language name.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="FormatException">A file is not a well-formed syntax definition.</exception>
    [MustUseReturnValue]
    public static SyntaxDefinitionCatalog FromDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var entries = new Dictionary<string, SyntaxDefinitionInfo>(StringComparer.Ordinal);
        var definitions = new Dictionary<string, SyntaxDefinition>(StringComparer.Ordinal);
        var catalogParseCount = 0;

        foreach (var file in Directory.EnumerateFiles(path, "*.xml"))
        {
            using var stream = File.OpenRead(file);
            var definition = SyntaxDefinitionReader.Read(stream);
            _ = Interlocked.Increment(ref catalogParseCount);

            if (!entries.TryAdd(definition.Name, ToInfo(definition, Path.GetFileName(file))))
            {
                throw new ArgumentException($"More than one file declares the language name '{definition.Name}'.", nameof(path));
            }

            definitions[definition.Name] = definition;
        }

        return entries.Count > 0
            ? CreateDirectoryCatalog(entries, definitions, catalogParseCount)
            : throw new ArgumentException("The directory contains no .xml syntax-definition files.", nameof(path));
    }

    /// <summary>Gets preserved catalog and provenance metadata for one exact name.</summary>
    /// <param name="name">The non-null exact case-sensitive language name.</param>
    /// <returns>The immutable metadata record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The exact name is absent.</exception>
    [Pure]
    public SyntaxDefinitionInfo GetInfo(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries.TryGetValue(name, out var value)
            ? value
            : throw new KeyNotFoundException($"The syntax-definition catalog does not contain '{name}'.");
    }

    /// <summary>Finds the highest-priority catalog name whose extension pattern matches a file name.</summary>
    /// <param name="fileName">The non-null file name (a full path is accepted; only its final segment is matched).</param>
    /// <returns>The greatest-priority match, using ordinal name order to break ties, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is null.</exception>
    [Pure]
    public string? FindNameForFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var name = Path.GetFileName(fileName);

        foreach (var candidate in _detectionNames)
        {
            var info = _entries[candidate];

            foreach (var pattern in info.Extensions)
            {
                if (MatchesGlob(pattern, name))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>Parses and validates one named definition.</summary>
    /// <param name="name">The non-null exact case-sensitive language name.</param>
    /// <returns>The parsed definition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The exact name is absent.</exception>
    /// <exception cref="InvalidDataException">An embedded resource disagrees with its recorded provenance.</exception>
    /// <exception cref="FormatException">The definition is not well-formed.</exception>
    /// <remarks>
    /// The first call for a given name reads and parses the definition; every later call for that
    /// same name on this instance returns the already-parsed, immutable result instead of
    /// repeating that work.
    /// </remarks>
    public SyntaxDefinition GetDefinition(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var info = GetInfo(name);
        Lazy<SyntaxDefinition> lazy;

        lock (_definitionGate)
        {
            if (!_definitions.TryGetValue(name, out lazy!))
            {
                lazy = new Lazy<SyntaxDefinition>(
                    () =>
                    {
                        _ = Interlocked.Increment(ref _definitionParseCount);
                        return SyntaxDefinitionReader.Read(_readXml(this, info));
                    },
                    LazyThreadSafetyMode.ExecutionAndPublication);
                _definitions.Add(name, lazy);
            }
        }

        try
        {
            return lazy.Value;
        }
        catch
        {
            lock (_definitionGate)
            {
                if (_definitions.TryGetValue(name, out var current) && ReferenceEquals(current, lazy))
                {
                    _ = _definitions.Remove(name);
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Compiles one named definition into a grammar, resolving any cross-definition
    /// <c>IncludeRules</c> or context switch against every other definition in this catalog.
    /// </summary>
    /// <param name="name">The non-null exact case-sensitive language name.</param>
    /// <returns>The compiled grammar.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The exact name is absent.</exception>
    /// <exception cref="InvalidDataException">An embedded resource disagrees with its recorded provenance.</exception>
    /// <exception cref="FormatException">The definition is not well-formed.</exception>
    public SyntaxGrammar GetGrammar(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        _ = GetInfo(name);

        lock (_grammarGate)
        {
            if (_grammarCompiler.TryGetGrammar(name, out var existing))
            {
                return existing;
            }

            _ = Interlocked.Increment(ref _grammarCompilationCount);
            return _grammarCompiler.Compile(GetDefinition(name));
        }
    }

    private SyntaxDefinition? ResolveDefinitionByName(string name) => _entries.ContainsKey(name) ? GetDefinition(name) : null;

    /// <summary>Creates a fresh embedded catalog so concurrency tests can exercise an unwarmed
    /// first lookup independently of the process-wide <see cref="Default"/> instance.</summary>
    /// <returns>A fresh catalog over the audited embedded definitions.</returns>
    internal static SyntaxDefinitionCatalog CreateEmbedded()
    {
        var assembly = typeof(SyntaxDefinitionCatalog).Assembly;
        using var manifestStream = assembly.GetManifestResourceStream(_manifestResource) ??
                                    throw new InvalidDataException($"Embedded resource '{_manifestResource}' is missing.");

        var entries = ParseManifest(manifestStream);

        return entries.Count != 160
            ? throw new InvalidDataException("The embedded syntax-definition manifest must contain exactly 160 definitions.")
            : new SyntaxDefinitionCatalog(entries, ReadEmbeddedXml);
    }

    private static SyntaxDefinitionCatalog CreateDirectoryCatalog(
        Dictionary<string, SyntaxDefinitionInfo> entries,
        Dictionary<string, SyntaxDefinition> definitions,
        int parseCount)
    {
        var catalog = new SyntaxDefinitionCatalog(
            entries,
            static (_, _) => throw new UnreachableException("Directory definitions are parsed during catalog construction."),
            definitions)
        {
            _definitionParseCount = parseCount,
        };
        return catalog;
    }

    private static string ReadEmbeddedXml(SyntaxDefinitionCatalog self, SyntaxDefinitionInfo info)
    {
        var assembly = typeof(SyntaxDefinitionCatalog).Assembly;
        var resourceName = $"SharpVision.SyntaxHighlighting.Resources.Syntax.{info.File}";

        using var stream = assembly.GetManifestResourceStream(resourceName) ??
                            throw new InvalidDataException($"Embedded syntax-definition resource '{resourceName}' is missing.");

        _ = Interlocked.Increment(ref self._embeddedResourceReadCount);

        if (stream.Length != info.Bytes)
        {
            throw new InvalidDataException($"The embedded syntax-definition length for '{info.File}' is invalid.");
        }

        var bytes = new byte[checked((int) stream.Length)];
        stream.ReadExactly(bytes);

        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (!hash.Equals(info.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The embedded syntax-definition hash for '{info.File}' does not match its audit.");
        }

        // Several upstream files carry a UTF-8 byte-order mark. Encoding.UTF8.GetString does not
        // strip it, which would otherwise leave a stray U+FEFF before "<?xml" and fail parsing.
        var preamble = Encoding.UTF8.Preamble;
        var content = bytes.AsSpan().StartsWith(preamble) ? bytes.AsSpan(preamble.Length) : bytes.AsSpan();

        return Encoding.UTF8.GetString(content);
    }

    private static SyntaxDefinitionInfo ToInfo(SyntaxDefinition definition, string file) =>
        new(
            definition.Name,
            file,
            definition.Section,
            definition.Extensions,
            definition.MimeTypes,
            definition.AlternativeNames,
            definition.Author,
            definition.Priority,
            license: string.Empty,
            sha256: string.Empty,
            bytes: 0,
            sourceRepository: string.Empty,
            sourceCommit: string.Empty);

    private static Dictionary<string, SyntaxDefinitionInfo> ParseManifest(Stream manifestStream)
    {
        using var document = JsonDocument.Parse(manifestStream);
        var root = document.RootElement;

        if (root.GetProperty("schema").GetInt32() != 1)
        {
            throw new InvalidDataException("The embedded syntax-definition manifest schema is not supported.");
        }

        var count = root.GetProperty("count").GetInt32();
        var result = new Dictionary<string, SyntaxDefinitionInfo>(StringComparer.Ordinal);

        foreach (var element in root.GetProperty("definitions").EnumerateArray())
        {
            var info = new SyntaxDefinitionInfo(
                element.GetProperty("name").GetString()!,
                element.GetProperty("file").GetString()!,
                element.GetProperty("section").GetString()!,
                SyntaxDefinitionReader.SplitList(element.GetProperty("extensions").GetString()),
                SyntaxDefinitionReader.SplitList(element.GetProperty("mimetype").GetString()),
                SyntaxDefinitionReader.SplitList(element.GetProperty("alternativeNames").GetString()),
                element.GetProperty("author").GetString()!,
                element.TryGetProperty("priority", out var priority) && priority.ValueKind == JsonValueKind.Number
                    ? priority.GetInt32()
                    : null,
                element.GetProperty("license").GetString()!,
                element.GetProperty("sha256").GetString()!,
                element.GetProperty("bytes").GetInt32(),
                element.GetProperty("sourceRepository").GetString()!,
                element.GetProperty("sourceCommit").GetString()!);

            // sourceCommit is empty exactly for a first-party definition original to this
            // repository rather than redistributed from sourceRepository's own commit history -
            // never partially populated: either a real 40-character pinned commit hash, or empty.
            if (string.IsNullOrWhiteSpace(info.Name) ||
                string.IsNullOrWhiteSpace(info.File) ||
                info.File.Contains('/') ||
                info.File.Contains('\\') ||
                info.Bytes <= 0 ||
                info.Sha256.Length != 64 ||
                string.IsNullOrWhiteSpace(info.SourceRepository) ||
                (info.SourceCommit.Length != 40 && info.SourceCommit.Length != 0) ||
                !result.TryAdd(info.Name, info))
            {
                throw new InvalidDataException("The embedded syntax-definition manifest contains an invalid entry.");
            }
        }

        return count == result.Count
            ? result
            : throw new InvalidDataException("The embedded syntax-definition manifest count does not match its entries.");
    }

    private static bool MatchesGlob(string pattern, string fileName)
    {
        // KDE extension globs use only '*' and '?' wildcards, never full regular expressions.
        // The resulting pattern is built entirely from Regex.Escape(pattern) plus only ".*"/"."
        // substitutions, so it can never contain attacker-controlled metacharacters or nested
        // quantifiers capable of catastrophic backtracking - a timeout is not a correctness
        // requirement here the way it is for a third-party RegExpr/emptyLine pattern. It is added
        // anyway for consistency with every other Regex this assembly constructs.
        var regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));
    }
}
