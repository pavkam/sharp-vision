// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Stack control with orientation, spacing, and reverse-order specimens.</summary>
internal sealed class StackPane: CompositeControlBase
{
    internal StackPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Stack";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Fixed, percentage, and star widths share one horizontal axis.
        Stack horizontal = new() { Orientation = Orientation.Horizontal, Spacing = 2, Width = Length.Cells(40) };
        var fixedCard = Card("Fixed 10", BorderGlyphStyle.Light);
        fixedCard.Width = Length.Cells(10);
        horizontal.Children.Add(fixedCard);
        var percentCard = Card("35%", BorderGlyphStyle.Heavy);
        percentCard.Width = Length.Percent(35);
        horizontal.Children.Add(percentCard);
        var starCard = Card("1*", BorderGlyphStyle.Paired);
        starCard.Width = Length.Star(1);
        horizontal.Children.Add(starCard);

        // Reverse changes presentation and focus order without reparenting children.
        Stack reversed = new() { Orientation = Orientation.Horizontal, Spacing = 2, Reverse = true };
        var reverseStatus = new Text("Activation: none\nVisible + Tab: Third → Second → First")
        {
            Width = Length.Cells(44),
            Overflow = Overflow.Wrap
        };
        var first = new Button { Text = "&First" };
        var second = new Button { Text = "S&econd" };
        var third = new Button { Text = "&Third" };
        first.Click += (_, _) => reverseStatus.Content = "Activation: First (source index 0).";
        second.Click += (_, _) => reverseStatus.Content = "Activation: Second (source index 1).";
        third.Click += (_, _) => reverseStatus.Content = "Activation: Third (source index 2).";
        reversed.Children.Add(first);
        reversed.Children.Add(second);
        reversed.Children.Add(third);

        // Vertical stack with explicit region heights.
        Stack vertical = new() { Spacing = 1, Width = Length.Cells(46) };
        var vHeader = Card("Header region (3 rows)", BorderGlyphStyle.Rounded, new Thickness(1, 0));
        vHeader.Height = Length.Cells(3);
        var vContent = Card(
            "Content region (5 rows)\nSpacing = 1 between children",
            BorderGlyphStyle.Light,
            new Thickness(1, 0),
            Overflow.Wrap);
        vContent.Height = Length.Cells(5);
        var vFooter = Card("Footer region (3 rows)", BorderGlyphStyle.Heavy, new Thickness(1, 0));
        vFooter.Height = Length.Cells(3);
        vertical.Children.Add(vHeader);
        vertical.Children.Add(vContent);
        vertical.Children.Add(vFooter);

        // Horizontal orientation with fixed sidebar and star main area.
        var horizontalOrientation =
            new Stack { Orientation = Orientation.Horizontal, Spacing = 1, Width = Length.Cells(46) };
        var hNav = Card("Sidebar\n12 cells", BorderGlyphStyle.Rounded, new Thickness(1, 0), Overflow.Wrap);
        hNav.Width = Length.Cells(12);
        hNav.Height = Length.Cells(6);
        var hMain = Card("Main area\nFills remaining width", BorderGlyphStyle.Light, new Thickness(1, 0), Overflow.Wrap);
        hMain.Width = Length.Star(1);
        hMain.Height = Length.Cells(6);
        horizontalOrientation.Children.Add(hNav);
        horizontalOrientation.Children.Add(hMain);

        // Margin belongs outside a child; spacing belongs between participating tracks.
        var margins = new Stack { Orientation = Orientation.Horizontal, Spacing = 1 };
        var marginCard = Card("Margin 2", BorderGlyphStyle.Light);
        marginCard.Margin = new Thickness(2, 0);
        margins.Children.Add(marginCard);
        margins.Children.Add(Card("Spacing 1", BorderGlyphStyle.Heavy));

