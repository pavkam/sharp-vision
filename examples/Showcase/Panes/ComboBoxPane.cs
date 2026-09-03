// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the ComboBox control with live popup-selection specimens.</summary>
internal sealed class ComboBoxPane: CompositeControlBase
{
    internal ComboBoxPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ComboBox";

    private static readonly Face _captionFace = new(
        SemanticColor.ControlText,
        Color.Transparent,
        TerminalAttributes.Bold,
        Underline.None,
        Color.Default);

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Default bordered field inside a stage that allows the popup to extend outside the box.
        var density = new Text("Selected: Comfortable.");
        var comboBox = new ComboBox
        {
            Width = Length.Cells(28),
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 1,
            DropDownHeight = Length.Cells(4)
        };
        ShowcasePaneHelpers.WireComboSelectionStatus(comboBox, density, "Selected");
        var stage = ShowcasePaneHelpers.ComboStage(30, 6, comboBox);

        // PopupChrome overrides the owned drop-down's border and shadow independently of the
        // closed field's own chrome, which ComboBox always owns and never exposes for override.
        var customChrome = new ComboBox
        {
            Width = Length.Cells(28),
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 0,
            PopupChrome = new PopupChrome
            {
                Border = new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Heavy,
                    Color.Rgb(0x77, 0xaa, 0xff),
                    Color.Transparent,
                    TerminalAttributes.None)
            }
        };

        // Enter commits the highlighted row; Escape dismisses without changing the value.
        var commitStatus = new Text("Committed: Comfortable");
        var commitCombo = new ComboBox
        {
            Width = Length.Cells(28),
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 1
        };
        ShowcasePaneHelpers.WireComboSelectionStatus(commitCombo, commitStatus, "Committed", trailingPeriod: false);

        // SelectedIndex -1 is a valid explicit empty state; Placeholder supplies its face.
        var emptyStatus = new Text("Selected: Clearable one.");
        var emptyCombo = new ComboBox
        {
            Width = Length.Cells(28),
            Items = ["Clearable one", "Clearable two"],
            SelectedIndex = 0,
            Placeholder = "Choose an item…"
        };
        ShowcasePaneHelpers.WireComboSelectionStatus(emptyCombo, emptyStatus, "Selected");
        var clearSelection = new Button { Text = "&Clear selection" };
        clearSelection.Click += (_, _) => emptyCombo.SelectedIndex = -1;

        // A relative DropDownHeight follows the usable placement-side extent while the list scrolls inside it.
        var manyItems = new object?[12];
        for (var index = 0; index < manyItems.Length; index++)
        {
            manyItems[index] = $"Item {index + 1}";
        }

        var tallStatus = new Text("Selected: Item 1.");
        var tall = new ComboBox
        {
            Width = Length.Cells(24),
            Items = manyItems,
            SelectedIndex = 0,
            DropDownHeight = Length.Percent(50),
            RowHeight = Length.Percent(25),
            ItemTemplate = item => new Text((string) item!) { Height = Length.Star(1) }
        };
        ShowcasePaneHelpers.WireComboSelectionStatus(tall, tallStatus, "Selected");
        var tallStage = ShowcasePaneHelpers.ComboStage(26, 8, tall);

        var disabled = new ComboBox
        {
            Width = Length.Cells(24),
            Items = ["Locked choice"],
            SelectedIndex = 0,
            IsEnabled = false
        };

        // Lower-edge placement prefers below, flips above when needed, then clamps to the host.
        var edgeStatus = new Text("Open near the lower edge to observe fallback placement.");
        var edgeChoice = new ComboBox
        {
            Width = Length.Cells(24),
            Items = ["Above when needed", "Clamped to host"],
            SelectedIndex = 0,
            DropDownHeight = Length.Cells(4)
        };
        ShowcasePaneHelpers.WireComboSelectionStatus(edgeChoice, edgeStatus, "Committed", trailingPeriod: false);
        var edgeStage = ShowcasePaneHelpers.OverlayStage(30, 3, clipToBounds: false);
        Overlay.SetTop(edgeChoice, Length.Cells(1));
        edgeStage.Children.Add(edgeChoice);

        // StartAffix reserves a fixed cell beside the field text, alongside the closed field's
        // own drop-down indicator glyph.
        var taggedStatus = new Text("Selected: Comfortable.");
        var tagged = new ComboBox
        {
            Width = Length.Cells(28),
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 1,
            StartAffix = new Affix("🏷")
        };
        ShowcasePaneHelpers.WireComboSelectionStatus(tagged, taggedStatus, "Selected");

        return new DocPage(
            Title,
            "<info>ComboBox</info> displays one selected value and opens an owned popup-style <info>ListView</info> for keyboard or pointer choice.",
            new DocSection(
                "🔽",
                "Start here",
                "Choose one compact value while keeping the full choice list available on demand.",
                new DocExample(
                    "Default field and custom popup chrome",
                    "ComboBox always owns the closed field's border; applications cannot remove or replace it. <info>PopupChrome</info> instead overrides the owned drop-down popup's border and shadow independently, without touching the field. Both keep an opaque <info>Surface</info>. The open list joins the field directly and uses the field's lower edge instead of drawing a second line between them. Click, <reverse>Enter</reverse>, or <reverse>Space</reverse> opens the list; <reverse>Enter</reverse> <success>commits</success>, while <reverse>Escape</reverse> or outside input <warning>dismisses</warning> without changing the value. With the field closed and focused, arrows, <reverse>Home</reverse>/<reverse>End</reverse>, and <reverse>Page Up</reverse>/<reverse>Page Down</reverse> also change the selection immediately, without opening the list.",
                    new DocColumn(
                        new Text("Default field chrome") { Face = _captionFace },
                        stage,
                        density,
                        new Text("Custom popup chrome") { Face = _captionFace },
                        customChrome),
                    "combo.PopupChrome = new PopupChrome\n{\n    Border = new Border(BorderSide.All, BorderGlyphStyle.Heavy, accentColor, Color.Transparent, TerminalAttributes.None),\n};")),
            new DocSection(
                "🔽",
                "Commit versus dismiss",
                "The popup keeps hover and keyboard highlight separate from the committed selection, and outside input closes it cleanly.",
                new DocExample(
                    "Enter commits; Escape dismisses",
                    "Open the first field, move with arrows, then compare <reverse>Enter</reverse> with <reverse>Escape</reverse>. <reverse>Escape</reverse> closes without replacing the previous value.",
                    new DocColumn(commitCombo, commitStatus))),
            new DocSection(
                "🔽",
                "Long choices",
                "Cap popup height relative to its available placement side and let the owned ListView scroll the remaining choices.",
                new DocExample(
                    "Capped drop-down",
                    "The list uses half of the usable placement-side height and responds when the terminal resizes. Arrow, wheel, and paging input scroll inside the popup before the page.",
                    new DocColumn(tallStage, tallStatus),
                    "combo.DropDownHeight = Length.Percent(50);\ncombo.RowHeight = Length.Percent(25);\ncombo.ShowScrollBars = ShowScrollBars.WhenNeeded;")),
            new DocSection(
                "🔽",
                "No selection",
                "<info>SelectedIndex</info> <info>-1</info> is a valid explicit empty state.",
                new DocExample(
                    "Clear the committed value",
                    "Activate Clear selection. The field and status update through the same SelectionChanged contract.",
                    new DocColumn(clearSelection, emptyCombo, emptyStatus))),
            new DocSection(
                "🔽",
                "Constrained placement",
                "The owned Popup prefers below, flips when the lower edge cannot fit, and clamps to its host.",
                new DocExample(
                    "Lower-edge field",
                    "Open this field near the stage bottom and resize the terminal to watch placement recompute.",
                    new DocColumn(edgeStage, edgeStatus))),
            new DocSection(
                "🔽",
                "Unavailable state",
                "Keep a locked choice visible without allowing focus or popup activation.",
                new DocExample(
                    "Disabled field",
                    "The committed value remains readable while keyboard and pointer changes are ignored.",
                    disabled)),
            new DocSection(
                "🏷️",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell for application-owned, data-driven decoration that coexists with the closed field's own drop-down indicator.",
                new DocExample(
                    "Tag glyph beside the field",
                    "The tag glyph occupies its own reserved cell before the field text; the theme-owned drop-down indicator still renders at the field's edge.",
                    new DocColumn(tagged, taggedStatus),
                    "combo.StartAffix = new Affix(\"🏷\");")));
    }
}
