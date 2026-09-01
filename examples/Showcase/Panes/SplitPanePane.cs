// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents SplitPane sizing, interaction, availability, and responsive behavior.</summary>
internal sealed class SplitPanePane: CompositeControlBase
{
    /// <summary>Initializes the retained SplitPane documentation surface.</summary>
    internal SplitPanePane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "SplitPane";

    /// <summary>Creates the retained documentation page and all live SplitPane specimens.</summary>
    /// <returns>The complete page root.</returns>
    private static DocPage CreateContent()
    {
        var narrow = CreateSplit(
            Length.Cells(18),
            Length.Percent(35),
            "35%\nNav",
            "Narrow\ncontent");
        var normal = CreateSplit(
            Length.Cells(30),
            Length.Cells(10),
            "10 cells\nFiles",
            "Normal editor");
        var wide = CreateSplit(
            Length.Percent(100),
            Length.Percent(32),
            "32% sidebar",
            "Wide workspace follows the reading column");

        var vertical = new SplitPane
        {
            Orientation = Orientation.Vertical,
            Height = Length.Cells(8),
            FirstPaneLength = Length.Percent(60),
            Children =
            {
                new Text("Editor\nProgram.cs\nConsole.WriteLine(\"SharpVision\");"),
                new Text("Output\nBuild succeeded")
            }
        };

        var primary = new TextInput { Text = "Edit pane input" };
        var secondary = new Text("Workspace\nTab to pane content after the divider.");
        var status = new Text(
            "Focus the divider for resize commands, then Tab to the editable pane and type without resizing.")
        {
            Overflow = Overflow.Wrap
        };
        var interactive = new SplitPane
        {
            Height = Length.Cells(5),
            FirstPaneLength = Length.Percent(40),
            SmallChange = 2,
            LargeChange = 6,
            Children = { primary, secondary }
        };
        interactive.GotFocus += (_, _) =>
            status.Content =
                "Keyboard ready: arrows move 2 cells; Page Up/Page Down move 6; Home/End use the feasible limits.";
        interactive.PointerMoved += (_, _) =>
        {
            if (interactive.HasPointerCapture)
            {
                status.Content = Text.Escape(
                    $"Pointer drag active: {DescribeLength(interactive.FirstPaneLength)}");
            }
        };
        interactive.SplitChanged += (_, eventArgs) =>
            status.Content = Text.Escape(
                $"Split committed: {DescribeLength(eventArgs.PreviousLength)} → " +
                DescribeLength(eventArgs.Length));
        primary.GotFocus += (_, _) =>
            status.Content = "Pane input focused after Tab; descendant typing remains editor input.";
        primary.TextChanged += (_, eventArgs) =>
            status.Content = Text.Escape($"Pane input changed without resizing: {eventArgs.Text}");

        var toggleResizable = new Button { Text = "&Lock divider" };
        toggleResizable.Click += (_, _) =>
        {
            interactive.IsResizable = !interactive.IsResizable;
            toggleResizable.Text = interactive.IsResizable ? "&Lock divider" : "Un&lock divider";
            status.Content = interactive.IsResizable
                ? "Divider resizing restored; Tab can stop on the visible divider."
                : "Divider locked; Tab skips it while pane input remains available.";
        };

        var toggleEnabled = new Button { Text = "&Disable split" };
        toggleEnabled.Click += (_, _) =>
        {
            interactive.IsEnabled = !interactive.IsEnabled;
            toggleEnabled.Text = interactive.IsEnabled ? "&Disable split" : "&Enable split";
            status.Content = interactive.IsEnabled
                ? "Split enabled; divider and pane input are available again."
                : "Split disabled; its divider releases interaction and its descendants inherit disabled state.";
        };

        var toggleSecond = new Button { Text = "Collapse &second" };
        toggleSecond.Click += (_, _) =>
        {
            var collapsed = secondary.Visibility == Visibility.Collapsed;
            secondary.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            toggleSecond.Text = collapsed ? "Collapse &second" : "Restore &second";
            status.Content = collapsed
                ? "Second pane restored; the divider participates again."
                : "Second pane collapsed; the first pane fills the split and the divider is absent.";
        };
        var actions = new Wrap { Width = Length.Percent(100), Spacing = 1, LineSpacing = 1 };
        actions.Children.Add(toggleResizable);
        actions.Children.Add(toggleEnabled);
        actions.Children.Add(toggleSecond);

        return new DocPage(
            Title,
            "<info>SplitPane</info> keeps two retained panes around one live divider whose fixed or percentage position responds to keyboard, pointer, constraints, and viewport changes.",
            new DocSection(
                "📐",
                "Responsive sidebars",
                "A fixed leading pane keeps its authored cells; a percentage leading pane follows the divider-excluded reading column.",
                new DocExample(
                    "Narrow, normal, and wide",
                    "The same public control contains both panes at 18 cells, 30 cells, and the full reading-column width without a fixed over-wide specimen.",
                    new DocColumn(narrow, normal, wide),
                    "var split = new SplitPane\n{\n    FirstPaneLength = Length.Percent(35),\n    Children = { navigation, workspace },\n};")),
            new DocSection(
                "↕️",
                "Vertical workspace",
                "Changing <info>Orientation</info> maps the same allocation and commands from width to height.",
                new DocExample(
                    "Editor and output",
                    "The editor receives 60% of the divider-excluded height and the output fills the remainder.",
                    vertical)),
            new DocSection(
                "⌨️",
                "Interaction and availability",
                "Focus or drag the divider and read each committed split; Tab onward to edit pane content, then lock, disable, or collapse the split to observe deterministic fallback.",
                new DocExample(
                    "Keyboard and pointer status",
                    "Tab reaches the divider before the editable leading pane while it is available. Typing updates the readout without resizing; locking or collapsing skips the divider stop without removing pane input.",
                    new DocColumn(
                        interactive,
                        actions,
                        status))));
    }

    /// <summary>Creates one responsive horizontal split with two plain text panes.</summary>
    /// <param name="width">The specimen width inside the current reading column.</param>
    /// <param name="firstPaneLength">The leading pane's fixed or percentage request.</param>
    /// <param name="first">The leading pane text.</param>
    /// <param name="second">The trailing pane text.</param>
    /// <returns>The initialized split.</returns>
    private static SplitPane CreateSplit(Length width, Length firstPaneLength, string first, string second) => new()
    {
        Width = width,
        Height = Length.Cells(3),
        FirstPaneLength = firstPaneLength,
        Children = { new Text(first), new Text(second) }
    };

    /// <summary>Formats one authored split length for a live interaction readout.</summary>
    /// <param name="length">The fixed-cell or percentage length to describe.</param>
    /// <returns>A culture-independent visible length.</returns>
    private static string DescribeLength(Length length) => length.Kind == LengthKind.Cells
        ? FormattableString.Invariant($"{length.Value:0} cells")
        : FormattableString.Invariant($"{length.Value:0.##}%");
}
