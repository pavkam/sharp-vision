// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies validation and ownership behavior for file-picker filters.</summary>
public sealed class FilePickerFilterTests
{
    /// <summary>Verifies filters retain an owned pattern snapshot and use deterministic basename wildcard matching.</summary>
    [Fact]
    public void Constructor_WhenFilterIsValid_CopiesPatternsAndMatchesNamesOrdinalIgnoreCase()
    {
        // Arrange
        var patterns = new[] { "*.cs", "README.?d" };

        // Act
        var filter = new FilePickerFilter("Sources", patterns);
        patterns[0] = "*.txt";

        // Assert
        filter.Name.ShouldBe("Sources");
        filter.Patterns.Count.ShouldBe(2);
        filter.Patterns[0].ShouldBe("*.cs");
        filter.Patterns[1].ShouldBe("README.?d");
        filter.Matches("Program.CS").ShouldBeTrue();
        filter.Matches("README.md").ShouldBeTrue();
        filter.Matches("notes.txt").ShouldBeFalse();
    }

    /// <summary>Verifies every malformed filter argument is rejected before an instance can be observed.</summary>
    [Fact]
    public void Constructor_WhenFilterIsInvalid_ThrowsValidationException()
    {
        _ = Should.Throw<ArgumentNullException>(() => new FilePickerFilter(null!, "*"));
        _ = Should.Throw<ArgumentException>(() => new FilePickerFilter(" ", "*"));
        _ = Should.Throw<ArgumentNullException>(() => new FilePickerFilter("Files", null!));
        _ = Should.Throw<ArgumentException>(() => new FilePickerFilter("Files", []));
        _ = Should.Throw<ArgumentException>(() => new FilePickerFilter("Files", ""));
        _ = Should.Throw<ArgumentException>(() => new FilePickerFilter("Files", "sub/*.cs"));
        _ = Should.Throw<ArgumentException>(() => new FilePickerFilter("Files", "../*.cs"));
        _ = Should.Throw<ArgumentException>(() => new FilePickerFilter("Files", Path.GetPathRoot(Environment.CurrentDirectory)!));
    }
}
