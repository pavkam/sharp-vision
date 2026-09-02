// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents JsonView with a live syntax-colored and expandable document.</summary>
internal sealed class JsonViewPane: CompositeControlBase
{
    /// <summary>Initializes the live JSON viewer documentation page.</summary>
    internal JsonViewPane() => InitializeContent(CreateContent());

    /// <summary>Gets the exact catalog and page name.</summary>
    internal const string Title = "JsonView";

    private static DocPage CreateContent()
    {
        const string document = /*lang=json,strict*/ """
            {
              "title": "SharpVision",
              "description": "SharpVision is a deterministic .NET terminal UI library built for Unicode fidelity, precise cell geometry, responsive retained controls, and reliable keyboard and pointer interaction across modern terminal emulators.",
              "tags": ["terminal", "dotnet", "unicode", "界"],
              "release": {
                "version": "0.8.0-alpha.3",
                "channel": "preview",
                "runtime": {
                  "framework": ".NET 10",
                  "platforms": ["Linux", "macOS", "Windows"],
                  "capabilities": {
                    "colorDepth": 24,
                    "unicode": {
                      "graphemes": true,
                      "ambiguousWidth": "terminal"
                    },
                    "input": ["keyboard", "mouse", "paste"]
                  }
                }
              },
              "contributors": [
                {
                  "name": "Alex",
                  "名前": "アレックス",
                  "roles": ["maintainer", "design"],
                  "contact": {
                    "email": "alex@example.com",
                    "timezone": "Europe/Lisbon"
                  }
                },
                {
                  "name": "Ada",
                  "roles": ["testing", "documentation"],
                  "active": true
                }
              ],
              "metrics": {
                "tests": 5611,
                "coverage": 0.91,
                "lastRelease": null
              }
            }
            """;
        var status = new Text("Selected: /title");
        var json = new JsonView
        {
            Json = document,
            Height = Length.Cells(20),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarStyle = ScrollBarStyle.ThinLine
        };
        _ = json.SetExpanded("/tags", false);
        json.SelectionChanged += (_, eventArgs) =>
            status.Content = $"Selected: {eventArgs.Path ?? "(none)"}";
        var expand = new Button("&Expand all");
        expand.Click += (_, _) => json.ExpandAll();
        var collapse = new Button("&Collapse all");
        collapse.Click += (_, _) => json.CollapseAll();

        const string recipe = """
            var json = new JsonView
            {
                Json = document,
                ScrollBars = ScrollBars.Both
            };

            json.SelectionChanged += (_, e) =>
                Console.WriteLine(e.Path);
            """;

        var example = new DocExample(
            "Deep objects, arrays, Unicode, and long scalar values",
            "The long description wraps within the viewport. The tags array starts collapsed, while the expanded release and contributor branches demonstrate several nesting levels. Click a key to select it or its disclosure glyph to toggle the branch.",
            new DocColumn(json, status, new DocRow(expand, collapse)),
            recipe)
        {
            Width = Length.Percent(50)
        };

        return new DocPage(
            Title,
            "<info>JsonView</info> displays JSON as a syntax-colored tree with key-level selection, disclosure, and two-axis scrolling.",
            new DocSection(
                "{…}",
                "Structured JSON",
                "Use ↑↓ to move between visible keys and indices, ←→ to collapse or enter branches, and Enter or Space to toggle the selected container.",
                example));
    }
}
