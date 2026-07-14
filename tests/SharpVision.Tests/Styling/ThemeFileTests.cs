// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Text;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the public runtime theme-file loader.</summary>
public sealed class ThemeFileTests
{
    private const string _json = /*lang=json,strict*/ """
        { "name": "Ext", "slug": "ext", "colorScheme": "dark", "order": 1,
          "author": "A", "license": "MIT", "source": "s",
          "palette": { "bg": "#101020", "fg": "#f0f0ff" },
          "roles": { "background": "bg", "foreground": "fg", "accent": "#77aaff" } }
        """;

    /// <summary>Verifies parsing valid JSON text returns a frozen theme with resolved roles.</summary>
    [Fact]
    public void Parse_WhenValid_ReturnsFrozenTheme()
    {
        Theme theme = ThemeFile.Parse(_json);

        theme.IsFrozen.ShouldBeTrue();
        theme.TryGetColor(ColorRole.Accent, out Color accent).ShouldBeTrue();
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

        Theme theme = ThemeFile.Load(stream);

        theme.TryGetColor(ColorRole.Background, out Color bg).ShouldBeTrue();
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
    }

    /// <summary>Verifies loading a file path returns a frozen theme with resolved roles.</summary>
    [Fact]
    public void LoadFile_WhenValid_ReturnsFrozenTheme()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, _json);

        try
        {
            Theme theme = ThemeFile.LoadFile(path);

            theme.TryGetColor(ColorRole.Accent, out Color accent).ShouldBeTrue();
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
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        _ = Should.Throw<FileNotFoundException>(() => ThemeFile.LoadFile(path));
    }

    /// <summary>Verifies loading a file with malformed content is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void LoadFile_WhenMalformedJson_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
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
}
