// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>Provides case-sensitive lazy access to a named collection of FIGfont sources.</summary>
/// <remarks>
/// <see cref="Default"/> is the audited embedded BSD/MIT collection. <see cref="FromDirectory"/>,
/// <see cref="FromZip"/>, and <see cref="FromFonts"/> build a catalog from caller-supplied fonts
/// with the same lookup and rendering surface.
/// </remarks>
[PublicAPI]
public sealed class FigletCatalog
{
    private const string _manifestResource = "SharpVision.FigletFonts.Resources.fonts.manifest.json";
    private const string _unauditedLicense = "unaudited";

    private readonly Dictionary<string, FigletFontInfo> _entries;
    private readonly Dictionary<string, Func<FigletCatalog, FigletLimits, FigletFont>> _loaders;
    private readonly Dictionary<(string Name, FigletLimits Limits), Lazy<FigletFont>> _cache = new();
    private readonly Lock _cacheGate = new();
    private int _embeddedResourceReadCount;

    private FigletCatalog(
        Dictionary<string, FigletFontInfo> entries,
        Dictionary<string, Func<FigletCatalog, FigletLimits, FigletFont>> loaders)
    {
        _entries = entries;
        _loaders = loaders;
        Names = Array.AsReadOnly(entries.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Gets the process-wide immutable audited embedded catalog.</summary>
    public static FigletCatalog Default { get; } = CreateEmbedded();

    /// <summary>Gets the immutable snapshot of exact case-sensitive names in ordinal order.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Gets how many embedded font resource streams this instance opened. This seam exists for
    /// tests to prove that inventory access is lazy and a load reads only its selected resource.
    /// </summary>
    internal int EmbeddedResourceReadCount => Volatile.Read(ref _embeddedResourceReadCount);

    /// <summary>Builds a catalog from every <c>.flf</c>/<c>.tlf</c> file directly inside one directory.</summary>
    /// <param name="path">The non-null directory to scan; not searched recursively.</param>
    /// <param name="limits">The optional default finite limits applied when <see cref="Load(string)"/> omits them.</param>
    /// <returns>A new immutable catalog over the discovered files.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The directory contains no font files, or two files resolve to the same name.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="InvalidDataException">A file exceeds the configured input byte limit.</exception>
    [MustUseReturnValue]
    public static FigletCatalog FromDirectory(string path, FigletLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        var effectiveLimits = limits ?? FigletLimits.Default;
        var entries = new Dictionary<string, FigletFontInfo>(StringComparer.Ordinal);
        var loaders = new Dictionary<string, Func<FigletCatalog, FigletLimits, FigletFont>>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(path))
        {
            var format = FontFormatOf(file);

            if (format is null)
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(file);
            var bytes = File.ReadAllBytes(file);

            if (bytes.Length > effectiveLimits.MaxInputBytes)
            {
                throw new InvalidDataException($"Font file '{file}' exceeds the configured input byte limit.");
            }

            AddUnauditedEntry(entries, loaders, name, Path.GetFileName(file), format, bytes);
        }

        return entries.Count > 0
            ? new FigletCatalog(entries, loaders)
            : throw new ArgumentException("The directory contains no .flf or .tlf font files.", nameof(path));
    }

    /// <summary>Builds a catalog from every <c>.flf</c>/<c>.tlf</c> entry in one Zip archive.</summary>
    /// <param name="archive">The non-null readable Zip stream, read to completion and not disposed.</param>
    /// <param name="limits">The optional default finite limits applied when <see cref="Load(string)"/> omits them.</param>
    /// <returns>A new immutable catalog over the discovered entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="archive"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The archive contains no font entries, or two entries resolve to the same name.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// The stream is not a valid Zip archive, or an entry exceeds the configured input byte limit.
    /// </exception>
    [MustUseReturnValue]
    public static FigletCatalog FromZip(Stream archive, FigletLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var effectiveLimits = limits ?? FigletLimits.Default;
        var entries = new Dictionary<string, FigletFontInfo>(StringComparer.Ordinal);
        var loaders = new Dictionary<string, Func<FigletCatalog, FigletLimits, FigletFont>>(StringComparer.Ordinal);

        using (ZipArchive zip = new(archive, ZipArchiveMode.Read, leaveOpen: true))
        {
            foreach (var entry in zip.Entries)
            {
                var format = FontFormatOf(entry.FullName);

                if (format is null)
                {
                    continue;
                }

                if (entry.Length > effectiveLimits.MaxInputBytes)
                {
                    throw new InvalidDataException(
                        $"Archive entry '{entry.FullName}' exceeds the configured input byte limit.");
                }

                var name = Path.GetFileNameWithoutExtension(entry.FullName);
                var bytes = ReadEntry(entry, checked((int) entry.Length));

                AddUnauditedEntry(entries, loaders, name, entry.FullName, format, bytes);
            }
        }

        return entries.Count > 0
            ? new FigletCatalog(entries, loaders)
            : throw new ArgumentException("The archive contains no .flf or .tlf font entries.", nameof(archive));
    }

    /// <summary>Builds a catalog from already-parsed fonts, keyed by their own <see cref="FigletFont.Name"/>.</summary>
    /// <param name="fonts">The non-null sequence of non-null fonts.</param>
    /// <returns>A new immutable catalog wrapping the supplied instances.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fonts"/> or an element is null.</exception>
    /// <exception cref="ArgumentException">The sequence is empty, or two fonts share a name.</exception>
    /// <remarks>
    /// <see cref="GetInfo"/> reports placeholder metadata for these entries: there is no source file
    /// or byte sequence to audit for an already-parsed font.
    /// </remarks>
    [MustUseReturnValue]
    public static FigletCatalog FromFonts(IEnumerable<FigletFont> fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        var entries = new Dictionary<string, FigletFontInfo>(StringComparer.Ordinal);
        var loaders = new Dictionary<string, Func<FigletCatalog, FigletLimits, FigletFont>>(StringComparer.Ordinal);

        foreach (var font in fonts)
        {
            ArgumentNullException.ThrowIfNull(font, nameof(fonts));

            if (!entries.TryAdd(
                    font.Name,
                    new FigletFontInfo(
                        font.Name,
                        file: "<in-memory>",
                        format: "unknown",
                        sha256: string.Empty,
                        bytes: 0,
                        notice: string.Empty,
                        _unauditedLicense,
                        resource: string.Empty,
                        sourceRepository: string.Empty,
                        sourceCommit: string.Empty)))
            {
                throw new ArgumentException(
                    $"The sequence contains more than one font named '{font.Name}'.",
                    nameof(fonts));
            }

            loaders[font.Name] = (_, _) => font;
        }

        return entries.Count > 0
            ? new FigletCatalog(entries, loaders)
            : throw new ArgumentException("The sequence contains no fonts.", nameof(fonts));
    }

    /// <summary>Gets preserved provenance metadata for one exact catalog name.</summary>
    /// <param name="name">The non-null exact case-sensitive name.</param>
    /// <returns>The immutable metadata record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The exact name is absent.</exception>
    [Pure]
    public FigletFontInfo GetInfo(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries.TryGetValue(name, out var value)
            ? value
            : throw new KeyNotFoundException($"The FIGlet catalog does not contain '{name}'.");
    }

    /// <summary>Loads and validates one named font using default finite limits.</summary>
    /// <param name="name">The non-null exact case-sensitive name.</param>
    /// <returns>A parsed font instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The exact name is absent.</exception>
    /// <exception cref="InvalidDataException">The source bytes disagree with recorded provenance.</exception>
    /// <exception cref="FormatException">The selected entry is not a supported FIGfont.</exception>
    public FigletFont Load(string name) => Load(name, FigletLimits.Default);

    /// <summary>Loads and validates one named font using explicit finite limits.</summary>
    /// <param name="name">The non-null exact case-sensitive name.</param>
    /// <param name="limits">The finite parser and renderer limits retained on the returned font.</param>
    /// <returns>A parsed font instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The exact name is absent.</exception>
    /// <exception cref="InvalidDataException">The source bytes disagree with recorded provenance.</exception>
    /// <exception cref="FormatException">The selected entry is not a supported FIGfont.</exception>
    /// <remarks>
    /// The first call for a given name and limits pair reads, hashes, and parses the font; every
    /// later call for that same pair on this instance returns the already-parsed, immutable
    /// result instead of repeating that work. Different limits for the same name are cached
    /// separately, since a stricter or looser limit can change parsing or rejection.
    /// </remarks>
    public FigletFont Load(string name, FigletLimits limits)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!_loaders.TryGetValue(name, out var loader))
        {
            throw new KeyNotFoundException($"The FIGlet catalog does not contain '{name}'.");
        }

