// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Controls.SyntaxHighlighting;

using Text = SharpVision.Controls.Display.Text;

internal sealed class DocumentPane: CompositeControlBase
{
    internal DocumentPane() => InitializeContent(CreateContent());
    internal const string Title = "Document";

    private static DocPage CreateContent() =>
        new(
            Title,
            "Displays a scrollable tree of rich text content: headings, paragraphs with inline " +
            "markup and activatable links, lists, block quotes, code blocks, and thematic breaks.",
            CreateSelectionSection(),
            CreateTextSection(),
            CreateLinkSection(),
            CreateFormSection(),
            CreateListSection(),
            CreateBlockQuoteSection(),
            CreateCodeSection(),
            CreateFlagshipSection());

    private static DocSection CreateSelectionSection()
    {
        var status = new Text("Selection: collapsed");
        var link = new DocumentLink("browser-like link");
        var button = new Button("&Ship");
        var updates = new CheckBox("&Updates");
        var stable = new RadioButton("&Stable") { GroupName = "selection-channel", IsChecked = true };
        var preview = new RadioButton("&Preview") { GroupName = "selection-channel" };
        var code = new CodeView
        {
            Code = "var selected = document.SelectedText;\nclipboard.Write(selected);",
            Language = "C#",
            Height = Length.Cells(5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var introduction = new DocumentParagraph();
        introduction.Inlines.Add(new DocumentTextRun(
            "Drag from this <b>styled paragraph</b>, through the "));
        introduction.Inlines.Add(link);
        introduction.Inlines.Add(new DocumentTextRun(
            ", controls, and code below. Selection follows semantic text rather than decorative chrome."));

        var document = new Document
        {
            Width = Length.Cells(64),
            Height = Length.Cells(18),
        };
        document.Blocks.Add(new DocumentHeading(2, "One continuous selection"));
        document.Blocks.Add(introduction);
        document.Blocks.Add(new DocumentBlockControl(button));
        document.Blocks.Add(new DocumentBlockControl(updates));
        document.Blocks.Add(new DocumentBlockControl(stable));
        document.Blocks.Add(new DocumentBlockControl(preview));
        document.Blocks.Add(new DocumentBlockControl(code));
        document.Blocks.Add(new DocumentParagraph(
            "Keep dragging beyond the top or bottom edge to autoscroll. Release, then hold Shift " +
            "with arrows, Home, End, Page Up, or Page Down to extend from the active caret."));
        document.Blocks.Add(new DocumentParagraph(
            "Ctrl+A selects the complete semantic stream. Ctrl+C publishes it through the " +
            "Application clipboard route when the terminal supports clipboard writes."));
        document.SelectionChanged += (_, _) =>
            status.Content = document.Selection.IsEmpty
                ? $"Selection: caret {document.Selection.Caret}"
                : $"Selection: {document.Selection.Length} UTF-16 code unit(s)";

        return new DocSection(
            "⌁",
            "Selection across mixed content",
            "A <info>Document</info> owns one browser-like semantic selection across its text, links, " +
            "embedded controls, and <info>CodeView</info> blocks. Drag across the specimen, hold beyond " +
            "an edge to scroll, extend with Shift plus navigation keys, or use Ctrl+A and Ctrl+C.",
            new DocExample(
                "Text, controls, and code in one range",
                "A stationary click still activates the link, button, checkbox, or radio button. " +
                "Moving one cell starts selection instead, cancels that pending activation, and can " +
                "select part of any caption or code line.",
                new DocColumn(document, status),
                """
                var document = new Document();
                document.Blocks.Add(new DocumentParagraph("Select across everything below."));
                document.Blocks.Add(new DocumentBlockControl(new Button("Ship")));
                document.Blocks.Add(new DocumentBlockControl(new CheckBox("Updates")));
                document.Blocks.Add(new DocumentBlockControl(new CodeView
                {
                    Code = source,
                    Language = "C#"
                }));

                document.SelectionChanged += (_, _) => Show(document.SelectedText);
                """));
    }

    private static DocSection CreateTextSection()
    {
        var document = new Document { Width = Length.Cells(64) };
        document.Blocks.Add(new DocumentHeading(1, "SharpVision"));
        document.Blocks.Add(new DocumentParagraph(
            "Build rich terminal apps without giving up <b>Unicode</b>, <i>predictable layout</i>, " +
            "<u>correct</u> terminal behavior, or <s>compromise</s>. Every paragraph flows and " +
            "wraps exactly like <info>Text</info> does."));
        document.Blocks.Add(new DocumentHeading(2, "Weight and color, not size"));
        document.Blocks.Add(new DocumentParagraph(
            "A terminal has no true font size, so a heading's level differentiates through weight, " +
            "color, and underline instead: levels 1 and 2 render with the themed accent heading " +
            "face, shown above, while levels 3 through 6 stay in the plain body face with only " +
            "bold weight added."));
        document.Blocks.Add(new DocumentHeading(3, "Still bold, still readable"));

        return new DocSection(
            "\U0001F4C4",
            "Headings and inline styling",
            "A heading's level (1 through 6) differentiates through weight, color, and underline - " +
            "there is no true font size in a terminal. A paragraph flows the same inline-markup " +
            "syntax <info>Text</info> uses (<info>\\<b></info>, <info>\\<i></info>, <info>\\<u></info>, <info>\\<s></info>).",
            new DocExample(
                "Heading and paragraph",
                "Level 1-2 headings use the themed accent heading face; level 3+ headings render " +
                "bold in the plain body face. Both wrap and space exactly like any other block.",
                document,
                """
                var document = new Document();
                document.Blocks.Add(new DocumentHeading(1, "SharpVision"));
                document.Blocks.Add(new DocumentParagraph(
                    "Build rich terminal apps without giving up <b>Unicode</b>."));
                document.Blocks.Add(new DocumentHeading(2, "Weight and color, not size"));
                document.Blocks.Add(new DocumentParagraph(
                    "Levels 1-2 use the accent heading face; levels 3-6 stay in the body face."));
                document.Blocks.Add(new DocumentHeading(3, "Still bold, still readable"));
                """));
    }

    private static DocSection CreateLinkSection()
    {
        var status = new Text("Clicked: (never)");
        var learnMore = new DocumentLink("learn more");
        var getStarted = new DocumentLink("get started");
        var reference = new DocumentLink("browse the reference", "https://example.invalid/docs");
        learnMore.Clicked += (_, _) => status.Content = "Clicked: learn more";
        getStarted.Clicked += (_, _) => status.Content = "Clicked: get started";
        reference.Clicked += (_, _) => status.Content = "Clicked: browse the reference";

        var paragraph = new DocumentParagraph();
        paragraph.Inlines.Add(new DocumentTextRun(
            "Activatable links sit mid-sentence, exactly like any other word - click "));
        paragraph.Inlines.Add(learnMore);
        paragraph.Inlines.Add(new DocumentTextRun(" to see the design, "));
        paragraph.Inlines.Add(getStarted);
        paragraph.Inlines.Add(new DocumentTextRun(" to try it yourself, or "));
        paragraph.Inlines.Add(reference);
        paragraph.Inlines.Add(new DocumentTextRun(
            " for the full API. A link given a target also emits a real OSC 8 terminal " +
            "hyperlink, so a capable terminal offers its own open-or-copy affordance " +
            "independent of the <info>Clicked</info> handler below. Resize the window to watch " +
            "every link wrap with its surrounding words like any other text."));

        var document = new Document { Width = Length.Cells(64) };
        document.Blocks.Add(paragraph);

        var section = new Stack
        {
            Orientation = Orientation.Vertical,
            Children = { document, status }
        };

        var ctaStatus = new Text("Clicked: (never)");
        var getStartedButton = new DocumentLink("Get started free") { Emphasis = DocumentLinkEmphasis.Action };
        var pricingButton = new DocumentLink("View pricing") { Emphasis = DocumentLinkEmphasis.Action };
        getStartedButton.Clicked += (_, _) => ctaStatus.Content = "Clicked: Get started free";
        pricingButton.Clicked += (_, _) => ctaStatus.Content = "Clicked: View pricing";

        var ctaParagraph = new DocumentParagraph();
        ctaParagraph.Inlines.Add(new DocumentTextRun("Ready to try it? "));
        ctaParagraph.Inlines.Add(getStartedButton);
        ctaParagraph.Inlines.Add(new DocumentTextRun("  "));
        ctaParagraph.Inlines.Add(pricingButton);

        var ctaDocument = new Document { Width = Length.Cells(60) };
        ctaDocument.Blocks.Add(ctaParagraph);
        var ctaSection = new Stack { Children = { ctaDocument, ctaStatus } };

        return new DocSection(
            "\U0001F517",
            "Links inside the flow",
            "A <info>DocumentLink</info> is a compact semantic link node. Tab and Shift+Tab " +
            "move between links and release focus at either end exactly as a browser does; " +
            "Enter, Space, or a primary click activates the focused link. A link's " +
            "<info>Emphasis</info> chooses between an ordinary inline look and a solid " +
            "call-to-action chip, both fully themeable through <info>DocumentStyle</info>.",
            new DocExample(
                "Multiple inline links",
                "Each link wraps with the surrounding words and stays clickable on every line it " +
                "occupies, whether it was reached by keyboard or the pointer.",
                section,
                """
                var link = new DocumentLink("here", "https://example.com/docs");
                link.Clicked += (_, _) => status.Content = "Clicked";

                var paragraph = new DocumentParagraph();
                paragraph.Inlines.Add(new DocumentTextRun("Click "));
                paragraph.Inlines.Add(link);
                paragraph.Inlines.Add(new DocumentTextRun(" to continue."));
                """),
            new DocExample(
                "A link with call-to-action emphasis",
                "Setting <info>Emphasis</info> to <info>Action</info> paints a link as a solid, " +
                "high-contrast chip using <info>DocumentStyle</info>'s own <info>ActionLinkFace</info> " +
                "and <info>ActiveActionLinkFace</info> - a themeable button look built into the style " +
                "system, not a one-off color chosen by the application. The link stays a genuine, " +
                "focusable part of the flowing paragraph around it, exactly like a standard link.",
                ctaSection,
                """
                var link = new DocumentLink("Get started free")
                {
                    Emphasis = DocumentLinkEmphasis.Action
                };
                link.Clicked += (_, _) => Deploy();
                """));
    }

    private static DocSection CreateFormSection()
    {
        var updates = new CheckBox("Product updates");
        var stable = new RadioButton("Stable") { GroupName = "channel", IsChecked = true };
        var preview = new RadioButton("Preview") { GroupName = "channel" };
        var submit = new Button("Submit");
        var status = new Text("Not submitted");
        submit.Click += (_, _) => status.Content =
            $"Submitted: updates={updates.IsChecked}, channel={(stable.IsChecked ? "stable" : "preview")}";

        var document = new Document { Width = Length.Cells(60) };
        document.Blocks.Add(new DocumentHeading(2, "Release preferences"));
        document.Blocks.Add(new DocumentParagraph("Choose how this application should contact you."));
        document.Blocks.Add(new DocumentBlockControl(updates));
        document.Blocks.Add(new DocumentBlockControl(stable));
        document.Blocks.Add(new DocumentBlockControl(preview));
        document.Blocks.Add(new DocumentBlockControl(submit));

        return new DocSection(
            "☑",
            "Interactive forms",
            "Inline and block control nodes retain genuine <info>Button</info>, <info>CheckBox</info>, and <info>RadioButton</info> descendants inside readable document flow.",
            new DocExample(
                "A document-backed form",
                "Tab through the controls, change the choices, and submit. Their events, commands, grouping, and focus are ordinary SharpVision behavior.",
                new DocColumn(document, status),
                """
                var document = new Document();
                document.Blocks.Add(new DocumentParagraph("Release preferences"));
                document.Blocks.Add(new DocumentBlockControl(new CheckBox("Product updates")));
                document.Blocks.Add(new DocumentBlockControl(new RadioButton("Stable")
                {
                    GroupName = "channel"
                }));
                document.Blocks.Add(new DocumentBlockControl(new Button("Submit")));
                """));
    }

    private static DocSection CreateListSection()
    {
        var deepNested = new DocumentList(DocumentListKind.Bulleted);
        deepNested.Items.Add(new DocumentListItem("Measured, arranged, and rendered as one control"));
        deepNested.Items.Add(new DocumentListItem("Real controls mount only through explicit control nodes"));

        var nested = new DocumentList(DocumentListKind.Bulleted);
        nested.Items.Add(new DocumentListItem("Composable semantic content nodes"));
        nested.Items.Add(new DocumentListItem("Deterministic layout on every resize"));
        nested.Items.Add(new DocumentListItem("Three levels deep, and still aligned:", deepNested));

        var bulleted = new DocumentList(DocumentListKind.Bulleted);
        bulleted.Items.Add(new DocumentListItem("A familiar UI model borrowed from the web"));
        bulleted.Items.Add(new DocumentListItem("Nested content indents and rotates its glyph:", nested));
        bulleted.Items.Add(new DocumentListItem("Terminal behavior you can inspect and trust"));

        var numbered = new DocumentList(DocumentListKind.Numbered);
        for (var step = 1; step <= 11; step++)
        {
            numbered.Items.Add(new DocumentListItem($"Step {step} - still lines up at two digits"));
        }

        var document = new Document { Width = Length.Cells(64) };
        document.Blocks.Add(new DocumentHeading(2, "Bulleted, with nesting"));
        document.Blocks.Add(bulleted);
        document.Blocks.Add(new DocumentHeading(2, "Numbered past nine"));
        document.Blocks.Add(numbered);

        var looseFirst = new DocumentList { IsLoose = true };
        looseFirst.Items.Add(new DocumentListItem(
            "Loose lists separate every item with a blank line, which suits an item whose " +
            "content runs to several sentences instead of one short phrase."));
        looseFirst.Items.Add(new DocumentListItem(
            "This is CommonMark's loose-list model - the opposite of the tight nesting shown " +
            "above, where an item's own paragraph sits directly against its nested list."));
        looseFirst.Items.Add(new DocumentListItem("Set IsLoose to true to switch a list between the two."));
        var looseDocument = new Document { Width = Length.Cells(60) };
        looseDocument.Blocks.Add(looseFirst);

        return new DocSection(
            "\U0001F4CB",
            "Lists",
            "A <info>DocumentList</info> is bulleted or numbered. Nesting is a <info>DocumentListItem</info> " +
            "owning its own nested <info>DocumentList</info>, matching CommonMark's real model - the " +
            "bullet glyph rotates by depth, numbers renumber automatically when an item is " +
            "removed, and the marker gutter is measured from the widest marker so a list past " +
            "nine never collides with its own text.",
            new DocExample(
                "Bulleted, nested, and numbered past nine",
                "The nested lists under \"Nested content\" render progressively indented with a " +
                "rotated glyph per depth. The numbered list below it grows a wider gutter once " +
                "its markers reach two digits, keeping every item's text aligned.",
                document,
                """
                var list = new DocumentList(DocumentListKind.Bulleted);
                var nested = new DocumentList(DocumentListKind.Bulleted);
                nested.Items.Add(new DocumentListItem("Composable controls"));
                list.Items.Add(new DocumentListItem("Nested content:", nested));

                var numbered = new DocumentList(DocumentListKind.Numbered);
                for (var step = 1; step <= 11; step++)
                {
                    numbered.Items.Add(new DocumentListItem($"Step {step}"));
                }
                // Item 10 and beyond widen the gutter automatically.
                """),
            new DocExample(
                "A loose list for longer items",
                "IsLoose adds one blank line between items only - each item's own blocks stay tight " +
                "either way.",
                looseDocument,
                """
                var list = new DocumentList { IsLoose = true };
                list.Items.Add(new DocumentListItem("A longer first item worth its own breathing room."));
                list.Items.Add(new DocumentListItem("A second item, clearly separated from the first."));
                """));
    }

    private static DocSection CreateBlockQuoteSection()
    {
        var attribution = new DocumentParagraph();
        attribution.Inlines.Add(new DocumentTextRun("- from the "));
        attribution.Inlines.Add(new DocumentLink("project charter", "https://example.invalid/charter"));

        var innerQuote = new DocumentBlockQuote(
            "\"Correct terminal behavior, deterministic UI state, Unicode fidelity, bounded " +
            "memory use, and observable proof outrank convenience shortcuts.\"");
        innerQuote.Blocks.Add(attribution);

        var outerQuote = new DocumentBlockQuote();
        outerQuote.Blocks.Add(new DocumentParagraph(
            "A reviewer once summarized the whole project in one sentence, quoting the charter " +
            "directly:"));
        outerQuote.Blocks.Add(innerQuote);

        var document = new Document { Width = Length.Cells(60) };
        document.Blocks.Add(outerQuote);

        return new DocSection(
            "\U0001F4AC",
            "Block quotes",
            "A <info>DocumentBlockQuote</info> indents its content and marks every line it spans with a " +
            "left bar. Quotes nest freely: a quote inside a quote indents twice and draws two " +
            "bars, and a quote's own <info>Blocks</info> can hold more than one paragraph.",
            new DocExample(
                "A nested, multi-paragraph quote",
                "The bar is drawn on every line each quote spans, including wrapped continuations, " +
                "and the inner quote's own attribution paragraph sits directly beneath it.",
                document,
                """
                var quote = new DocumentBlockQuote("Correct terminal behavior outranks shortcuts.");
                var attribution = new DocumentParagraph();
                attribution.Inlines.Add(new DocumentTextRun("- from the "));
                attribution.Inlines.Add(new DocumentLink("project charter"));
                quote.Blocks.Add(attribution);
                """));
    }

    private static DocSection CreateCodeSection()
    {
        var document = new Document { Width = Length.Cells(64) };
        document.Blocks.Add(new DocumentHeading(3, "Quick start"));
        document.Blocks.Add(new DocumentCodeBlock(
            "var document = new Document();\n" +
            "document.Blocks.Add(new DocumentHeading(1, \"Hello\"));\n" +
            "document.Blocks.Add(new DocumentParagraph(\"Hi <b>world</b>.\"));\n" +
            "\n" +
            "await Application.RunAsync(document);"));
        document.Blocks.Add(new DocumentSeparator());
        document.Blocks.Add(new DocumentParagraph(
            "The rule above spans the remaining content width at its nesting level - narrower " +
            "inside a block quote, full width here at the document root."));

        return new DocSection(
            "\U0001F4BB",
            "Code blocks and thematic breaks",
            "A <info>DocumentCodeBlock</info> is literal: markup is never parsed, so source containing " +
            "angle brackets needs no escaping, line structure is preserved exactly, and lines " +
            "never wrap. A <info>DocumentSeparator</info> draws a rule across the width available " +
            "at its nesting level.",
            new DocExample(
                "Preformatted source and a rule",
                "Line breaks, blank lines, and indentation are all preserved exactly as written; " +
                "the rule spans the remaining content width.",
                document,
                """
                document.Blocks.Add(new DocumentCodeBlock(
                    "var x = a < b;\nvar y = a > b;"));
                document.Blocks.Add(new DocumentSeparator());
                """));
    }

    private static DocSection CreateFlagshipSection()
    {
        var document = new Document { Width = Length.Cells(92) };

        document.Blocks.Add(new DocumentHeading(1, "TermFlow"));

        var intro = new DocumentParagraph();
        intro.Inlines.Add(new DocumentTextRun(
            "TermFlow is a <i>lightweight</i> task tracker built entirely for the terminal - " +
            "<b>fast</b>, distraction-free, and <s>never</s> always in your workflow. It runs " +
            "over plain SSH, needs no browser, and keeps every board in a single append-only " +
            "log your whole team can audit. "));
        intro.Inlines.Add(new DocumentLink("Learn why", "https://example.invalid/termflow"));
        intro.Inlines.Add(new DocumentTextRun(" to see the full design goals."));
        document.Blocks.Add(intro);

        document.Blocks.Add(new DocumentHeading(2, "Features"));
        var searchDetails = new DocumentList(DocumentListKind.Bulleted);
        searchDetails.Items.Add(new DocumentListItem("Fuzzy matching across every board"));
        searchDetails.Items.Add(new DocumentListItem("Keyboard-only navigation, no mouse required"));
        var features = new DocumentList(DocumentListKind.Bulleted);
        features.Items.Add(new DocumentListItem("Instant search:", searchDetails));
        features.Items.Add(new DocumentListItem("Real-time collaboration over a shared session"));
        features.Items.Add(new DocumentListItem("Offline-first sync that reconciles on reconnect"));
        features.Items.Add(new DocumentListItem("Themeable, down to every glyph and face"));
        document.Blocks.Add(features);

        document.Blocks.Add(new DocumentHeading(2, "Getting started"));
        var steps = new DocumentList(DocumentListKind.Numbered);
        steps.Items.Add(new DocumentListItem("Install the CLI for your platform"));
        steps.Items.Add(new DocumentListItem("Run <b>termflow init</b> inside your project"));
        steps.Items.Add(new DocumentListItem("Open your first board and invite your team"));
        steps.Items.Add(new DocumentListItem("Connect a webhook so updates post to chat automatically"));
        document.Blocks.Add(steps);

        document.Blocks.Add(new DocumentCodeBlock(
            "$ termflow init\n" +
            "$ termflow board add \"Sprint 42\"\n" +
            "$ termflow board share --team engineering"));

        document.Blocks.Add(new DocumentBlockQuote(
            "Team and Enterprise plans include priority support, single sign-on, and an " +
            "uptime SLA backed by a real on-call rotation."));

        document.Blocks.Add(new DocumentSeparator());

        var closing = new DocumentParagraph();
        closing.Inlines.Add(new DocumentTextRun(
            "Every plan starts with a fourteen-day trial - no credit card, no sales call."));
        document.Blocks.Add(closing);

        var getStartedButton = new DocumentLink("Get started free") { Emphasis = DocumentLinkEmphasis.Action };
        var pricingButton = new DocumentLink("View pricing") { Emphasis = DocumentLinkEmphasis.Action };
        var githubButton = new DocumentLink("Star on GitHub", "https://example.invalid/termflow/repo")
        {
            Emphasis = DocumentLinkEmphasis.Action
        };

        var ctaParagraph = new DocumentParagraph();
        ctaParagraph.Inlines.Add(new DocumentTextRun("Ready?  "));
        ctaParagraph.Inlines.Add(getStartedButton);
        ctaParagraph.Inlines.Add(new DocumentTextRun("  "));
        ctaParagraph.Inlines.Add(pricingButton);
        ctaParagraph.Inlines.Add(new DocumentTextRun("  "));
        ctaParagraph.Inlines.Add(githubButton);
        document.Blocks.Add(ctaParagraph);

        return new DocSection(
            "\U0001F5C2",
            "A complete document",
            "Everything composes: headings, flowing paragraphs with activatable links, multi-level " +
            "lists mixing bulleted and numbered styles, a literal code block, a block quote, a " +
            "thematic break, and a closing call-to-action banner - a genuine document, not a toy " +
            "example, built entirely from the closed set of content nodes above with no embedded " +
            "controls anywhere in the tree. The banner links are ordinary <info>DocumentLink</info> " +
            "nodes with <info>Emphasis</info> set to <info>Action</info>, styled through " +
            "<info>DocumentStyle</info> like everything else in the tree.",
            new DocExample(
                "Project overview",
                "One control measures, paints, hit-tests, and focuses the whole tree; the nodes " +
                "themselves are pure data.",
                document,
                """
                var document = new Document();
                document.Blocks.Add(new DocumentHeading(1, "TermFlow"));

                var paragraph = new DocumentParagraph();
                paragraph.Inlines.Add(new DocumentTextRun("Built for the terminal. "));
                paragraph.Inlines.Add(new DocumentLink("Learn why"));
                document.Blocks.Add(paragraph);

                var features = new DocumentList(DocumentListKind.Bulleted);
                var searchDetails = new DocumentList(DocumentListKind.Bulleted);
                searchDetails.Items.Add(new DocumentListItem("Fuzzy matching"));
                features.Items.Add(new DocumentListItem("Instant search:", searchDetails));
                document.Blocks.Add(features);

                document.Blocks.Add(new DocumentCodeBlock("$ termflow init"));
                document.Blocks.Add(new DocumentBlockQuote("Priority support included."));
                document.Blocks.Add(new DocumentSeparator());

                var cta = new DocumentParagraph();
                cta.Inlines.Add(new DocumentLink("Get started free")
                {
                    Emphasis = DocumentLinkEmphasis.Action
                });
                document.Blocks.Add(cta);
                """));
    }
}
