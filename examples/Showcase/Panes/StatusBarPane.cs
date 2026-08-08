// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Documents StatusBar edge groups, contextual state, and explicitly interactive content.</summary>
internal sealed class StatusBarPane: CompositeControlBase
{
    /// <summary>Initializes the retained status-bar documentation page.</summary>
    internal StatusBarPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "StatusBar";

    private static DocPage CreateContent()
    {
        var workspace = new StatusBarWorkspace();

        return new DocPage(
            Title,
            "<info>StatusBar</info> anchors live application context to the bottom edge without interrupting the primary workspace.",
            new DocSection(
                "🖥️",
                "Professional editor workspace",
                "This is the scale a status bar is designed for: one persistent strip spanning an application surface, with activity and branch state on the left and precise document context on the right.",
                new DocExample(
                    "Live application status",
                    "Move the pointer across the editor to update <info>Mouse</info> coordinates. The leading Spinner advances independently; mixed Unicode separators divide trailing context, and the retained <info>CheckBox</info> toggles autosave status through ordinary input.",
                    workspace,
                    "var status = new StatusBar();\nstatus.Items.Add(new StatusBarItem\n{\n    Content = new Stack\n    {\n        Orientation = Orientation.Horizontal,\n        Children = { new Spinner(), new Text(\"Indexing\") }\n    }\n});\nstatus.Items.Add(new StatusBarItem\n{\n    Alignment = StatusBarItemAlignment.Right,\n    Content = new CheckBox { Text = \"Autosave\" }\n});\nstatus.Items.Add(new StatusBarItem\n{\n    Alignment = StatusBarItemAlignment.Right,\n    ShowLeftSeparator = true,\n    Content = pointerStatus\n});\nDock.SetSide(status, DockSide.Bottom);")));
    }
}