        var key = (Name: name, Limits: limits);
        Lazy<FigletFont> lazy;

        lock (_cacheGate)
        {
            if (!_cache.TryGetValue(key, out lazy!))
            {
                lazy = new Lazy<FigletFont>(
                    () => loader(this, limits),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                _cache.Add(key, lazy);
            }
        }

        try
        {
            return lazy.Value;
        }
        catch
        {
            lock (_cacheGate)
            {
                if (_cache.TryGetValue(key, out var current) && ReferenceEquals(current, lazy))
                {
                    _ = _cache.Remove(key);
                }
            }

            throw;
        }
    }

    /// <summary>Creates a fresh catalog over the package's embedded manifest and font resources.</summary>
    /// <returns>A catalog whose resource-read count begins at zero.</returns>
    internal static FigletCatalog CreateEmbedded()
    {
        var assembly = typeof(FigletCatalog).Assembly;
        var manifest = ReadResource(assembly, _manifestResource);
        var entries = ParseManifest(manifest);

        if (entries.Count != 19)
        {
            throw new InvalidDataException("The embedded FIGlet manifest must contain exactly 19 fonts.");
        }

        var loaders = new Dictionary<string, Func<FigletCatalog, FigletLimits, FigletFont>>(StringComparer.Ordinal);

        foreach (var (name, info) in entries)
        {
            loaders[name] = (catalog, limits) => catalog.LoadEmbeddedResource(assembly, info, limits);
        }

        return new FigletCatalog(entries, loaders);
    }

