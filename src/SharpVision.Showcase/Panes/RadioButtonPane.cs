// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the RadioButton control with grouped, mutually exclusive selection specimens.</summary>
internal sealed class RadioButtonPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "RadioButton";

    /// <inheritdoc/>
    protected override Control Build()
    {
        RadioButton fast = new() { Content = new Text("Fast"), GroupName = "quality", IsChecked = true };
        RadioButton balanced = new() { Content = new Text("Balanced"), GroupName = "quality" };
        RadioButton unavailable = new()
        {
            Content = new Text("Unavailable"),
            GroupName = "quality",
            IsEnabled = false,
        };
        Dock group = new()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Padding = new Thickness(1, 0),
            Children = { Doc.Column(fast, balanced, unavailable) },
        };

        RadioButton independent = new()
        {
            Content = new Text("Independent selection group"),
            GroupName = "delivery",
            IsChecked = true,
        };
        Dock separate = new()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Light,
            Padding = new Thickness(1, 0),
            Children = { independent },
        };

        var traversalStatus = new Text("Traversal: Fast");
        fast.Checked += (_, _) => traversalStatus.Content = "Traversal: Fast";
        balanced.Checked += (_, _) => traversalStatus.Content = "Traversal: Balanced";

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
                "Named group",
                "Use one ordinal GroupName when choices may live in different layout containers but still select exclusively.",
                Doc.Example(
                    "Quality choice",
                    "Choose Fast or Balanced. The disabled member remains part of the group but keyboard traversal skips it.",
                    Doc.Column(group, separate),
                    "var fast = new RadioButton { GroupName = \"quality\", IsChecked = true };")),
            Doc.Section(
                "Arrow traversal",
                "Arrow keys move focus and selection through eligible members in stable tree order with wrapping.",
                Doc.Example(
                    "Visible traversal order",
                    "Use Up/Left and Down/Right on the quality group; the readout follows the committed member.",
                    traversalStatus)),
            Doc.Section(
                "Unnamed scope",
                "A null GroupName groups only siblings under their nearest parent, which is useful for self-contained option cards.",
                Doc.Example(
                    "Two independent local groups",
                    "Changing Local A/B never disturbs Other A/B because each pair belongs to a different parent container.",
                    Doc.Row(
                        Doc.Card(Doc.Column(localA, localB)),
                        Doc.Card(Doc.Column(otherA, otherB))))),
            Doc.Section(
                "No initial selection",
                "A group may begin empty and receive its first choice from user or programmatic input.",
                Doc.Example(
                    "Empty until chosen",
                    "Neither member starts checked. Use the button to select First through ordinary property mutation.",
                    Doc.Column(emptyFirst, emptySecond, selectFirst, emptyStatus))),
            Doc.Section(
                "Events",
                "Selection commits both members before notifications expose the completed transaction.",
                Doc.Example(
                    "Committed notification order",
                    "Choose Event second and observe Unchecked → Checked → SelectionChanged.",
                    Doc.Column(eventFirst, eventSecond, eventStatus))));
    }
}
