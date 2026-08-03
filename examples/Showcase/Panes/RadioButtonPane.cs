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
        var group = new Dock
        {
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children =
            {
                new DocColumn(new Text("Primary card") { Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default) }, fast, unavailable)
            }
        };

        var separate = new Dock
        {
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { new DocColumn(new Text("Secondary card") { Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default) }, balanced) }
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
        var rightPeer = new RadioButton { Text = "&Right peer", GroupName = "right", IsChecked = true };
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
                    new DocColumn(new DocRow(group, separate), qualityStatus),
                    "var fast = new RadioButton { GroupName = \"quality\", IsChecked = true };")),
            new DocSection(
                "🔘",
                "Arrow traversal",
                "Arrow keys move focus and selection through eligible members in stable tree order with wrapping.",
                new DocExample(
                    "Visible traversal order",
                    "Use Up/Left and Down/Right. The disabled member is skipped, wrapping stays stable, and the readout follows the committed member.",
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
                "Choose compact circles or fixed-width parentheses, then customize circle glyphs only when the visual language needs it.",
                new DocExample(
                    "Parenthesized marks",
                    "The parenthesized style renders ( ) and (●), aligned like bracket-style checkboxes.",
                    new DocColumn(parenthesizedUnchecked, parenthesizedChecked),
                    "radio.Style = RadioButtonStyle.Parentheses;"),
                new DocExample(
                    "Dash and star marks",
                    "A complete style replaces both compact circle marks as one presentation.",
                    new DocColumn(
                        new RadioButton
                        {
                            Text = "Picked option",
                            GroupName = "custom-glyph",
                            UseMnemonic = false,
                            Style = new RadioButtonStyle(
                                RadioButtonMarkStyle.Circle,
                                new RadioButtonGlyphs(new Rune('-'), new Rune('*')),
                                RadioButtonStyle.Default.Appearance),
                            IsChecked = true
                        },
                        new RadioButton
                        {
                            Text = "Rival option",
                            GroupName = "custom-glyph",
                            UseMnemonic = false,
                            Style = new RadioButtonStyle(
                                RadioButtonMarkStyle.Circle,
                                new RadioButtonGlyphs(new Rune('-'), new Rune('*')),
                                RadioButtonStyle.Default.Appearance)
                        }),
                    "radio.Style = new RadioButtonStyle(RadioButtonMarkStyle.Circle, glyphs, appearance);")),
            new DocSection(
                "⚡",
                "Events",
                "Selection commits both members before notifications expose the completed transaction.",
                new DocExample(
                    "Committed notification order",
                    "Choose Event second and observe Unchecked → Checked → SelectionChanged.",
                    new DocColumn(eventFirst, eventSecond, eventStatus))));
    }
}