    private FigletFont LoadEmbeddedResource(Assembly assembly, FigletFontInfo info, FigletLimits limits)
    {
        using var stream = assembly.GetManifestResourceStream(info.Resource) ??
                           throw new InvalidDataException($"Embedded font resource '{info.Resource}' is missing.");
        _ = Interlocked.Increment(ref _embeddedResourceReadCount);

        if (stream.Length != info.Bytes || stream.Length > limits.MaxInputBytes)
        {
            throw new InvalidDataException($"The embedded font length for '{info.File}' is invalid.");
        }

        var bytes = new byte[checked((int) stream.Length)];
        stream.ReadExactly(bytes);

        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException($"Embedded font resource '{info.Resource}' exceeds its declared length.");
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        return hash.Equals(info.Sha256, StringComparison.Ordinal)
            ? FigletFont.Load(new MemoryStream(bytes, writable: false), info.Name, limits)
            : throw new InvalidDataException(
                $"The embedded font hash for '{info.File}' does not match its audit.");
    }

    private static void AddUnauditedEntry(
        Dictionary<string, FigletFontInfo> entries,
        Dictionary<string, Func<FigletCatalog, FigletLimits, FigletFont>> loaders,
        string name,
        string file,
        string format,
        byte[] bytes)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (!entries.TryAdd(
                name,
            new FigletFontInfo(
                name,
                file,
                format,
                hash,
                bytes.Length,
                notice: string.Empty,
                _unauditedLicense,
                resource: string.Empty,
                sourceRepository: string.Empty,
                sourceCommit: string.Empty)))
        {
            throw new ArgumentException($"More than one font resolves to the name '{name}'.");
        }

        loaders[name] = (_, limits) => FigletFont.Load(new MemoryStream(bytes, writable: false), name, limits);
    }

    private static string? FontFormatOf(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        return extension.Equals(".flf", StringComparison.OrdinalIgnoreCase) ? "flf" :
            extension.Equals(".tlf", StringComparison.OrdinalIgnoreCase) ? "tlf" :
            null;
    }

    private static byte[] ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name) ??
                           throw new InvalidDataException($"Embedded resource '{name}' is missing.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, int length)
    {
        var bytes = new byte[length];
        using var stream = entry.Open();
        stream.ReadExactly(bytes);

        return stream.ReadByte() == -1
            ? bytes
            : throw new InvalidDataException(
                $"Archive entry '{entry.FullName}' exceeds its declared length.");
    }

    private static Dictionary<string, FigletFontInfo> ParseManifest(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        if (root.GetProperty("schema").GetInt32() != 2 || root.GetProperty("count").GetInt32() != 19)
        {
            throw new InvalidDataException("The embedded FIGlet manifest count is invalid.");
        }

        var result = new Dictionary<string, FigletFontInfo>(StringComparer.Ordinal);

        foreach (var element in root.GetProperty("fonts").EnumerateArray())
        {
            var info = new FigletFontInfo(
                element.GetProperty("name").GetString()!,
                element.GetProperty("file").GetString()!,
                element.GetProperty("format").GetString()!,
                element.GetProperty("sha256").GetString()!,
                element.GetProperty("bytes").GetInt32(),
                element.GetProperty("notice").GetString()!,
                element.GetProperty("license").GetString()!,
                element.GetProperty("resource").GetString()!,
                element.GetProperty("sourceRepository").GetString()!,
                element.GetProperty("sourceCommit").GetString()!);

            if (string.IsNullOrWhiteSpace(info.Name) ||
                string.IsNullOrWhiteSpace(info.File) ||
                info.File.Contains('/') ||
                info.File.Contains('\\') ||
                info.Bytes <= 0 ||
                info.Sha256.Length != 64 ||
                info.Resource != $"SharpVision.FigletFonts.Resources.Fonts.{info.File}" ||
                info.License is not ("BSD-3-Clause" or "MIT") ||
                string.IsNullOrWhiteSpace(info.SourceRepository) ||
                info.SourceCommit.Length != 40 ||
                !result.TryAdd(info.Name, info))
            {
                throw new InvalidDataException("The embedded FIGlet manifest contains an invalid entry.");
            }
        }

        return result;
    }
}
