// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>
/// Provides case-sensitive lazy access to a named collection of KDE syntax definitions, and
/// resolves cross-definition <c>IncludeRules</c> and context switches (such as
/// <c>Normal##JavaScript</c>) among the definitions it contains.
/// </summary>
/// <remarks>
/// <see cref="Default"/> is the audited embedded collection of 159 permissively licensed
/// definitions documented in the package's own <c>THIRD-PARTY-NOTICES.md</c>.
/// <see cref="FromDirectory"/> builds a catalog from caller-supplied files with the same lookup,
/// parsing, and compilation surface, mirroring how upstream Kate itself picks up additional
/// syntax definitions from the local file system.
/// </remarks>
[PublicAPI]
public sealed class SyntaxDefinitionCatalog
{
    private const string _manifestResource = "SharpVision.SyntaxHighlighting.Resources.syntax.manifest.json";

    private readonly Dictionary<string, SyntaxDefinitionInfo> _entries;
    private readonly Func<SyntaxDefinitionCatalog, SyntaxDefinitionInfo, string> _readXml;
    private readonly ConcurrentDictionary<string, SyntaxDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SyntaxGrammar> _grammars = new(StringComparer.Ordinal);
    private int _embeddedResourceReadCount;

    private SyntaxDefinitionCatalog(
        Dictionary<string, SyntaxDefinitionInfo> entries,
        Func<SyntaxDefinitionCatalog, SyntaxDefinitionInfo, string> readXml)
    {
        _entries = entries;
        _readXml = readXml;
        Names = Array.AsReadOnly(entries.Keys.Order(StringComparer.Ordinal).ToArray());
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
        var xmlByName = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(path, "*.xml"))
        {
            var xml = File.ReadAllText(file);
            var definition = SyntaxDefinitionReader.Read(xml);

            if (!entries.TryAdd(definition.Name, ToInfo(definition, Path.GetFileName(file))))
            {
                throw new ArgumentException($"More than one file declares the language name '{definition.Name}'.", nameof(path));
            }

            xmlByName[definition.Name] = xml;
        }

        return entries.Count > 0
            ? new SyntaxDefinitionCatalog(entries, (_, info) => xmlByName[info.Name])
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

    /// <summary>Finds the first catalog name whose extension pattern matches a file name.</summary>
    /// <param name="fileName">The non-null file name (a full path is accepted; only its final segment is matched).</param>
    /// <returns>The first matching name in ordinal order, or null when nothing matches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is null.</exception>
    [Pure]
    public string? FindNameForFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var name = Path.GetFileName(fileName);

        foreach (var candidate in Names)
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

        return _definitions.GetOrAdd(
            name,
            static (key, self) =>
            {
                var info = self.GetInfo(key);
                return SyntaxDefinitionReader.Read(self._readXml(self, info));
            },
            this);
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

        return _grammars.GetOrAdd(
            name,
            static (key, self) => SyntaxGrammar.Compile(self.GetDefinition(key), self.ResolveDefinitionByName),
            this);
    }

    private SyntaxDefinition? ResolveDefinitionByName(string name) => _entries.ContainsKey(name) ? GetDefinition(name) : null;

    private static SyntaxDefinitionCatalog CreateEmbedded()
    {
        var assembly = typeof(SyntaxDefinitionCatalog).Assembly;
        using var manifestStream = assembly.GetManifestResourceStream(_manifestResource) ??
                                    throw new InvalidDataException($"Embedded resource '{_manifestResource}' is missing.");

        var entries = ParseManifest(manifestStream);

        return entries.Count != 159
            ? throw new InvalidDataException("The embedded syntax-definition manifest must contain exactly 159 definitions.")
            : new SyntaxDefinitionCatalog(entries, ReadEmbeddedXml);
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
                element.GetProperty("license").GetString()!,
                element.GetProperty("sha256").GetString()!,
                element.GetProperty("bytes").GetInt32(),
                element.GetProperty("sourceRepository").GetString()!,
                element.GetProperty("sourceCommit").GetString()!);

            if (string.IsNullOrWhiteSpace(info.Name) ||
                string.IsNullOrWhiteSpace(info.File) ||
                info.File.Contains('/') ||
                info.File.Contains('\\') ||
                info.Bytes <= 0 ||
                info.Sha256.Length != 64 ||
                string.IsNullOrWhiteSpace(info.SourceRepository) ||
                info.SourceCommit.Length != 40 ||
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
        var regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
