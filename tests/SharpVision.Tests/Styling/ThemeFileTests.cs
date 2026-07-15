// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;




/// <summary>Verifies the public runtime theme-file loader.</summary>
public sealed class ThemeFileTests
{
    private const string _json = /*lang=json,strict*/ """
        { "version": 1, "name": "Ext", "slug": "ext", "colorScheme": "dark", "order": 1,
          "author": "A", "license": "MIT", "source": "s",
          "palette": { "bg": "#101020", "fg": "#f0f0ff" },
          "roles": { "background": "bg", "foreground": "fg", "accent": "#77aaff" } }
        """;

    /// <summary>Verifies parsing valid JSON text returns a frozen theme with resolved roles.</summary>
    [Fact]
    public void Parse_WhenValid_ReturnsFrozenTheme()
    {
        var theme = ThemeFile.Parse(_json);

        theme.IsFrozen.ShouldBeTrue();
        theme.TryGetColor(ColorRole.Accent, out var accent).ShouldBeTrue();
        accent.ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
    }

    /// <summary>Verifies parsing a null JSON string throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Parse_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeFile.Parse(null!));

    /// <summary>Verifies parsing malformed JSON is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void Parse_WhenMalformedJson_Throws() =>
        Should.Throw<InvalidDataException>(() => ThemeFile.Parse("{ not json"));

