// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the CheckBox control with live, themed toggle specimens.</summary>
internal sealed class CheckBoxPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "CheckBox";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var status = new Text("State log: waiting");
        var live = new CheckBox() { Content = new Text("Toggle with Space, Enter, or a pointer click") };
        live.StateChanged += (_, eventArgs) =>
            status.Content = $"State log: {eventArgs.Current?.ToString() ?? "indeterminate"} ({eventArgs.Cause})";

        var square = new CheckBox() { Content = new Text("Square marks"), MarkStyle = CheckBoxMarks.Square, IsChecked = true };
        var brackets = new CheckBox() { Content = new Text("Bracket marks"), MarkStyle = CheckBoxMarks.Brackets, IsChecked = true };
        var tick = new CheckBox() { Content = new Text("Tick marks"), MarkStyle = CheckBoxMarks.Tick, IsChecked = true };
        var custom = new CheckBox
        {
            Content = new Text("Custom marks"),
            Marks = new Marks(new Rune('·'), new Rune('✓'), new Rune('~')),
            IsChecked = true,
        };

        var unchecked_ = new CheckBox() { Content = new Text("Unchecked"), MarkStyle = CheckBoxMarks.Brackets };
        var checked_ = new CheckBox() { Content = new Text("Checked"), MarkStyle = CheckBoxMarks.Brackets, IsChecked = true };
        var indeterminate = new CheckBox()
        {
            Content = new Text("Indeterminate"),
            MarkStyle = CheckBoxMarks.Brackets,
            IsThreeState = true,
            IsChecked = null,
        };

        var disabled = new CheckBox()
        {
            Content = new Text("Disabled, checked"),
            MarkStyle = CheckBoxMarks.Brackets,
            IsChecked = true,
            IsEnabled = false,
        };

        var policyStatus = new Text("Policy: indeterminate");
        var policy = new CheckBox
        {
            Content = new Text("Optional inherited value"),
            IsThreeState = true,
            IsChecked = null,
        };
        var twoState = new Button() { Content = new Text("Return to two states") };
        twoState.Click += (_, _) =>
        {
            policy.IsThreeState = false;
            policyStatus.Content = $"Policy: {policy.IsChecked}";
        };

        var eventStatus = new Text("Events: waiting");
        var eventProbe = new CheckBox() { Content = new Text("Observe event order") };
        eventProbe.Checked += (_, _) => eventStatus.Content = "Events: Checked";
        eventProbe.Unchecked += (_, _) => eventStatus.Content = "Events: Unchecked";
        eventProbe.Indeterminate += (_, _) => eventStatus.Content = "Events: Indeterminate";
        eventProbe.StateChanged += (_, _) => eventStatus.Content += " → StateChanged";

        var settings = Doc.Card(Doc.Column(
            new Text("Export options") { Attributes = TerminalAttributes.Bold },
            new CheckBox { Content = new Text("Include metadata"), IsChecked = true },
            new CheckBox { Content = new Text("Compress output") },
            disabled));

        return Doc.Page(
            Title,
            "Toggles an optional label through two-state or three-state selection with explicit events.",
            Doc.Section(
                "Two-state choice",
                "Use the default false/true cycle for independent Boolean options.",
                Doc.Example(
                    "Live toggle",
                    "Use Space, Enter, or a primary click. The status reports both the committed value and activation cause.",
                    Doc.Column(live, status),
                    "var option = new CheckBox { Content = new Text(\"Include metadata\") };")),
            Doc.Section(
                "Three-state policy",
                "Use null only when the option genuinely represents an inherited or mixed value.",
                Doc.Example(
                    "Cycle and normalize",
                    "The three examples show every state. Return the live policy to two states and its indeterminate value normalizes to false before notifications finish.",
                    Doc.Column(unchecked_, checked_, indeterminate, policy, twoState, policyStatus),
                    "option.IsThreeState = true;\noption.IsChecked = null;")),
            Doc.Section(
                "Marks",
                "Built-in families and validated caller glyphs retain stable label placement.",
                Doc.Example(
                    "Built-in and custom glyphs",
                    "Square, bracket, tick, and custom marks all use printable one-cell state glyphs.",
                    Doc.Column(square, brackets, tick, custom))),
            Doc.Section(
                "Events",
                "State-specific notifications precede the general StateChanged notification.",
                Doc.Example(
                    "Committed event order",
                    "Toggle the probe and read the exact state-specific → StateChanged sequence.",
                    Doc.Column(eventProbe, eventStatus))),
            Doc.Section(
                "Form recipe",
                "Group related options and keep unavailable retained values visible as context.",
                Doc.Example(
                    "Export settings",
                    "The disabled checked option remains readable but refuses focus and activation.",
                    settings)));
    }
}
