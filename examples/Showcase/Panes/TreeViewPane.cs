// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

internal sealed class TreeViewPane: CompositeControlBase
{
    internal TreeViewPane() => InitializeContent(CreateContent());
    internal const string Title = "TreeView";

    private static DocPage CreateContent()
    {
        var status = new Text("Selected: (none)");

        var fileTree = new TreeView
        {
            Width = Length.Cells(34),
            Height = Length.Cells(12)
        };
        var documents = new TreeViewItem("Documents");
        var reports = new TreeViewItem("Reports");
        reports.Children.Add(new TreeViewItem("Q1 Report.txt"));
        reports.Children.Add(new TreeViewItem("Q2 Report.txt"));
        reports.Children.Add(new TreeViewItem("Q3 Report.txt"));
        documents.Children.Add(reports);
        documents.Children.Add(new TreeViewItem("Notes.md"));
        documents.Children.Add(new TreeViewItem("README.md"));
        var images = new TreeViewItem("Images");
        images.Children.Add(new TreeViewItem("photo.jpg"));
        images.Children.Add(new TreeViewItem("logo.png"));
        var config = new TreeViewItem("Config");
        config.Children.Add(new TreeViewItem("settings.json"));
        fileTree.Items.Add(documents);
        fileTree.Items.Add(images);
        fileTree.Items.Add(config);
        fileTree.SelectionChanged += (_, _) =>
            status.Content = $"Selected: {fileTree.SelectedItem?.Header ?? "(none)"}";

        var controlledTree = new TreeView
        {
            Width = Length.Cells(34),
            Height = Length.Cells(10),
            SelectionMode = TreeSelectionMode.Multiple
        };
        var root = new TreeViewItem("Project") { Checkable = true };
        var src = new TreeViewItem("src") { Checkable = true };
        src.Children.Add(new TreeViewItem("App.cs") { Checkable = true });
        src.Children.Add(new TreeViewItem("Program.cs") { Checkable = true });
        src.Children.Add(new TreeViewItem("Startup.cs") { Checkable = true });
        var tests = new TreeViewItem("tests") { Checkable = true };
        tests.Children.Add(new TreeViewItem("AppTests.cs") { Checkable = true });
        tests.Children.Add(new TreeViewItem("IntegrationTests.cs") { Checkable = true });
        root.Children.Add(src);
        root.Children.Add(tests);
        root.Children.Add(new TreeViewItem("README.md"));
        root.Children.Add(new TreeViewItem(".gitignore"));
        controlledTree.Items.Add(root);

        var controlledStatus = new Text("Selected: none");
        controlledTree.SelectionChanged += (_, _) => controlledStatus.Content =
            controlledTree.SelectedItems.Count == 0
                ? "Selected: none"
                : $"Selected: {string.Join(", ", controlledTree.SelectedItems.Select(item => item.Header))}";

        var expandAll = new Button("&Expand All");
        expandAll.Click += (_, _) => controlledTree.ExpandAll();
        var collapseAll = new Button("&Collapse All");
        collapseAll.Click += (_, _) => controlledTree.CollapseAll();
        var selectAll = new Button("Select &All");
        selectAll.Click += (_, _) => controlledTree.SelectAll();
        var clearSelection = new Button("Clear &Selection");
        clearSelection.Click += (_, _) => controlledTree.ClearSelection();

        var markTrees = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children =
            {
                MarkSpecimen("Square", CheckMark.Square),
                MarkSpecimen("Brackets", CheckMark.Brackets),
                MarkSpecimen("Tick", CheckMark.Tick)
            }
        };

        const string recipe = """
            var tree = new TreeView { Width = Length.Cells(34) };

            var docs = new TreeViewItem("Documents");
            var reports = new TreeViewItem("Reports");
            reports.Children.Add(new TreeViewItem("Q1 Report.txt"));
            reports.Children.Add(new TreeViewItem("Q2 Report.txt"));
            docs.Children.Add(reports);
            docs.Children.Add(new TreeViewItem("Notes.md"));

            var images = new TreeViewItem("Images");
            images.Children.Add(new TreeViewItem("photo.jpg"));
            images.Children.Add(new TreeViewItem("logo.png"));

            tree.Items.Add(docs);
            tree.Items.Add(images);

            tree.SelectionChanged += (_, _) =>
                Console.WriteLine($"Selected: {tree.SelectedItem?.Header}");
            """;

        return new DocPage(Title,
            "<info>TreeView</info> displays hierarchical data as an expandable and collapsible tree " +
            "with keyboard navigation, pointer interaction, single or multiple selection, " +
            "and optional checkable nodes.",
            new DocSection("\U0001f333", "File tree",
                "A file-system-like tree with nested folders and files. Click the disclosure " +
                "glyph (▶/▼) to expand or collapse a branch. Click an item to select it. " +
                "Use ↑↓ to navigate, ← to collapse or go to parent, → to expand or enter children, " +
                "and Enter to activate.",
                new DocExample("Documents, images, and config",
                    "Navigate with keyboard arrows after Tab. Right expands, Left collapses or moves to parent.",
                    new DocColumn(fileTree, status),
                    recipe)),
            new DocSection("\U0001f39b️", "Programmatic control",
                "Use <info>ExpandAll</info> and <info>CollapseAll</info> to control the entire tree at once.",
                new DocExample("Project tree with buttons",
                "Click the buttons to expand or collapse all nodes programmatically. Use Control-click, " +
                "Shift-click, or Space to exercise multiple selection and checking.",
                    new DocColumn(controlledTree, controlledStatus, new Stack
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 1,
                        Children = { expandAll, collapseAll, selectAll, clearSelection }
                    }))),
            new DocSection("\u2611\ufe0f", "Check mark families",
                "<info>CheckMark</info> selects the same mark layouts a standalone " +
                "<info>CheckBox</info> offers. Set it on the tree to share one presentation, or on " +
                "a single item to override it. Brackets reserve three cells and shift the header " +
                "accordingly; every cell of the mark is clickable.",
                new DocExample("Square, Brackets, and Tick",
                    "Each tree checks the same nodes. Click any mark cell to toggle it.",
                    markTrees)));
    }

    private static DocColumn MarkSpecimen(string title, CheckMark mark)
    {
        var tree = new TreeView
        {
            Width = Length.Cells(22),
            Height = Length.Cells(5),
            CheckMark = mark
        };
        var parent = new TreeViewItem("Group") { Checkable = true, Expanded = true };
        parent.Children.Add(new TreeViewItem("Checked") { Checkable = true, IsChecked = true });
        parent.Children.Add(new TreeViewItem("Unchecked") { Checkable = true });
        tree.Items.Add(parent);

        return new DocColumn(new Text(title), tree);
    }
}
