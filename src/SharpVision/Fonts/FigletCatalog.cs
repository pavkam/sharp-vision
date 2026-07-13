// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Provides case-sensitive lazy access to the audited embedded font archive.</summary>
public sealed class FigletCatalog
{
    private const string _archiveResource = "SharpVision.Fonts.figlet-fonts.zip";
    private const string _manifestResource = "SharpVision.Fonts.fonts.manifest.json";
    private readonly byte[] _archive;
    private readonly Dictionary<string, FigletFontInfo> _entries;

    private FigletCatalog()
    {
        Assembly assembly = typeof(FigletCatalog).Assembly;
        _archive = ReadResource(assembly, _archiveResource);
        var manifest = ReadResource(assembly, _manifestResource);
        _entries = ParseManifest(manifest);
        Names = _entries.Keys.Order(StringComparer.Ordinal).ToArray();

        if (Names.Count != 400)
        {
            throw new InvalidDataException("The embedded FIGlet manifest must contain exactly 400 fonts.");
        }
    }

    /// <summary>Gets the process-wide immutable embedded catalog.</summary>
    public static FigletCatalog Default { get; } = new();

    /// <summary>Gets exact case-sensitive names in ordinal order.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Gets preserved audit metadata for one exact catalog name.</summary>
    /// <param name="name">The non-null exact case-sensitive name.</param>
    /// <returns>The immutable manifest record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The exact name is absent.</exception>
    public FigletFontInfo GetInfo(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries.TryGetValue(name, out FigletFontInfo value)
            ? value
            : throw new KeyNotFoundException($"The FIGlet catalog does not contain '{name}'.");
    }

    /// <summary>Loads and validates one named font from the embedded archive.</summary>
    /// <param name="name">The non-null exact case-sensitive name.</param>
    /// <returns>A new immutable parsed font instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The exact name is absent.</exception>
    /// <exception cref="InvalidDataException">Archive bytes disagree with the audited manifest.</exception>
    /// <exception cref="FormatException">The selected entry is not a supported FIGfont.</exception>
    public FigletFont Load(string name)
    {
        FigletFontInfo info = GetInfo(name);
        using ZipArchive archive = new ZipArchive(
            new MemoryStream(_archive, writable: false),
            ZipArchiveMode.Read,
            leaveOpen: false);
        ZipArchiveEntry entry = archive.GetEntry(info.File) ??
            throw new InvalidDataException($"The archive is missing audited entry '{info.File}'.");

        if (entry.Length != info.Bytes || entry.Length > FigletLimits.Default.MaxInputBytes)
        {
            throw new InvalidDataException($"The archive length for '{info.File}' is invalid.");
        }

        var bytes = ReadEntry(entry, checked((int) entry.Length));
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (!hash.Equals(info.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The archive hash for '{info.File}' does not match its audit.");
        }

        bytes = Unwrap(bytes, info.File);
        return FigletFont.Load(new MemoryStream(bytes, writable: false), info.Name);
    }

    private static byte[] ReadResource(Assembly assembly, string name)
    {
        using Stream stream = assembly.GetManifestResourceStream(name) ??
            throw new InvalidDataException($"Embedded resource '{name}' is missing.");
        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, int length)
    {
        var bytes = new byte[length];
        using Stream stream = entry.Open();
        stream.ReadExactly(bytes);

        return stream.ReadByte() == -1
            ? bytes
            : throw new InvalidDataException(
                $"Archive entry '{entry.FullName}' exceeds its declared length.");
    }

    private static byte[] Unwrap(byte[] bytes, string file)
    {
        if (bytes.Length < 4 ||
            bytes[0] != (byte) 'P' ||
            bytes[1] != (byte) 'K' ||
            bytes[2] != 3 ||
            bytes[3] != 4)
        {
            return bytes;
        }

        using ZipArchive nested = new ZipArchive(
            new MemoryStream(bytes, writable: false),
            ZipArchiveMode.Read,
            leaveOpen: false);

        if (nested.Entries.Count != 1)
        {
            throw new InvalidDataException($"Nested font archive '{file}' must have exactly one entry.");
        }

        ZipArchiveEntry entry = nested.Entries[0];

        return entry.Length > 0 && entry.Length <= FigletLimits.Default.MaxInputBytes
            ? ReadEntry(entry, checked((int) entry.Length))
            : throw new InvalidDataException(
                $"Nested font archive '{file}' has an invalid entry length.");
    }

    private static Dictionary<string, FigletFontInfo> ParseManifest(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;

        if (root.GetProperty("count").GetInt32() != 400)
        {
            throw new InvalidDataException("The embedded FIGlet manifest count is invalid.");
        }

        Dictionary<string, FigletFontInfo> result = new Dictionary<string, FigletFontInfo>(StringComparer.Ordinal);

        foreach (JsonElement element in root.GetProperty("fonts").EnumerateArray())
        {
            FigletFontInfo info = new FigletFontInfo(
                element.GetProperty("name").GetString()!,
                element.GetProperty("file").GetString()!,
                element.GetProperty("format").GetString()!,
                element.GetProperty("sha256").GetString()!,
                element.GetProperty("bytes").GetInt32(),
                element.GetProperty("notice").GetString()!,
                element.GetProperty("license").GetString()!);

            if (string.IsNullOrWhiteSpace(info.Name) ||
                string.IsNullOrWhiteSpace(info.File) ||
                info.File.Contains('/') ||
                info.File.Contains('\\') ||
                info.Bytes <= 0 ||
                info.Sha256.Length != 64 ||
                string.IsNullOrWhiteSpace(info.License) ||
                !result.TryAdd(info.Name, info))
            {
                throw new InvalidDataException("The embedded FIGlet manifest contains an invalid entry.");
            }
        }

        return result;
    }
}