    /// <summary>Verifies loading from a stream returns a frozen theme with resolved roles.</summary>
    [Fact]
    public void Load_WhenStream_ReturnsFrozenTheme()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(_json));

        var theme = ThemeFile.Load(stream);

        theme.TryGetColor(ColorRole.Background, out var bg).ShouldBeTrue();
        bg.ShouldBe(Color.Rgb(0x10, 0x10, 0x20));
    }

    /// <summary>Verifies loading from a stream leaves the caller-owned stream open afterward.</summary>
    [Fact]
    public void Load_WhenStream_LeavesStreamOpen()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(_json));

        _ = ThemeFile.Load(stream);

        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies loading from a null stream throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Load_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeFile.Load(null!));

    /// <summary>Verifies loading malformed stream content is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void Load_WhenMalformedJson_Throws()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("{ not json"));

        _ = Should.Throw<InvalidDataException>(() => ThemeFile.Load(stream));
        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies missing and unsupported schema versions are rejected before theme construction.</summary>
    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"roles\":{\"background\":\"#000000\",\"foreground\":\"#ffffff\"}}")]
    [InlineData(/*lang=json,strict*/ "{\"version\":2,\"roles\":{\"background\":\"#000000\",\"foreground\":\"#ffffff\"}}")]
    public void Parse_WhenVersionIsMissingOrUnsupported_Throws(string json) =>
        Should.Throw<InvalidDataException>(() => ThemeFile.Parse(json));

    /// <summary>Verifies unknown and duplicate object fields are rejected instead of silently ignored.</summary>
    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"version\":1,\"unknown\":true,\"roles\":{\"background\":\"#000000\",\"foreground\":\"#ffffff\"}}")]
    [InlineData(/*lang=json,strict*/ "{\"version\":1,\"version\":1,\"roles\":{\"background\":\"#000000\",\"foreground\":\"#ffffff\"}}")]
    [InlineData(/*lang=json,strict*/ "{\"version\":1,\"palette\":{\"x\":\"#000000\",\"x\":\"#ffffff\"},\"roles\":{\"background\":\"x\",\"foreground\":\"x\"}}")]
    [InlineData(/*lang=json,strict*/ "{\"version\":1,\"roles\":{\"background\":\"#000000\",\"background\":\"#ffffff\",\"foreground\":\"#ffffff\"}}")]
    public void Parse_WhenFieldIsUnknownOrDuplicated_Throws(string json) =>
        Should.Throw<InvalidDataException>(() => ThemeFile.Parse(json));

    /// <summary>Verifies the documented byte limit accepts its boundary and rejects one extra byte.</summary>
    [Fact]
    public void Parse_WhenDocumentReachesByteLimit_UsesExactBound()
    {
        var bytes = Encoding.UTF8.GetByteCount(_json);
        var boundary = _json + new string(' ', ThemeLoader.MaximumDocumentBytes - bytes);
        var oversized = boundary + " ";

        ThemeFile.Parse(boundary).IsFrozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeFile.Parse(oversized));
    }

    /// <summary>Verifies fragmented non-seekable input is consumed from its current position and remains caller-owned.</summary>
    [Fact]
    public void Load_WhenFragmentedAndNonSeekable_ParsesIncrementallyAndLeavesOpen()
    {
        var prefix = Encoding.UTF8.GetBytes("ignored");
        var document = Encoding.UTF8.GetBytes(_json);
        var bytes = prefix.Concat(document).ToArray();
        using var stream = new FragmentedReadStream(bytes, prefix.Length, fragmentLength: 3);

        var theme = ThemeFile.Load(stream);

        theme.IsFrozen.ShouldBeTrue();
        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies malformed UTF-8 is rejected without closing the caller-owned stream.</summary>
    [Fact]
    public void Load_WhenUtf8IsMalformed_ThrowsAndLeavesStreamOpen()
    {
        using MemoryStream stream = new([0xff, 0xfe, 0xfd]);

        _ = Should.Throw<InvalidDataException>(() => ThemeFile.Load(stream));

        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies palette keys use the exact documented character bound.</summary>
    [Fact]
    public void Parse_WhenPaletteKeyReachesLimit_UsesExactBound()
    {
        var boundary = new string('k', ThemeLoader.MaximumKeyCharacters);
        var valid = JsonWithPaletteKey(boundary);
        var invalid = JsonWithPaletteKey(boundary + "k");

        ThemeFile.Parse(valid).IsFrozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeFile.Parse(invalid));
    }

    /// <summary>Verifies palette entry count accepts its boundary and rejects one extra entry.</summary>
    [Fact]
    public void Parse_WhenPaletteEntryCountReachesLimit_UsesExactBound()
    {
        var valid = JsonWithPaletteEntries(256);
        var invalid = JsonWithPaletteEntries(257);

        ThemeFile.Parse(valid).IsFrozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeFile.Parse(invalid));
    }

    /// <summary>Verifies semantic role count accepts every defined role and rejects a thirteenth field.</summary>
    [Fact]
    public void Parse_WhenRoleEntryCountReachesLimit_UsesExactBound()
    {
        var valid = JsonWithRoles(includeExtra: false);
        var invalid = JsonWithRoles(includeExtra: true);

        ThemeFile.Parse(valid).IsFrozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeFile.Parse(invalid));
    }

    /// <summary>Verifies each metadata string accepts its boundary and rejects one extra character.</summary>
    [Fact]
    public void Parse_WhenMetadataStringReachesLimit_UsesExactBound()
    {
        var boundary = new string('n', 2048);
        var valid = JsonWithName(boundary);
        var invalid = JsonWithName(boundary + "n");

        ThemeFile.Parse(valid).IsFrozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeFile.Parse(invalid));
    }

    /// <summary>Verifies loading a file path returns a frozen theme with resolved roles.</summary>
    [Fact]
    public void LoadFile_WhenValid_ReturnsFrozenTheme()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, _json);

        try
        {
            var theme = ThemeFile.LoadFile(path);

            theme.TryGetColor(ColorRole.Accent, out var accent).ShouldBeTrue();
            accent.ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies loading a null file path throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void LoadFile_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeFile.LoadFile(null!));

    /// <summary>Verifies loading a missing file path throws <see cref="FileNotFoundException"/>.</summary>
    [Fact]
    public void LoadFile_WhenFileMissing_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        _ = Should.Throw<FileNotFoundException>(() => ThemeFile.LoadFile(path));
    }

    /// <summary>Verifies loading a file with malformed content is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void LoadFile_WhenMalformedJson_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, "{ not json");

        try
        {
            _ = Should.Throw<InvalidDataException>(() => ThemeFile.LoadFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string JsonWithPaletteKey(string key) => $$"""
        { "version": 1, "palette": { "{{key}}": "#000000", "fg": "#ffffff" },
          "roles": { "background": "{{key}}", "foreground": "fg" } }
        """;

    private static string JsonWithPaletteEntries(int count)
    {
        var entries = Enumerable.Range(0, count)
            .Select(static index => $"\"k{index}\":\"#000000\"");
        return $$"""
            { "version": 1, "palette": { {{string.Join(',', entries)}} },
              "roles": { "background": "k0", "foreground": "k0" } }
            """;
    }

    private static string JsonWithName(string name) => $$"""
        { "version": 1, "name": "{{name}}",
          "roles": { "background": "#000000", "foreground": "#ffffff" } }
        """;

    private static string JsonWithRoles(bool includeExtra)
    {
        string[] roles =
        [
            "foreground",
            "background",
            "surface",
            "border",
            "accent",
            "muted",
            "selectionBackground",
            "selectionForeground",
            "error",
            "warning",
            "success",
            "info",
        ];
        var entries = roles.Select(static role => $"\"{role}\":\"#000000\"").ToList();

        if (includeExtra)
        {
            entries.Add("\"extra\":\"#000000\"");
        }

        return $$"""
            { "version": 1, "roles": { {{string.Join(',', entries)}} } }
            """;
    }
}
