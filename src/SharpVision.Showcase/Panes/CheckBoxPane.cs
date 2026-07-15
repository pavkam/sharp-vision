// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the CheckBox control with live, themed toggle specimens.</summary>
internal sealed class CheckBoxPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "CheckBox";

    /// <summary>Initializes the retained CheckBox documentation page.</summary>
    internal CheckBoxPane() => InitializeContent(CreateContent());

    private static Stack CreateContent()
    {
        var status = new Text("State log: waiting");
        var live = new CheckBox() { Content = new Text("Toggle with Space, Enter, or a pointer click") };
        live.StateChanged += (_, eventArgs) =>
            status.Content = $"State log: {eventArgs.Current?.ToString() ?? "indeterminate"} ({eventArgs.Cause})";

        var square = new CheckBox() { Content = new Text("Square marks"), MarkStyle = CheckBoxMarks.Square, IsChecked = true };
        var brackets = new CheckBox() { Content = new Text("Bracket marks"), MarkStyle = CheckBoxMarks.Brackets, IsChecked = true };
        var tick = new CheckBox() { Content = new Text("Tick marks"), MarkStyle = CheckBoxMarks.Tick, IsChecked = true };

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

        return Doc.Page(
            Title,
            "Toggles an optional label through two-state or three-state selection with explicit events.",
            Doc.Example(
                "Live toggle",
                "Space, Enter, or a primary pointer click commits a state transition. The most recent state and its activation cause are reported below.",
                Doc.Column(live, status)),
            Doc.Example(
                "Mark families",
                "Square, bracket, and tick mark families all reserve a fixed-width active mark before the label, so toggling never shifts the surrounding text.",
                Doc.Column(square, brackets, tick)),
            Doc.Example(
                "Three-state selection",
                "Setting IsThreeState lets IsChecked hold null. Space and pointer activation then cycle unchecked, checked, and indeterminate in order.",
                Doc.Column(unchecked_, checked_, indeterminate)),
            Doc.Example(
                "Disabled",
                "A disabled CheckBox keeps its current mark visible while ignoring keyboard and pointer activation and refusing focus.",
                disabled));
    }
}
