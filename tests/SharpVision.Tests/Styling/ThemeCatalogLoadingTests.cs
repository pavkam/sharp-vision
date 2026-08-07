// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the public runtime theme-file loader.</summary>
public sealed class ThemeCatalogLoadingTests
{
    private static readonly string _json = ThemeJson.Create(
        palette: "\"bg\":\"#101020\",\"fg\":\"#f0f0ff\"",
        name: "Ext",
        background: "#101020",
        foreground: "#f0f0ff");

    /// <summary>Verifies parsing valid JSON text returns a frozen theme with resolved control colors.</summary>
    [Fact]
    public void Parse_WhenValid_ReturnsFrozenTheme()
    {
        var theme = ThemeCatalog.Parse(_json);

        theme.Frozen.ShouldBeTrue();
        var accent = ThemeColorHelper.Accent(theme);
        accent.ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
    }

    /// <summary>Verifies parsing a null JSON string throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Parse_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeCatalog.Parse(null!));

    /// <summary>Verifies parsing malformed JSON is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void Parse_WhenMalformedJson_Throws() =>
        Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse("{ not json"));

    /// <summary>Verifies loading from a stream returns a frozen theme with resolved control colors.</summary>
    [Fact]
    public void Load_WhenStream_ReturnsFrozenTheme()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(_json));

        var theme = ThemeCatalog.Load(stream);

        var bg = ThemeColorHelper.Background(theme);
        bg.ShouldBe(Color.Rgb(0x10, 0x10, 0x20));
    }

    /// <summary>Verifies loading from a stream leaves the caller-owned stream open afterward.</summary>
    [Fact]
    public void Load_WhenStream_LeavesStreamOpen()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(_json));

        _ = ThemeCatalog.Load(stream);

        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies a seekable stream is read into a buffer sized from the document itself
    /// instead of the historical fixed 64KB+1 scratch buffer that every document paid regardless
    /// of its real size. Compares two documents differing only by padding: with a
    /// right-sized buffer the extra allocation tracks the extra padding bytes; with the old fixed
    /// scratch buffer it would stay flat regardless of size. Deserialization and Theme-graph
    /// construction cost is identical between the two calls (the padding is whitespace, not
    /// additional semantic content), so the delta isolates the read buffer.</summary>
    [Fact]
    public void Load_WhenStreamIsSeekable_AllocatesProportionallyToDocumentSize()
    {
        var padded = _json + new string(' ', ThemeCatalog.MaximumDocumentBytes - Encoding.UTF8.GetByteCount(_json) - 100);

        using var small = new MemoryStream(Encoding.UTF8.GetBytes(_json));
        var beforeSmall = GC.GetAllocatedBytesForCurrentThread();
        _ = ThemeCatalog.Load(small);
        var allocatedSmall = GC.GetAllocatedBytesForCurrentThread() - beforeSmall;

        using var large = new MemoryStream(Encoding.UTF8.GetBytes(padded));
        var beforeLarge = GC.GetAllocatedBytesForCurrentThread();
        _ = ThemeCatalog.Load(large);
        var allocatedLarge = GC.GetAllocatedBytesForCurrentThread() - beforeLarge;

        (allocatedLarge - allocatedSmall).ShouldBeGreaterThan(ThemeCatalog.MaximumDocumentBytes / 2);
    }

    /// <summary>Verifies loading from a null stream throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Load_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeCatalog.Load((Stream) null!));

    /// <summary>Verifies loading malformed stream content is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void Load_WhenMalformedJson_Throws()
    {
        using MemoryStream stream = new("{ not json"u8.ToArray());

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Load(stream));
        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies missing or empty semantic sections are rejected.</summary>
    [Theory]
    [InlineData( /*lang=json,strict*/ """{"status":{}}""")]
    [InlineData( /*lang=json,strict*/ """{"colors":{},"attributes":{},"styles":{}}""")]
    public void Parse_WhenSemanticSectionsAreMissingOrEmpty_Throws(string json) =>
        Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));

    /// <summary>Verifies unknown and duplicate object fields are rejected instead of silently ignored.</summary>
    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"unknown":true,"controls":{"Control":{"normal":{"background":"#000","foreground":"#fff"}}}}""")]
    [InlineData( /*lang=json,strict*/
        """{"roles":{},"controls":{"Control":{"normal":{"background":"#000","foreground":"#fff"}}}}""")]
    [InlineData( /*lang=json,strict*/
        """{"glyphs":{},"controls":{"Control":{"normal":{"background":"#000","foreground":"#fff"}}}}""")]
    [InlineData( /*lang=json,strict*/
        """{"name":"first","name":"second","controls":{"Control":{"normal":{"background":"#000","foreground":"#fff"}}}}""")]
    public void Parse_WhenFieldIsUnknownOrDuplicated_Throws(string json) =>
        Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));

    /// <summary>Verifies the documented byte limit accepts its boundary and rejects one extra byte.</summary>
    [Fact]
    public void Parse_WhenDocumentReachesByteLimit_UsesExactBound()
    {
        var bytes = Encoding.UTF8.GetByteCount(_json);
        var boundary = _json + new string(' ', ThemeCatalog.MaximumDocumentBytes - bytes);
        var oversized = boundary + " ";

        ThemeCatalog.Parse(boundary).Frozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(oversized));
    }

    /// <summary>Verifies fragmented non-seekable input is consumed from its current position and remains caller-owned.</summary>
    [Fact]
    public void Load_WhenFragmentedAndNonSeekable_ParsesIncrementallyAndLeavesOpen()
    {
        var prefix = "ignored"u8.ToArray();
        var document = Encoding.UTF8.GetBytes(_json);
        var bytes = prefix.Concat(document).ToArray();
        using var stream = new FragmentedReadStream(bytes, prefix.Length, fragmentLength: 3);

        var theme = ThemeCatalog.Load(stream);

        theme.Frozen.ShouldBeTrue();
        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies malformed UTF-8 is rejected without closing the caller-owned stream.</summary>
    [Fact]
    public void Load_WhenUtf8IsMalformed_ThrowsAndLeavesStreamOpen()
    {
        using MemoryStream stream = new([0xff, 0xfe, 0xfd]);

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Load(stream));

        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies palette entry count accepts its boundary and rejects one extra entry.</summary>
    [Fact]
    public void Parse_WhenPaletteEntryCountReachesLimit_UsesExactBound()
    {
        var valid = JsonWithPaletteEntries(256);
        var invalid = JsonWithPaletteEntries(257);

        ThemeCatalog.Parse(valid).Frozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(invalid));
    }

    /// <summary>Verifies each metadata string accepts its boundary and rejects one extra character.</summary>
    [Fact]
    public void Parse_WhenMetadataStringReachesLimit_UsesExactBound()
    {
        var boundary = new string('n', 2048);
        var valid = JsonWithName(boundary);
        var invalid = JsonWithName(boundary + "n");

        ThemeCatalog.Parse(valid).Frozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(invalid));
    }

    /// <summary>Verifies loading a file path returns a frozen theme with resolved control colors.</summary>
    [Fact]
    public void LoadFile_WhenValid_ReturnsFrozenTheme()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, _json);

        try
        {
            var theme = ThemeCatalog.LoadFile(path);

            var accent = ThemeColorHelper.Accent(theme);
            accent.ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies a leading UTF-8 byte order mark - what Visual Studio's "UTF-8 with
    /// signature", Notepad, and <c>new StreamWriter(path, false, Encoding.UTF8)</c> all produce -
    /// does not prevent loading. The Deserialize(ReadOnlySpan&lt;byte&gt;, ...) overload does not
    /// strip a preamble the way the Stream overload does for free, and buffering into a byte[] to
    /// enforce the size bound loses that leniency by accident.</summary>
    [Fact]
    public void LoadFile_WhenContentHasUtf8Preamble_ReturnsFrozenTheme()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var withPreamble = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_json)).ToArray();
        File.WriteAllBytes(path, withPreamble);

        try
        {
            var theme = ThemeCatalog.LoadFile(path);

            var accent = ThemeColorHelper.Accent(theme);
            accent.ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies a root-level parse failure (an empty document, in this case) reports a
    /// clean message instead of the "... at ''." artifact: error.Path is "$" at the document root,
    /// which is not whitespace, so the untrimmed guard let it through while interpolating the
    /// trimmed - empty - value.</summary>
    [Fact]
    public void LoadFile_WhenContentIsEmpty_DoesNotReportDanglingEmptyPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, string.Empty);

        try
        {
            var thrown = Should.Throw<InvalidDataException>(() => ThemeCatalog.LoadFile(path));
            thrown.Message.ShouldNotContain("at ''");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies loading a null file path throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void LoadFile_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeCatalog.LoadFile(null!));

    /// <summary>Verifies loading a missing file path throws <see cref="FileNotFoundException"/>.</summary>
    [Fact]
    public void LoadFile_WhenFileMissing_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        _ = Should.Throw<FileNotFoundException>(() => ThemeCatalog.LoadFile(path));
    }

    /// <summary>Verifies loading a file with malformed content is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void LoadFile_WhenMalformedJson_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, "{ not json");

        try
        {
            _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.LoadFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string JsonWithPaletteEntries(int count)
    {
        var entries = Enumerable.Range(0, count)
            .Select(static index => $"\"k{index}\":\"#000000\"");
        return ThemeJson.Create(palette: string.Join(',', entries));
    }

    private static string JsonWithName(string name) => ThemeJson.Create(name: name);

}
