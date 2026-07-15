// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the RadioButton control with grouped, mutually exclusive selection specimens.</summary>
internal sealed class RadioButtonPane: CompositeControl
{

    internal RadioButtonPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "RadioButton";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        var fast = new RadioButton() { Content = new Text("Fast"), GroupName = "quality", IsChecked = true };
        var balanced = new RadioButton() { Content = new Text("Balanced"), GroupName = "quality" };
        var unavailable = new RadioButton()
        {
            Content = new Text("Unavailable"),
            GroupName = "quality",
            IsEnabled = false,
        };
        var group = new Dock()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Padding = new Thickness(1, 0),
            Children = { Doc.Column(new Text("Primary card") { Attributes = TerminalAttributes.Bold }, fast, unavailable) },
        };

        var separate = new Dock()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Light,
            Padding = new Thickness(1, 0),
            Children = { Doc.Column(new Text("Secondary card") { Attributes = TerminalAttributes.Bold }, balanced) },
        };
        var qualityStatus = new Text("Selected quality: Fast");
        fast.Checked += (_, _) => qualityStatus.Content = "Selected quality: Fast";
        balanced.Checked += (_, _) => qualityStatus.Content = "Selected quality: Balanced";

        var traversalStatus = new Text("Traversal: one");
        var traversalOne = new RadioButton
        {
            Content = new Text("Traversal one"),
            GroupName = "traversal",
            IsChecked = true,
        };
        var traversalTwo = new RadioButton() { Content = new Text("Traversal two"), GroupName = "traversal" };
        var traversalUnavailable = new RadioButton
        {
            Content = new Text("Traversal unavailable"),
            GroupName = "traversal",
            IsEnabled = false,
        };
        traversalOne.Checked += (_, _) => traversalStatus.Content = "Traversal: one";
        traversalTwo.Checked += (_, _) => traversalStatus.Content = "Traversal: two";

        var regroupStatus = new Text("Regrouped: waiting");
        var movable = new RadioButton
        {
            Content = new Text("Movable option"),
            GroupName = "left",
            IsChecked = true,
        };
        var leftPeer = new RadioButton() { Content = new Text("Left peer"), GroupName = "left" };
        var rightPeer = new RadioButton
        {
            Content = new Text("Right peer"),
            GroupName = "right",
            IsChecked = true,
        };
        var regroup = new Button() { Content = new Text("Move selected option to right group") };
        regroup.Click += (_, _) =>
        {
            movable.GroupName = "right";
            regroupStatus.Content = "Regrouped: Movable option → right";
        };

        var localA = new RadioButton() { Content = new Text("Local A"), IsChecked = true };
        var localB = new RadioButton() { Content = new Text("Local B") };
        var otherA = new RadioButton() { Content = new Text("Other A"), IsChecked = true };
        var otherB = new RadioButton() { Content = new Text("Other B") };

        var emptyStatus = new Text("Empty group: none");
        var emptyFirst = new RadioButton() { Content = new Text("First"), GroupName = "empty" };
        var emptySecond = new RadioButton() { Content = new Text("Second"), GroupName = "empty" };
        var selectFirst = new Button() { Content = new Text("Select first programmatically") };
        selectFirst.Click += (_, _) =>
        {
            emptyFirst.IsChecked = true;
            emptyStatus.Content = "Empty group: First";
        };

        var eventStatus = new Text("Events: waiting");
        var eventFirst = new RadioButton
        {
            Content = new Text("Event first"),
            GroupName = "events",
            IsChecked = true,
        };
        var eventSecond = new RadioButton() { Content = new Text("Event second"), GroupName = "events" };
        eventFirst.Unchecked += (_, _) => eventStatus.Content = "Events: Unchecked";
        eventSecond.Checked += (_, _) => eventStatus.Content += " → Checked";
        eventSecond.SelectionChanged += (_, _) => eventStatus.Content += " → SelectionChanged";

        return Doc.Page(
            Title,
            "Selects one option from an ordinally named group scoped to the attached control root.",
            Doc.Section(
                "📻",
                "Named group",
                "Use one ordinal GroupName when choices may live in different layout containers but still select exclusively.",
                Doc.Example(
                    "Quality choice",
                    "Fast and Balanced live in separate cards but share the exact quality group. Selecting either clears the other; the disabled member remains visible and traversal skips it.",
                    Doc.Column(Doc.Row(group, separate), qualityStatus),
                    "var fast = new RadioButton { GroupName = \"quality\", IsChecked = true };")),
            Doc.Section(
                "📻",
                "Arrow traversal",
                "Arrow keys move focus and selection through eligible members in stable tree order with wrapping.",
                Doc.Example(
                    "Visible traversal order",
                    "Use Up/Left and Down/Right. The disabled member is skipped, wrapping stays stable, and the readout follows the committed member.",
                    Doc.Column(traversalOne, traversalTwo, traversalUnavailable, traversalStatus))),
            Doc.Section(
                "📻",
                "Programmatic regrouping",
                "Changing GroupName on a selected member immediately reconciles exclusivity in its new ordinal group.",
                Doc.Example(
                    "Move a selected member",
                    "Move the selected option from left to right. The existing right selection yields to the moved member as one committed transaction.",
                    Doc.Column(Doc.Row(movable, leftPeer), rightPeer, regroup, regroupStatus),
                    "selected.GroupName = \"right\";")),
            Doc.Section(
                "📻",
                "Unnamed scope",
                "A null GroupName groups only siblings under their nearest parent, which is useful for self-contained option cards.",
                Doc.Example(
                    "Two independent local groups",
                    "Changing Local A/B never disturbs Other A/B because each pair belongs to a different parent container.",
                    Doc.Row(
                        Doc.Card(Doc.Column(localA, localB)),
                        Doc.Card(Doc.Column(otherA, otherB))))),
            Doc.Section(
                "📻",
                "No initial selection",
                "A group may begin empty and receive its first choice from user or programmatic input.",
                Doc.Example(
                    "Empty until chosen",
                    "Neither member starts checked. Use the button to select First through ordinary property mutation.",
                    Doc.Column(emptyFirst, emptySecond, selectFirst, emptyStatus))),
            Doc.Section(
                "📻",
                "Events",
                "Selection commits both members before notifications expose the completed transaction.",
                Doc.Example(
                    "Committed notification order",
                    "Choose Event second and observe Unchecked → Checked → SelectionChanged.",
                    Doc.Column(eventFirst, eventSecond, eventStatus))));
    }
}
