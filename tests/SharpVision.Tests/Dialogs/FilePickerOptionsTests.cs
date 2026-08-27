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
        options.SelectionMode.ShouldBe(FileSelectionMode.Files);
        options.ShowHidden.ShouldBeFalse();
        options.MaxVisibleRows.ShouldBe(20);
        options.FilterIndex.ShouldBe(0);
        options.Filters[0].Name.ShouldBe("Sources");
        options.ReadyText.ShouldBeNull();
        options.LoadingText.ShouldBeNull();
        options.CountFormat.ShouldBeNull();
        options.SelectionFormat.ShouldBeNull();
    }

    /// <summary>Verifies invalid option assignments leave the previously committed values intact.</summary>
    [Fact]
    public void Properties_WhenOptionsAreInvalid_ThrowBeforeMutation()
    {
        var options = new FilePickerOptions
        {
            Filters = [new FilePickerFilter("Sources", "*.cs"), FilePickerFilter.AllFiles],
            FilterIndex = 1,
            MaxVisibleRows = 7,
            ParentDirectoryText = "«",
            DirectoryPlaceholder = "Ruta",
            ShowHiddenText = "Mostrar",
            CancelText = "Salir",
            OpenText = "Elegir"
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
        _ = Should.Throw<ArgumentNullException>(() => options.ParentDirectoryText = null!);
        _ = Should.Throw<ArgumentNullException>(() => options.DirectoryPlaceholder = null!);
        _ = Should.Throw<ArgumentNullException>(() => options.ShowHiddenText = null!);
        _ = Should.Throw<ArgumentNullException>(() => options.CancelText = null!);

        options.Title.ShouldBe("Open File");
        options.InitialDirectory.ShouldBe(Environment.CurrentDirectory);
        options.Filters.Count.ShouldBe(2);
        options.FilterIndex.ShouldBe(1);
        options.MaxVisibleRows.ShouldBe(7);
        options.ParentDirectoryText.ShouldBe("«");
        options.DirectoryPlaceholder.ShouldBe("Ruta");
        options.ShowHiddenText.ShouldBe("Mostrar");
        options.CancelText.ShouldBe("Salir");
        options.OpenText.ShouldBe("Elegir");
    }

    /// <summary>Verifies the boolean and style-slot properties round-trip through their own getters,
    /// including the aggregate <see cref="FilePickerOptions.Style"/> the ownership-transfer test
    /// below deliberately omits from its style-slot coverage.</summary>
    [Fact]
    public void Properties_WhenSetToValidValues_RoundTrip()
    {
        var style = FilePickerDialogStyle.Default with { RootPadding = new Thickness(2) };

        var options = new FilePickerOptions
        {
            AllowMultiple = true,
            SelectionMode = FileSelectionMode.FilesAndDirectories,
            ShowHidden = true,
            Style = style
        };

        options.AllowMultiple.ShouldBeTrue();
        options.SelectionMode.ShouldBe(FileSelectionMode.FilesAndDirectories);
        options.ShowHidden.ShouldBeTrue();
        options.Style.ShouldBe(style);

        var copy = options.Copy();
        copy.SelectionMode.ShouldBe(FileSelectionMode.FilesAndDirectories);
    }

    /// <summary>Verifies the status texts and formatters round-trip through their own getters and
    /// survive Copy(), matching how ShowAsync's copied snapshot must carry them to the constructed
    /// dialog.</summary>
    [Fact]
    public void Properties_WhenStatusTextsAndFormattersAreSet_RoundTripAndSurviveCopy()
    {
        string CountFormat(int folders, int files) => $"{folders}f/{files}d";
        string SelectionFormat(int count) => $"chosen: {count}";

        var options = new FilePickerOptions
        {
            ReadyText = "Listo",
            LoadingText = "Cargando…",
            CountFormat = CountFormat,
            SelectionFormat = SelectionFormat
        };

        var copy = options.Copy();

        options.ReadyText.ShouldBe("Listo");
        options.LoadingText.ShouldBe("Cargando…");
        options.CountFormat.ShouldBe(CountFormat);
        options.SelectionFormat.ShouldBe(SelectionFormat);
        copy.ReadyText.ShouldBe("Listo");
        copy.LoadingText.ShouldBe("Cargando…");
        copy.CountFormat.ShouldBe(CountFormat);
        copy.SelectionFormat.ShouldBe(SelectionFormat);
    }
}
