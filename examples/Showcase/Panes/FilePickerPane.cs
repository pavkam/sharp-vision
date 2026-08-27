// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;


using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents live single- and multiple-selection FilePickerDialog presentation, plus SaveFileDialog's save-path and overwrite-confirmation flow.</summary>
internal sealed class FilePickerPane: CompositeControlBase
{
    /// <summary>Initializes the retained FilePicker showcase content.</summary>
    internal FilePickerPane() => InitializeContent(CreateContent());

    /// <summary>Gets the exact catalog/page name.</summary>
    internal const string Title = "FilePicker";

    private static DocPage CreateContent()
    {
        var status = new Text("Result: no picker opened");
        var single = CreateLauncher(status, "&Open one file", allowMultiple: false, showHidden: false);
        var multiple = CreateLauncher(status, "Open &multiple files", allowMultiple: true, showHidden: true);
        var save = CreateSaveLauncher(status);
        var localized = CreateLocalizedLauncher(status);
        var launchers = new DocRow(single, multiple, save, localized) { Spacing = 1 };

        return new DocPage(
            Title,
            "<info>FilePickerDialog</info> chooses existing local files through a responsive modal Window with typed filters, directory navigation, multiple selection, and hidden-entry visibility. <info>SaveFileDialog</info> chooses one canonical path for a later save through the same browser, confirming with a MessageBox before overwriting an existing file.",
            new DocSection(
                "📂",
                "Browse and choose files",
                "The picker composes Grid, ListView, TextInput, ComboBox, CheckBox, and Button controls while retaining one modal Window and dispatcher-safe asynchronous enumeration. \"Localized (options)\" demonstrates FilePickerDialogStyle and configurable captions - a themed frame and localized action text configured through one FilePickerOptions snapshot.",
                new DocExample(
                    "Single, multiple, save, and localized selection",
                    "Open either variant. Entries are directories first and name-sorted; refreshes retain an existing selected path. Use ↑, the location bar, filters, Show hidden, pointer selection, Control/Shift ranges, Enter, Backspace, Open/Save, or Cancel.",
                    new DocColumn(launchers, status),
                    "var result = await FilePickerDialog.ShowAsync(owner, new FilePickerOptions\n{\n    AllowMultiple = true,\n    MaxVisibleRows = 20,\n    Filters = [new FilePickerFilter(\"Sources\", \"*.cs\"), FilePickerFilter.AllFiles],\n});")));
    }

    private static Button CreateLauncher(
        Text status,
        string label,
        bool allowMultiple,
        bool showHidden)
    {
        var launcher = new Button { Text = label };
        launcher.Click += async (_, _) =>
        {
            var result = await FilePickerDialog.ShowAsync(
                launcher,
                new FilePickerOptions
                {
                    Title = allowMultiple ? "Open files" : "Open a file",
                    InitialDirectory = Environment.CurrentDirectory,
                    AllowMultiple = allowMultiple,
                    ShowHidden = showHidden,
                    Filters =
                    [
                        new FilePickerFilter("C# source", "*.cs", "*.csx"),
                        new FilePickerFilter("Documents", "*.md", "*.txt"),
                        FilePickerFilter.AllFiles
                    ]
                });
            ShowcasePaneHelpers.PostStatus(status, "FilePickerDialog", () => status.Content = FormatResult(result));
        };
        return launcher;
    }

    private static Button CreateSaveLauncher(Text status)
    {
        var launcher = new Button { Text = "&Save a file" };
        launcher.Click += async (_, _) =>
        {
            var result = await SaveFileDialog.ShowAsync(
                launcher,
                new SaveFileOptions
                {
                    Title = "Save a file",
                    InitialDirectory = Environment.CurrentDirectory,
                    Filters =
                    [
                        new FilePickerFilter("C# source", "*.cs", "*.csx"),
                        new FilePickerFilter("Documents", "*.md", "*.txt"),
                        FilePickerFilter.AllFiles
                    ]
                });
            ShowcasePaneHelpers.PostStatus(status, "SaveFileDialog", () => status.Content = FormatSaveResult(result));
        };
        return launcher;
    }

    private static Button CreateLocalizedLauncher(Text status)
    {
        var launcher = new Button { Text = "&Localized (options)" };
        launcher.Click += async (_, _) =>
        {
            var result = await FilePickerDialog.ShowAsync(
                launcher,
                new FilePickerOptions
                {
                    Title = "Abrir archivo",
                    InitialDirectory = Environment.CurrentDirectory,
                    ParentDirectoryText = "«",
                    DirectoryPlaceholder = "Ruta del directorio",
                    ShowHiddenText = "Mostrar &ocultos",
                    CancelText = "&Cancelar",
                    OpenText = "&Abrir",
                    Style = FilePickerDialogStyle.Default with { ContentSpacing = 2 }
                });
            ShowcasePaneHelpers.PostStatus(status, "FilePickerDialog", () => status.Content = FormatResult(result));
        };
        return launcher;
    }

    private static string FormatSaveResult(SaveFileResult result) =>
        result.IsConfirmed
            ? $"Result: saved to {Path.GetFileName(result.Path)}"
            : "Result: cancelled";

    private static string FormatResult(FilePickerResult result)
    {
        if (!result.IsAccepted)
        {
            return "Result: cancelled";
        }

        var names = result.Paths.Select(Path.GetFileName);
        return $"Result: {result.Paths.Count} selected · {string.Join(", ", names)}";
    }
}
