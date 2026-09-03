// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the RadioButton control with grouped, mutually exclusive selection specimens.</summary>
internal sealed class RadioButtonPane: CompositeControlBase
{
    internal RadioButtonPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "RadioButton";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var fast = new RadioButton { Text = "&Fast", GroupName = "quality", IsChecked = true };
        var balanced = new RadioButton { Text = "&Balanced", GroupName = "quality" };
        var unavailable = new RadioButton
        {
            Text = "&Unavailable",
            GroupName = "quality",
            IsEnabled = false
        };
        var group = new DocCard(
            new DocColumn(new Text("Primary card")
            {
                Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default)
            }, fast, unavailable));
        var separate = new DocCard(
            new DocColumn(new Text("Secondary card")
            {
                Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default)
            }, balanced));
        var qualityCards = new Wrap
        {
            Width = Length.Percent(100),
            Spacing = 2,
            LineSpacing = 1,
            Children = { group, separate }
        };
        var qualityStatus = new Text("Selected quality: Fast");
        fast.Checked += (_, _) => qualityStatus.Content = "Selected quality: Fast";
        balanced.Checked += (_, _) => qualityStatus.Content = "Selected quality: Balanced";

        var traversalStatus = new Text("Traversal: one");
        var traversalOne = new RadioButton
        {
            Text = "&Traversal one",
            GroupName = "traversal",
            IsChecked = true
        };
        var traversalTwo = new RadioButton { Text = "Traversal t&wo", GroupName = "traversal" };
        var traversalUnavailable = new RadioButton
        {
            Text = "Traversal &unavailable",
            GroupName = "traversal",
            IsEnabled = false
        };
        traversalOne.Checked += (_, _) => traversalStatus.Content = "Traversal: one";
        traversalTwo.Checked += (_, _) => traversalStatus.Content = "Traversal: two";

        var regroupStatus = new Text("Regrouped: waiting");
        var movable = new RadioButton { Text = "&Movable option", GroupName = "left", IsChecked = true };
        var leftPeer = new RadioButton { Text = "&Left peer", GroupName = "left" };
        var rightPeer = new RadioButton { Text = "Ri&ght peer", GroupName = "right", IsChecked = true };
        var regroup = new Button { Text = "Mo&ve selected option to right group" };
        regroup.Click += (_, _) =>
        {
            movable.GroupName = "right";
            regroupStatus.Content = "Regrouped: Movable option → right";
        };

        var localA = new RadioButton { Text = "Loc&al A", IsChecked = true };
        var localB = new RadioButton { Text = "Lo&cal B" };
        var otherA = new RadioButton { Text = "&Other A", IsChecked = true };
        var otherB = new RadioButton { Text = "Ot&her B" };

        var emptyStatus = new Text("Empty group: none");
        var emptyFirst = new RadioButton { Text = "F&irst", GroupName = "empty" };
        var emptySecond = new RadioButton { Text = "&Second", GroupName = "empty" };
        var selectFirst = new Button { Text = "Select first &programmatically" };
        selectFirst.Click += (_, _) =>
        {
            emptyFirst.IsChecked = true;
            emptyStatus.Content = "Empty group: First";
        };

        var eventStatus = new Text("Events: waiting");
        var eventFirst = new RadioButton { Text = "&Event first", GroupName = "events", IsChecked = true };
        var eventSecond = new RadioButton { Text = "Eve&nt second", GroupName = "events" };
        eventFirst.Unchecked += (_, _) => eventStatus.Content = "Events: Unchecked";
        eventSecond.Checked += (_, _) => eventStatus.Content += " → Checked";
        eventSecond.SelectionChanged += (_, _) => eventStatus.Content += " → SelectionChanged";

        var parenthesizedUnchecked = new RadioButton
        {
            Text = "Unselected option",
            GroupName = "parenthesized",
            Style = RadioButtonStyle.Parentheses
        };
        var parenthesizedChecked = new RadioButton
        {
            Text = "Selected option",
            GroupName = "parenthesized",
            Style = RadioButtonStyle.Parentheses,
            IsChecked = true
        };
        var compactUnchecked = new RadioButton
        {
            Text = "Compact unselected",
            GroupName = "compact",
            UseMnemonic = false,
            Style = RadioButtonStyle.Glyph with { MarkGap = 3 }
        };
        var compactChecked = new RadioButton
        {
            Text = "Compact selected",
            GroupName = "compact",
            UseMnemonic = false,
            Style = RadioButtonStyle.Glyph with { MarkGap = 3 },
            IsChecked = true
        };

        var activationCount = 0;
        var activationStatus = new Text("Programmatic activations: 0");
        var activationTarget = new RadioButton
        {
            Text = "Programmatic target &X",
            GroupName = "programmatic",
            Command = new ShowcaseCommand(
                _ => activationStatus.Content = $"Programmatic activations: {++activationCount}",
                _ => true)
        };
        var activationTrigger = new Button { Text = "Invoke programmaticall&y" };
        activationTrigger.Click += (_, _) => activationTarget.PerformClick();

        // EndAffix reserves a fixed cell for an application-owned recommended-choice badge.
        var recommendedFast = new RadioButton
        {
            Text = "&Recommended: Fast",
            GroupName = "recommendation",
            IsChecked = true,
            EndAffix = new Affix("★", "*", SemanticColor.Warning)
        };
        var recommendedBalanced = new RadioButton { Text = "Recommended: Balance&d", GroupName = "recommendation" };

        return new DocPage(
            Title,
            "<info>RadioButton</info> selects one option from a <info>GroupName</info> group scoped to the attached control root.",
            new DocSection(
                "📻",
                "Named group",
                "Use a shared <info>GroupName</info> string when choices may live in different layout containers but still select exclusively.",
                new DocExample(
                    "Quality choice",
                    "Fast and Balanced live in separate cards but share the exact quality group. Selecting either clears the other; the disabled member remains visible and traversal skips it.",
                    new DocColumn(qualityCards, qualityStatus),
                    "var fast = new RadioButton { GroupName = \"quality\", IsChecked = true };")),
            new DocSection(
                "🔘",
                "Arrow traversal",
                "Arrow keys move through eligible members with wrapping; Home and End jump to the eligible group boundaries.",
                new DocExample(
                    "Visible traversal order",
                    "Use Up/Left and Down/Right to walk, then Home or End to jump. The disabled member is skipped, wrapping stays stable, and the readout follows the committed member.",
                    new DocColumn(traversalOne, traversalTwo, traversalUnavailable, traversalStatus))),
            new DocSection(
                "🔀",
                "Programmatic regrouping",
                "Changing <info>GroupName</info> on a selected member immediately reconciles exclusivity in its new group.",
                new DocExample(
                    "Move a selected member",
                    "Move the selected option from left to right. The existing right selection yields to the moved member as one committed transaction.",
                    new DocColumn(new DocRow(movable, leftPeer), rightPeer, regroup, regroupStatus),
                    "selected.GroupName = \"right\";")),
            new DocSection(
                "🏷️",
                "Unnamed scope",
                "A <info>null GroupName</info> groups only siblings under their nearest parent, which is useful for self-contained option cards.",
                new DocExample(
                    "Two independent local groups",
                    "Changing Local A/B never disturbs Other A/B because each pair belongs to a different parent container.",
                    new DocRow(
                        new DocCard(new DocColumn(localA, localB)),
                        new DocCard(new DocColumn(otherA, otherB))))),
            new DocSection(
                "📭",
                "No initial selection",
                "A group may begin empty and receive its first choice from user or programmatic input.",
                new DocExample(
                    "Empty until chosen",
                    "Neither member starts checked. Use the button to select First through ordinary property mutation.",
                    new DocColumn(emptyFirst, emptySecond, selectFirst, emptyStatus))),
            new DocSection(
                "🎨",
                "Mark styles",
                "Choose compact circles or fixed-width parentheses, align their captions with MarkGap, and move custom marks to either edge.",
                new DocExample(
                    "Built-in mark families",
                    "Parentheses render ( ) and (●). Compact circles render ○ and ◉, with a larger MarkGap here to align both families' captions.",
                    new DocColumn(parenthesizedUnchecked, parenthesizedChecked, compactUnchecked, compactChecked),
                    "radio.Style = RadioButtonStyle.Glyph with { MarkGap = 3 };"),
                new DocExample(
                    "Trailing dash and star marks",
                    "A complete style replaces both compact marks and places them two cells after the caption.",
                    new DocColumn(
                        new RadioButton
                        {
                            Text = "Picked option",
                            GroupName = "custom-glyph",
                            UseMnemonic = false,
                            Style = new RadioButtonStyle(
                                RadioButtonStyle.Default.Face,
                                RadioButtonStyle.Default.Border,
                                RadioButtonStyle.Default.Shadow,
                                RadioButtonMarkStyle.Circle,
                                new RadioButtonGlyphs(new Rune('-'), new Rune('*'))) with
                            {
                                MarkGap = 2,
                                MarkPlacement = SelectionMarkPlacement.Trailing
                            },
                            IsChecked = true
                        },
                        new RadioButton
                        {
                            Text = "Rival option",
                            GroupName = "custom-glyph",
                            UseMnemonic = false,
                            Style = new RadioButtonStyle(
                                RadioButtonStyle.Default.Face,
                                RadioButtonStyle.Default.Border,
                                RadioButtonStyle.Default.Shadow,
                                RadioButtonMarkStyle.Circle,
                                new RadioButtonGlyphs(new Rune('-'), new Rune('*'))) with
                            {
                                MarkGap = 2,
                                MarkPlacement = SelectionMarkPlacement.Trailing
                            }
                        }),
                    "radio.Style = new RadioButtonStyle(face, border, shadow, RadioButtonMarkStyle.Circle, glyphs)\n{\n    MarkGap = 2,\n    MarkPlacement = SelectionMarkPlacement.Trailing\n};")),
            new DocSection(
                "⚡",
                "Events",
                "Selection commits both members before notifications expose the completed transaction.",
                new DocExample(
                    "Committed notification order",
                    "Choose Event second and observe Unchecked → Checked → SelectionChanged.",
                    new DocColumn(eventFirst, eventSecond, eventStatus)),
                new DocExample(
                    "Programmatic activation",
                    "Invoke PerformClick repeatedly. The first activation selects the target; every activation then executes its bound Command.",
                    new DocColumn(activationTrigger, activationTarget, activationStatus),
                    "target.PerformClick();")),
            new DocSection(
                "📌",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve the leading and trailing edges outside the combined mark-caption box for application-owned, data-driven decoration that a theme never authors.",
                new DocExample(
                    "Recommended-choice badge after the caption",
                    "The star marks the recommended option; it sits in its own reserved cell after the caption.",
                    new DocColumn(recommendedFast, recommendedBalanced),
                    "var radio = new RadioButton\n{\n    Text = \"Recommended: Fast\",\n    EndAffix = new Affix(\"★\", \"*\", SemanticColor.Warning)\n};")));
    }
}
