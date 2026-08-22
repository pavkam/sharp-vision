// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates loading baseline and extended Markdown files into Document.</summary>
internal sealed class MarkdownPane: CompositeControlBase
{
    /// <summary>Initializes the Markdown examples.</summary>
    internal MarkdownPane() => InitializeContent(CreateContent());

    /// <summary>Gets the exact catalog and page name.</summary>
    internal const string Title = "Markdown";

    private static DocPage CreateContent()
    {
        var commonMark = Load("commonmark.md", new MarkdownDocumentReader());
        var extended = Load(
            "extensions.md",
            new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.All }));

        const string baselineRecipe = """
            var markdown = await File.ReadAllTextAsync("README.md");
            var document = new Document();
            document.Load(markdown, new MarkdownDocumentReader());
            """;
        const string extensionRecipe = """
            var reader = new MarkdownDocumentReader(new MarkdownOptions
            {
                Extensions = MarkdownExtension.GitHubFlavored |
                             MarkdownExtension.WikiLinks |
                             MarkdownExtension.Callouts |
                             MarkdownExtension.RadioLists
            });
            document.Load(markdown, reader);
            """;

        return new DocPage(
            Title,
            "Loads Markdown through <info>IDocumentFormatReader</info> into the same flowing, " +
            "interactive <info>Document</info> used for programmatic forms.",
            new DocSection(
                "MD",
                "CommonMark",
                "All baseline formatting is represented: six heading levels, emphasis, strong text, inline and fenced code, links, soft and hard breaks, lists, quotes, and rules.",
                new DocExample(
                    "An embedded Markdown file",
                    "This page reads a real .md resource and replaces the Document tree in one operation.",
                    commonMark,
                    baselineRecipe)),
            new DocSection(
                "+",
                "Optional extensions",
                "Every extension is represented, including a table with left, center, and right alignment plus tasks, radios, strikethrough, autolinks, wiki links, and callouts.",
                new DocExample(
                    "GFM and knowledge-base syntax",
                    "The checkboxes and radio choices below are genuine retained controls, not painted imitations.",
                    extended,
                    extensionRecipe)));
    }

    private static Document Load(string name, MarkdownDocumentReader reader)
    {
        var resource = FormattableString.Invariant($"SharpVision.Showcase.Markdown.{name}");
        using var stream = typeof(MarkdownPane).Assembly.GetManifestResourceStream(resource) ??
            throw new InvalidOperationException($"Embedded Markdown resource '{resource}' was not found.");
        using var text = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var document = new Document
        {
            Height = Length.Cells(18),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _ = document.Load(text.ReadToEnd(), reader);
        return document;
    }
}
