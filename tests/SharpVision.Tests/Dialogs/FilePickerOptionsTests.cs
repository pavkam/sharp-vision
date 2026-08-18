// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies defaults, ownership, and validation for file-picker options.</summary>
public sealed class FilePickerOptionsTests
{
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
}