        // Hidden keeps its track; Collapsed releases it and adjacent spacing.
        var optional = Card("Optional", BorderGlyphStyle.Rounded);
        var visibility = new Stack { Spacing = 1 };
        visibility.Children.Add(Card("Before", BorderGlyphStyle.Light));
        visibility.Children.Add(optional);
        visibility.Children.Add(Card("After", BorderGlyphStyle.Heavy));
        var visibilityStatus = new Text("Optional: visible; its track and content render.")
        {
            Width = Length.Cells(44),
            Overflow = Overflow.Wrap
        };
        var cycleVisibility = new Button { Text = "Cycle &visibility" };
        cycleVisibility.Click += (_, _) =>
        {
            optional.Visibility = optional.Visibility == Visibility.Visible
                ? Visibility.Hidden
                : optional.Visibility == Visibility.Hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            visibilityStatus.Content = optional.Visibility == Visibility.Hidden
                ? "Optional: hidden; its track and adjacent spacing remain."
                : optional.Visibility == Visibility.Collapsed
                    ? "Optional: collapsed; its track and adjacent spacing are released."
                    : "Optional: visible; its track and content render.";
        };

        // Container-owned scrolling exposes separate keyboard and wheel input policies.
        var scrolling = new Stack
        {
            Width = Length.Cells(44),
            Height = Length.Cells(7),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            IsFocusable = true,
            IsTabStop = true,
            LineSize = 1,
            PageOverlap = 1
        };

        for (var index = 1; index <= 12; index++)
        {
            scrolling.Children.Add(new Text($"Row {index:00} · retained container content"));
        }

        var scrollStatus = new Text("Offset: 0 · cause: none\nKeyboard: on · wheel: on")
        {
            Width = Length.Cells(44),
            Overflow = Overflow.Wrap
        };
        var focusViewport = new Button { Text = "Foc&us viewport" };
        var keyboardScrolling = new CheckBox { Text = "&Keyboard scrolling", IsChecked = true };
        var wheelScrolling = new CheckBox { Text = "&Wheel scrolling", IsChecked = true };

        focusViewport.Click += (_, _) =>
        {
            var focused = scrolling.Focus();
            UpdateScrollStatus(focused ? "focus" : "focus unavailable");
        };
        keyboardScrolling.StateChanged += (_, _) =>
        {
            scrolling.IsKeyboardScrollingEnabled = keyboardScrolling.IsChecked == true;
            UpdateScrollStatus("policy");
        };
        wheelScrolling.StateChanged += (_, _) =>
        {
            scrolling.IsWheelScrollingEnabled = wheelScrolling.IsChecked == true;
            UpdateScrollStatus("policy");
        };
        scrolling.ScrollChanged += (_, eventArgs) => UpdateScrollStatus(eventArgs.Cause.ToString());

        // Over-constrained requests saturate safely instead of producing negative geometry.
        var constrained = new Stack { Orientation = Orientation.Horizontal, Width = Length.Cells(16), Spacing = 2 };
        var constrainedFixed = Card("Fixed", BorderGlyphStyle.Light);
        constrainedFixed.Width = Length.Cells(12);
        var constrainedStar = Card("Star", BorderGlyphStyle.Paired);
        constrainedStar.Width = Length.Star(1);
        constrained.Children.Add(constrainedFixed);
        constrained.Children.Add(constrainedStar);

        // Star spacer pushes the primary action to the trailing edge.
        var actionBar = new Stack { Orientation = Orientation.Horizontal, Spacing = 1, Width = Length.Cells(40) };
        actionBar.Children.Add(new Button { Text = "&Cancel" });
        var spacer = new Dock { Width = Length.Star(1) };
        actionBar.Children.Add(spacer);
        actionBar.Children.Add(new Button { Text = "&Save" });

