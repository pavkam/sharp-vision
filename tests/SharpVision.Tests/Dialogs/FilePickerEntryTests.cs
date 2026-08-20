// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies identity semantics for file-picker entries.</summary>
public sealed class FilePickerEntryTests
{
    /// <summary>Verifies construction retains every constructor argument unchanged.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RetainsEveryProperty()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-entry-construction"));
        var fullPath = Path.Combine(directory, "notes.txt");

        // Act
        var entry = new FilePickerEntry("notes.txt", fullPath, isDirectory: false, isHidden: true);

        // Assert
        entry.Name.ShouldBe("notes.txt");
        entry.FullPath.ShouldBe(fullPath);
        entry.IsDirectory.ShouldBeFalse();
        entry.IsHidden.ShouldBeTrue();
    }

    /// <summary>Verifies every malformed constructor argument is rejected before an instance can be observed.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsValidationExceptions()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-entry-invalid"));
        var fullPath = Path.Combine(directory, "notes.txt");

        _ = Should.Throw<ArgumentNullException>(() => new FilePickerEntry(null!, fullPath, false, false));
        _ = Should.Throw<ArgumentNullException>(() => new FilePickerEntry("notes.txt", null!, false, false));
        _ = Should.Throw<ArgumentException>(() => new FilePickerEntry(" ", fullPath, false, false));
        _ = Should.Throw<ArgumentException>(() => new FilePickerEntry("notes.txt", " ", false, false));
        _ = Should.Throw<ArgumentException>(() => new FilePickerEntry("notes.txt", "notes.txt", false, false));
    }

    /// <summary>Verifies two entries whose FullPath differs only by case are treated as distinct
    /// identities - matching the Ordinal tie-break FilePickerEntryComparer already assumes when
    /// sorting case-variant siblings (e.g. "readme.txt" and "Readme.txt") as separate rows.</summary>
    [Fact]
    public void Equals_WhenFullPathDiffersOnlyByCase_ReturnsFalseAndHashCodesDiffer()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-entry-identity"));
        var lower = new FilePickerEntry("readme.txt", Path.Combine(directory, "readme.txt"), false, false);
        var upper = new FilePickerEntry("Readme.txt", Path.Combine(directory, "Readme.txt"), false, false);

        // Act and assert
        lower.Equals(upper).ShouldBeFalse();
        upper.Equals(lower).ShouldBeFalse();
        lower.GetHashCode().ShouldNotBe(upper.GetHashCode());
    }

    /// <summary>Verifies two entries with an identical FullPath remain equal with matching hash
    /// codes, the ordinary case this identity is meant to preserve.</summary>
    [Fact]
    public void Equals_WhenFullPathMatchesExactly_ReturnsTrueAndHashCodesMatch()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-entry-identity"));
        var path = Path.Combine(directory, "readme.txt");
        var first = new FilePickerEntry("readme.txt", path, false, false);
        var second = new FilePickerEntry("readme.txt", path, false, false);

        // Act and assert
        first.Equals(second).ShouldBeTrue();
        second.Equals(first).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
