// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;


using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents live single- and multiple-selection FilePickerDialog presentation.</summary>
internal sealed class FilePickerPane: CompositeControlBase
{
    /// <summary>Initializes the retained FilePicker showcase content.</summary>
    internal FilePickerPane() => InitializeContent(CreateContent());

    /// <summary>Gets the exact catalog/page name.</summary>
    internal const string Title = "FilePicker";

    private static Dock CreateContent()
    {
        var status = new Text("Result: no picker opened");
        var stage = new Overlay
        {
            Width = Length.Cells(78),
            Height = Length.Cells(28),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children =
            {
                new Text(
                    "Project workspace\n\n" +
                    "Open a picker, navigate with Enter or Backspace, switch filters, and toggle hidden entries.\n\n" +
                    "The workspace remains inert while the dialog owns modality.")
                {
                    Margin = new Thickness(2, 2),
                    Overflow = Overflow.Wrap,
                    IsHitTestVisible = false
                }
            }
        };
        var single = CreateLauncher(status, "&Open one file", allowMultiple: false, showHidden: false);
        var multiple = CreateLauncher(status, "Open &multiple files", allowMultiple: true, showHidden: true);
        var save = CreateSaveLauncher(status);
        var launchers = Doc.Row(single, multiple, save);
        launchers.Spacing = 1;

        return Doc.Page(
            Title,
            "<info>FilePickerDialog</info> chooses existing local files through a responsive modal Window with typed filters, directory navigation, multiple selection, and hidden-entry visibility.",
            Doc.Section(
                "📂",
                "Browse and choose files",
                "The picker composes Grid, ListView, TextInput, ComboBox, CheckBox, and Button controls while retaining one modal Window and dispatcher-safe asynchronous enumeration.",
                Doc.Example(
                    "Single, multiple, and save selection",
                    "Open either variant. Entries are directories first and name-sorted; refreshes retain an existing selected path. Use ↑, the location bar, filters, Show hidden, pointer selection, Control/Shift ranges, Enter, Backspace, Open/Save, or Cancel.",
                    Doc.Column(launchers, stage, status),
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
            var dispatcher = status.Dispatcher ?? throw new InvalidOperationException(
                "The showcase status must remain attached while the FilePickerDialog is open.");
            dispatcher.Post(() => status.Content = FormatResult(result));
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
            var dispatcher = status.Dispatcher ?? throw new InvalidOperationException(
                "The showcase status must remain attached while the SaveFileDialog is open.");
            dispatcher.Post(() => status.Content = FormatSaveResult(result));
        };
        return launcher;
    }

    private static string FormatSaveResult(SaveFileResult result) =>
        result.Confirmed
            ? $"Result: saved to {Path.GetFileName(result.Path)}"
            : "Result: cancelled";

    private static string FormatResult(FilePickerResult result)
    {
        if (!result.Accepted)
        {
            return "Result: cancelled";
        }

        var names = result.Paths.Select(Path.GetFileName);
        return $"Result: {result.Paths.Count} selected · {string.Join(", ", names)}";
    }
}
