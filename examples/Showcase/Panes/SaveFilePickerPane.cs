// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents deterministic new-path and overwrite-confirmation save flows.</summary>
internal sealed class SaveFilePickerPane: CompositeControlBase
{
    /// <summary>Initializes the retained save-file picker showcase content.</summary>
    internal SaveFilePickerPane() => InitializeContent(CreateContent());

    /// <summary>Gets the exact catalog and page name.</summary>
    internal const string Title = "SaveFilePicker";

    private static DocPage CreateContent()
    {
        var directory = PrepareDirectory();
        var saveStatus = new Text("Result: no new-path picker opened") { Overflow = Overflow.Wrap };
        var overwriteStatus = new Text("Result: no overwrite picker opened") { Overflow = Overflow.Wrap };
        var save = CreateLauncher(saveStatus, directory, "&Save new file", "draft.txt");
        var overwrite = CreateLauncher(overwriteStatus, directory, "&Overwrite report", "report.txt");

        return new DocPage(
            Title,
            "<info>SaveFileDialog</info> chooses one canonical path for a later save and confirms before replacing an existing file. Open-file selection remains on its own focused page.",
            new DocSection(
                "💾",
                "Choose a save path",
                "The deterministic sample directory contains report.txt, so the second launcher reaches the real nested overwrite confirmation while the first proposes a new path.",
                new DocExample(
                    "New save path",
                    "Save new file starts with draft.txt, a path absent from the deterministic sample directory.",
                    new DocColumn(save, saveStatus),
                    "var result = await SaveFileDialog.ShowAsync(owner, new SaveFileOptions\n" +
                    "{\n" +
                    "    InitialDirectory = sampleDirectory,\n" +
                    "    InitialFileName = \"draft.txt\"\n" +
                    "});"),
                new DocExample(
                    "Overwrite confirmation",
                    "Overwrite report starts with an existing report.txt and asks for confirmation before returning a confirmed result.",
                    new DocColumn(overwrite, overwriteStatus),
                    "var result = await SaveFileDialog.ShowAsync(owner, new SaveFileOptions\n" +
                    "{\n" +
                    "    InitialDirectory = sampleDirectory,\n" +
                    "    InitialFileName = \"report.txt\",\n" +
                    "    ConfirmOverwrite = true,\n" +
                    "});")));
    }

    private static Button CreateLauncher(
        Text status,
        string directory,
        string text,
        string initialFileName)
    {
        var launcher = new Button { Text = text };
        launcher.Click += async (_, _) =>
        {
            var result = await SaveFileDialog.ShowAsync(
                launcher,
                new SaveFileOptions
                {
                    Title = "Save report",
                    InitialDirectory = directory,
                    InitialFileName = initialFileName,
                    ConfirmOverwrite = true,
                    Filters =
                    [
                        new FilePickerFilter("Documents", "*.md", "*.txt"),
                        FilePickerFilter.AllFiles
                    ]
                });
            ShowcasePaneHelpers.PostStatus(
                status,
                "SaveFileDialog",
                () => status.Content = result.IsConfirmed
                    ? $"Result: saved to {Path.GetFileName(result.Path)}"
                    : "Result: cancelled");
        };
        return launcher;
    }

    private static string PrepareDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "SharpVision.Showcase", "FilePicker", "Save");
        _ = Directory.CreateDirectory(root);
        File.Delete(Path.Combine(root, "draft.txt"));
        File.WriteAllText(Path.Combine(root, "report.txt"), "Existing sample report\n");
        return root;
    }
}
