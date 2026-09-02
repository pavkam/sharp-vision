// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents deterministic single-file, multiple-file, and directory selection.</summary>
internal sealed class OpenFilePickerPane: CompositeControlBase
{
    /// <summary>Initializes the retained open-file picker showcase content.</summary>
    internal OpenFilePickerPane() => InitializeContent(CreateContent());

    /// <summary>Gets the exact catalog and page name.</summary>
    internal const string Title = "OpenFilePicker";

    private static DocPage CreateContent()
    {
        var directory = PrepareDirectory();
        var singleStatus = new Text("Result: no single-file picker opened") { Overflow = Overflow.Wrap };
        var single = CreateLauncher(
            singleStatus,
            directory,
            "&Open one file",
            "Open one file",
            FileSelectionMode.Files,
            allowMultiple: false,
            showHidden: false);
        var directoryStatus = new Text("Result: no directory picker opened") { Overflow = Overflow.Wrap };
        var folder = CreateLauncher(
            directoryStatus,
            directory,
            "Choose a &directory",
            "Choose a directory",
            FileSelectionMode.Directories,
            allowMultiple: false,
            showHidden: false);
        var multipleStatus = new Text("Result: no multiple-file picker opened") { Overflow = Overflow.Wrap };
        var multiple = CreateLauncher(
            multipleStatus,
            directory,
            "Open &multiple files",
            "Open multiple files",
            FileSelectionMode.Files,
            allowMultiple: true,
            showHidden: true);

        return new DocPage(
            Title,
            "<info>FilePickerDialog</info> selects existing files or directories in one responsive modal browser. This page keeps open-file tasks separate from save-path and overwrite decisions.",
            new DocSection(
                "📂",
                "Open existing paths",
                "The launchers share one deterministic sample directory. Single-file, multiple-file, and directory modes retain their own selection and acceptance rules.",
                new DocExample(
                    "Single-file selection",
                    "Open one existing file. Filters, navigation, selection, Open, Cancel, and the final immutable result use the real dialog.",
                    new DocColumn(single, singleStatus),
                    "var result = await FilePickerDialog.ShowAsync(owner, new FilePickerOptions\n" +
                    "{\n" +
                    "    InitialDirectory = sampleDirectory,\n" +
                    "    Filters = [new FilePickerFilter(\"Documents\", \"*.md\", \"*.txt\")],\n" +
                    "});"),
                new DocExample(
                    "Directory selection",
                    "Choose reports or source as a directory result instead of navigating into it.",
                    new DocColumn(folder, directoryStatus),
                    "var options = new FilePickerOptions\n" +
                    "{\n" +
                    "    SelectionMode = FileSelectionMode.Directories\n" +
                    "};"),
                new DocExample(
                    "Multiple-file selection",
                    "Select multiple source or document files with Control or Shift. Hidden entries begin visible for this variant.",
                    new DocColumn(multiple, multipleStatus),
                    "var options = new FilePickerOptions\n" +
                    "{\n" +
                    "    AllowMultiple = true,\n" +
                    "    ShowHidden = true\n" +
                    "};")));
    }

    private static Button CreateLauncher(
        Text status,
        string directory,
        string text,
        string title,
        FileSelectionMode selectionMode,
        bool allowMultiple,
        bool showHidden)
    {
        var launcher = new Button { Text = text };
        launcher.Click += async (_, _) =>
        {
            var result = await FilePickerDialog.ShowAsync(
                launcher,
                new FilePickerOptions
                {
                    Title = title,
                    InitialDirectory = directory,
                    SelectionMode = selectionMode,
                    AllowMultiple = allowMultiple,
                    ShowHidden = showHidden,
                    Filters =
                    [
                        new FilePickerFilter("C# source", "*.cs", "*.csx"),
                        new FilePickerFilter("Documents", "*.md", "*.txt"),
                        FilePickerFilter.AllFiles
                    ]
                });
            ShowcasePaneHelpers.PostStatus(
                status,
                "FilePickerDialog",
                () => status.Content = FormatResult(result));
        };
        return launcher;
    }

    private static string FormatResult(FilePickerResult result)
    {
        if (!result.IsAccepted)
        {
            return "Result: cancelled";
        }

        var names = result.Paths.Select(Path.GetFileName);
        return $"Result: {result.Paths.Count} selected · {string.Join(", ", names)}";
    }

    private static string PrepareDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "SharpVision.Showcase", "FilePicker", "Open");
        _ = Directory.CreateDirectory(root);
        _ = Directory.CreateDirectory(Path.Combine(root, "reports"));
        _ = Directory.CreateDirectory(Path.Combine(root, "source"));
        File.WriteAllText(Path.Combine(root, "README.md"), "# Sample\n");
        File.WriteAllText(Path.Combine(root, "Program.cs"), "// Sample\n");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "Sample notes\n");
        File.WriteAllText(Path.Combine(root, ".hidden.md"), "# Hidden sample\n");
        return root;
    }
}