        return new DocPage(
            Title,
            "<info>Stack</info> arranges children sequentially with fixed, automatic, percentage, or proportional lengths and stable <info>Spacing</info>.",
            new DocSection(
                "📚",
                "Orientation",
                "Choose the sequential axis; the child collection remains the authoritative source for child identity and keyboard navigation.",
                new DocExample(
                    "Vertical and horizontal",
                    "Vertical is the default. Horizontal places the same kind of children left to right.",
                    new DocColumn(vertical, horizontalOrientation),
                    "var actions = new Stack\n{\n    Orientation = Orientation.Horizontal,\n    Spacing = 1,\n};")),
            new DocSection(
                "📐",
                "Mixed sizing",
                "Fixed, percentage, automatic, and proportional lengths share one deterministic axis allocation.",
                new DocExample(
                    "Fixed, percent, and star",
                    "The fixed card reserves ten cells, percentage resolves once against the inner width, and star receives the remainder.",
                    horizontal)),
            new DocSection(
                "↔️",
                "Spacing and margins",
                "Stack spacing belongs between participating tracks; margins belong outside individual children.",
                new DocExample(
                    "Two different gaps",
                    "Compare the two-cell external margin around the first card with the one-cell inter-child spacing.",
                    margins)),
            new DocSection(
                "🔄",
                "Reverse",
                "Reverse changes geometry, rendering, selectable-text reading order, and default focus traversal without reparenting children.",
                new DocExample(
                    "Stable source; reversed presentation",
                    "Activate the buttons or Tab through them: source order remains First, Second, Third while visible and keyboard order runs in reverse.",
                    new DocColumn(reversed, reverseStatus))),
            new DocSection(
                "👁️",
                "Visibility",
                "Hidden children retain a track while Collapsed children consume neither a track nor adjacent spacing.",
                new DocExample(
                    "Visible, hidden, and collapsed",
                    "Cycle the optional card to compare rendered content, retained geometry, and released geometry directly.",
                    new DocColumn(cycleVisibility, visibilityStatus, visibility))),
            new DocSection(
                "🧭",
                "Scrolling input",
                "AutoScroll belongs to every Container; keyboard and wheel handling can be enabled independently while the same viewport, bars, offsets, and programmatic API remain active.",
                new DocExample(
                    "One viewport; two input policies",
                    "Focus the viewport for arrows, Page Up/Down, Home, and End. Wheel over its rows, then disable either policy and verify that input remains available to the routed ancestor.",
                    new DocColumn(
                        new DocRow(focusViewport, keyboardScrolling),
                        new DocRow(wheelScrolling),
                        scrollStatus,
                        scrolling),
                    "var panel = new Stack\n{\n    AutoScroll = true,\n    IsKeyboardScrollingEnabled = true,\n    IsWheelScrollingEnabled = true,\n};")),
            new DocSection(
                "📏",
                "Constrained space",
                "The container's size limit prevails when requests and spacing cannot fit; later flexible tracks may shrink safely to zero.",
                new DocExample(
                    "Saturated allocation",
                    "A twelve-cell fixed card and spacing leave only the safe remainder for the proportional card.",
                    constrained)),
            new DocSection(
                "🧩",
                "Action-bar recipe",
                "A proportional spacer pushes the primary command to the trailing edge without absolute positioning.",
                new DocExample(
                    "Secondary and primary actions",
                    "Resize the page and the spacer absorbs the changing remainder between Cancel and Save.",
                    actionBar)));

        void UpdateScrollStatus(string cause)
        {
            scrollStatus.Content =
                $"Offset: {scrolling.VerticalOffset} · cause: {cause}\n" +
                $"Keyboard: {(scrolling.IsKeyboardScrollingEnabled ? "on" : "off")} · " +
                $"wheel: {(scrolling.IsWheelScrollingEnabled ? "on" : "off")}";
        }
    }

    private static Dock Card(
        string text,
        BorderGlyphStyle glyphs,
        Thickness? padding = null,
        Overflow overflow = Overflow.Visible) =>
        ShowcasePaneHelpers.Card(text, glyphs, padding, overflow);
}
