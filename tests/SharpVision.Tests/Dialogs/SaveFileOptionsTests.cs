// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies save-file dialog options reject invalid state at their public boundary.</summary>
public sealed class SaveFileOptionsTests
{
    /// <summary>Verifies a null filename is rejected before it can replace the current value.</summary>
    [Fact]
    public void InitialFileName_WhenNull_ThrowsArgumentNullExceptionWithoutChangingValue()
    {
        var options = new SaveFileOptions { InitialFileName = "report.csv" };

        _ = Should.Throw<ArgumentNullException>(() => options.InitialFileName = null!);

        options.InitialFileName.ShouldBe("report.csv");
    }

    /// <summary>Verifies options expose useful defaults and copy caller-owned filters, mirroring
    /// FilePickerOptionsTests' equivalent construction defaults coverage.</summary>
    [Fact]
    public void Constructor_WhenOptionsAreCreated_UsesDefaultsAndOwnsFilters()
    {
        // Arrange
        var filters = new[]
        {
            new FilePickerFilter("CSV", "*.csv"),
            FilePickerFilter.AllFiles
        };
        var options = new SaveFileOptions
        {
            // Act
            Filters = filters
        };
        filters[0] = FilePickerFilter.AllFiles;

        // Assert
        options.Title.ShouldBe("Save As");
        options.InitialDirectory.ShouldBe(Environment.CurrentDirectory);
        options.InitialFileName.ShouldBe(string.Empty);
        options.ConfirmOverwrite.ShouldBeTrue();
        options.ShowHidden.ShouldBeFalse();
        options.MaxVisibleRows.ShouldBe(12);
        options.FilterIndex.ShouldBe(0);
        options.Filters[0].Name.ShouldBe("CSV");
        options.Style.ShouldBeNull();
        options.OverwriteStyle.ShouldBeNull();
        options.ParentDirectoryText.ShouldBe("↑");
        options.DirectoryPlaceholder.ShouldBe("Directory path");
        options.ShowHiddenText.ShouldBe("Show &hidden");
        options.CancelText.ShouldBe("&Cancel");
        options.SaveText.ShouldBe("&Save");
        options.FileNameLabel.ShouldBe("Name:");
        options.FileNamePlaceholder.ShouldBe("File name");
        options.OverwriteTitle.ShouldBe("Confirm Save As");
        options.OverwriteYesText.ShouldBe("&Yes");
        options.OverwriteNoText.ShouldBe("&No");
        options.ReadyText.ShouldBeNull();
        options.LoadingText.ShouldBeNull();
        options.CountFormat.ShouldBeNull();
        options.OverwriteMessageFormat.ShouldBeNull();
    }

    /// <summary>Verifies invalid option assignments leave the previously committed values intact.</summary>
    [Fact]
    public void Properties_WhenOptionsAreInvalid_ThrowBeforeMutation()
    {
        var options = new SaveFileOptions
        {
            Filters = [new FilePickerFilter("CSV", "*.csv"), FilePickerFilter.AllFiles],
            FilterIndex = 1,
            MaxVisibleRows = 7,
            ParentDirectoryText = "«",
            DirectoryPlaceholder = "Ruta",
            ShowHiddenText = "Mostrar",
            CancelText = "Salir",
            SaveText = "Guardar",
            FileNameLabel = "Archivo:",
            FileNamePlaceholder = "Nombre",
            OverwriteTitle = "¿Reemplazar?",
            OverwriteYesText = "Sí",
            OverwriteNoText = "No"
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
        _ = Should.Throw<ArgumentNullException>(() => options.SaveText = null!);
        _ = Should.Throw<ArgumentNullException>(() => options.FileNameLabel = null!);
        _ = Should.Throw<ArgumentNullException>(() => options.FileNamePlaceholder = null!);
        _ = Should.Throw<ArgumentNullException>(() => options.OverwriteTitle = null!);
        _ = Should.Throw<ArgumentNullException>(() => options.OverwriteYesText = null!);
        _ = Should.Throw<ArgumentNullException>(() => options.OverwriteNoText = null!);

        options.Title.ShouldBe("Save As");
        options.InitialDirectory.ShouldBe(Environment.CurrentDirectory);
        options.Filters.Count.ShouldBe(2);
        options.FilterIndex.ShouldBe(1);
        options.MaxVisibleRows.ShouldBe(7);
        options.ParentDirectoryText.ShouldBe("«");
        options.DirectoryPlaceholder.ShouldBe("Ruta");
        options.ShowHiddenText.ShouldBe("Mostrar");
        options.CancelText.ShouldBe("Salir");
        options.SaveText.ShouldBe("Guardar");
        options.FileNameLabel.ShouldBe("Archivo:");
        options.FileNamePlaceholder.ShouldBe("Nombre");
        options.OverwriteTitle.ShouldBe("¿Reemplazar?");
        options.OverwriteYesText.ShouldBe("Sí");
        options.OverwriteNoText.ShouldBe("No");
    }

    /// <summary>Verifies the boolean and style-slot properties round-trip through their own
    /// getters, including the aggregate <see cref="SaveFileOptions.Style"/> and
    /// <see cref="SaveFileOptions.OverwriteStyle"/> the ownership-transfer test in
    /// SaveFileDialogTests deliberately omits from its style-slot coverage.</summary>
    [Fact]
    public void Properties_WhenSetToValidValues_RoundTrip()
    {
        var style = SaveFileDialogStyle.Default with { RootPadding = new Thickness(2) };
        var overwriteStyle = MessageBoxStyle.Default with { ActionBarMargin = new Thickness(3, 0) };

        var options = new SaveFileOptions
        {
            ConfirmOverwrite = false,
            ShowHidden = true,
            Style = style,
            OverwriteStyle = overwriteStyle
        };

        options.ConfirmOverwrite.ShouldBeFalse();
        options.ShowHidden.ShouldBeTrue();
        options.Style.ShouldBe(style);
        options.OverwriteStyle.ShouldBe(overwriteStyle);
    }

    /// <summary>Verifies the status texts and formatters round-trip through their own getters and
    /// survive Copy(), matching how ShowAsync's copied snapshot must carry them to the constructed
    /// dialog.</summary>
    [Fact]
    public void Properties_WhenStatusTextsAndFormattersAreSet_RoundTripAndSurviveCopy()
    {
        string CountFormat(int folders, int files) => $"{folders}f/{files}d";
        string OverwriteMessageFormat(string fileName) => $"Replace {fileName}?";

        var options = new SaveFileOptions
        {
            ReadyText = "Listo",
            LoadingText = "Cargando…",
            CountFormat = CountFormat,
            OverwriteMessageFormat = OverwriteMessageFormat
        };

        var copy = options.Copy();

        options.ReadyText.ShouldBe("Listo");
        options.LoadingText.ShouldBe("Cargando…");
        options.CountFormat.ShouldBe((Func<int, int, string>) CountFormat);
        options.OverwriteMessageFormat.ShouldBe((Func<string, string>) OverwriteMessageFormat);
        copy.ReadyText.ShouldBe("Listo");
        copy.LoadingText.ShouldBe("Cargando…");
        copy.CountFormat.ShouldBe((Func<int, int, string>) CountFormat);
        copy.OverwriteMessageFormat.ShouldBe((Func<string, string>) OverwriteMessageFormat);
    }
}
