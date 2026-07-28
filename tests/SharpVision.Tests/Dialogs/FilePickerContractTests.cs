// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;


/// <summary>Defines validation, ownership, and immutable result behavior for file-picker values.</summary>
public sealed class FilePickerContractTests
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

    /// <summary>Verifies options expose useful defaults and copy caller-owned filters.</summary>
    [Fact]
    public void Constructor_WhenOptionsAreCreated_UsesDefaultsAndOwnsFilters()
    {
        // Arrange
        var filters = new[]
        {
            new FilePickerFilter("Sources", "*.cs"),
            FilePickerFilter.AllFiles
        };
        var options = new FilePickerOptions
        {
            // Act
            Filters = filters
        };
        filters[0] = FilePickerFilter.AllFiles;

        // Assert
        options.Title.ShouldBe("Open File");
        options.InitialDirectory.ShouldBe(Environment.CurrentDirectory);
        options.AllowMultiple.ShouldBeFalse();
        options.ShowHidden.ShouldBeFalse();
        options.MaxVisibleRows.ShouldBe(20);
        options.FilterIndex.ShouldBe(0);
        options.Filters[0].Name.ShouldBe("Sources");
    }

    /// <summary>Verifies invalid option assignments leave the previously committed values intact.</summary>
    [Fact]
    public void Properties_WhenOptionsAreInvalid_ThrowBeforeMutation()
    {
        var options = new FilePickerOptions
        {
            Filters = [new FilePickerFilter("Sources", "*.cs"), FilePickerFilter.AllFiles],
            FilterIndex = 1,
            MaxVisibleRows = 7
        };

        _ = Should.Throw<ArgumentNullException>(() => options.Title = null!);
        _ = Should.Throw<ArgumentException>(() => options.Title = " ");
        _ = Should.Throw<ArgumentNullException>(() => options.InitialDirectory = null!);
        _ = Should.Throw<ArgumentException>(() => options.InitialDirectory = " ");
        _ = Should.Throw<ArgumentNullException>(() => options.Filters = null!);
        _ = Should.Throw<ArgumentException>(() => options.Filters = []);
        _ = Should.Throw<ArgumentException>(() => options.Filters = [null!]);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => options.FilterIndex = 2);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => options.MaxVisibleRows = 0);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => options.MaxVisibleRows = -1);
        _ = Should.Throw<ArgumentException>(() => options.Filters = [FilePickerFilter.AllFiles]);

        options.Title.ShouldBe("Open File");
        options.InitialDirectory.ShouldBe(Environment.CurrentDirectory);
        options.Filters.Count.ShouldBe(2);
        options.FilterIndex.ShouldBe(1);
        options.MaxVisibleRows.ShouldBe(7);
    }

    /// <summary>Verifies accepted and cancelled results own stable path snapshots.</summary>
    [Fact]
    public void Create_WhenResultIsProduced_OwnsPathsAndExposesSinglePathConvenience()
    {
        // Arrange
        var paths = new[] { "/workspace/a.cs", "/workspace/b.cs" };

        // Act
        var accepted = FilePickerResult.Accept(paths);
        paths[0] = "/changed";
        var cancelled = FilePickerResult.Cancelled;

        // Assert
        accepted.Accepted.ShouldBeTrue();
        accepted.Paths.Count.ShouldBe(2);
        accepted.Paths[0].ShouldBe("/workspace/a.cs");
        accepted.Paths[1].ShouldBe("/workspace/b.cs");
        accepted.SelectedPath.ShouldBe("/workspace/a.cs");
        cancelled.Accepted.ShouldBeFalse();
        cancelled.Paths.ShouldBeEmpty();
        cancelled.SelectedPath.ShouldBeNull();
    }
}
