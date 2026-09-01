// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents Breadcrumb paths, overflow, participation, styling, and activation.</summary>
internal sealed class BreadcrumbPane: CompositeControlBase
{
    /// <summary>Initializes the retained Breadcrumb showcase content.</summary>
    internal BreadcrumbPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Breadcrumb";

    /// <summary>Creates the retained documentation page and all live Breadcrumb specimens.</summary>
    /// <returns>The complete page root.</returns>
    private static DocPage CreateContent()
    {
        var activity = new Text { Overflow = Overflow.Wrap };
        var activityEntries = new List<string>();

        void AppendActivity(string entry)
        {
            activityEntries.Add(entry);

            while (activityEntries.Count > 4)
            {
                activityEntries.RemoveAt(0);
            }

            activity.Content = "<d>Activity</d>\n" +
                string.Join('\n', activityEntries.Select(static value => Text.Escape($"• {value}")));
        }

        var interactive = new Breadcrumb { Width = Length.Percent(100) };
        var home = CreateItem("&Home", "Home", AppendActivity);
        var projects = CreateItem("Pr&ojects", "Projects", AppendActivity);
        var design = CreateItem("界 &Design", "界 Design", AppendActivity);
        var release = CreateItem("Relea&se 🚀", "Release 🚀", AppendActivity);
        interactive.Items.Add(home);
        interactive.Items.Add(projects);
        interactive.Items.Add(design);
        interactive.Items.Add(release);
        interactive.CurrentChanged += (_, eventArgs) =>
            AppendActivity($"CurrentChanged: {Plain(eventArgs.PreviousItem)} → {Plain(eventArgs.CurrentItem)}");
        AppendActivity("Ready: Release 🚀 is current.");

        var clearCurrent = new Button { Text = "Clear &current" };
        clearCurrent.Click += (_, _) =>
        {
            interactive.CurrentIndex = -1;
            AppendActivity("Current: none (explicit state).");
        };

        var restoreLeaf = new Button { Text = "Restore &leaf" };
        restoreLeaf.Click += (_, _) =>
        {
            interactive.CurrentItem = release;
            AppendActivity("Current: Release 🚀 restored.");
        };

        var toggleWidth = new Button { Text = "&Narrow path" };
        toggleWidth.Click += (_, _) =>
        {
            var isNarrow = interactive.Width == Length.Cells(18);
            interactive.Width = isNarrow ? Length.Percent(100) : Length.Cells(18);
            toggleWidth.Text = isNarrow ? "&Narrow path" : "&Widen path";
            AppendActivity(isNarrow
                ? "Width: live reading column restored."
                : "Width: narrow overflow projection active.");
        };
        var actions = new Wrap { Width = Length.Percent(100), Spacing = 1, LineSpacing = 1 };
        actions.Children.Add(clearCurrent);
        actions.Children.Add(restoreLeaf);
        actions.Children.Add(toggleWidth);

        var overflow = new Breadcrumb
        {
            Width = Length.Cells(18),
            Style = BreadcrumbStyle.Default with
            {
                SeparatorGlyph = new ControlGlyph(new Rune('/'), new Rune('>')),
                SeparatorColor = SemanticColor.Accent
            }
        };
        overflow.Items.Add(new BreadcrumbItem { Text = "Wo&rkspace" });
        overflow.Items.Add(new BreadcrumbItem { Text = "A&pplications" });
        overflow.Items.Add(new BreadcrumbItem { Text = "界 Desi&gn" });
        overflow.Items.Add(new BreadcrumbItem { Text = "Releas&e 🚀" });

        var participation = new Breadcrumb { Width = Length.Cells(39) };
        participation.Items.Add(new BreadcrumbItem { Text = "Roo&t" });
        participation.Items.Add(new BreadcrumbItem
        {
            Text = "Hi&dden cache",
            Visibility = Visibility.Hidden
        });
        participation.Items.Add(new BreadcrumbItem
        {
            Text = "Collapsed bran&ch",
            Visibility = Visibility.Collapsed
        });
        participation.Items.Add(new BreadcrumbItem
        {
            Text = "Locked",
            IsEnabled = false
        });
        participation.Items.Add(new BreadcrumbItem { Text = "A&vailable leaf" });

        return new DocPage(
            Title,
            "<info>Breadcrumb</info> retains a root-to-location path, keeps semantic current state separate from keyboard movement, and compresses whole Unicode entries into one overflow menu.",
            new DocSection(
                "🧭",
                "Current path and commands",
                "Use the mnemonic keys or focus the owner and move with arrows. Activation commits current before publishing the item event and captured command.",
                new DocExample(
                    "Unicode project path",
                    "The path follows the live reading-column width. Clear current to reveal prefix-first overflow behavior; restore the leaf to return to the conventional final location.",
                    new DocColumn(
                        interactive,
                        actions,
                        activity),
                    "var path = new Breadcrumb();\n" +
                    "path.Items.Add(new BreadcrumbItem { Text = \"&Home\" });\n" +
                    "path.Items.Add(new BreadcrumbItem { Text = \"界 &Design\" });\n" +
                    "path.CurrentChanged += (_, args) => Navigate(args.CurrentItem);")),
            new DocSection(
                "📐",
                "Automatic overflow",
                "Finite width preserves complete entries and separators. The retained overflow trigger projects omitted available sources without reparenting them.",
                new DocExample(
                    "Automatic overflow",
                    "This 18-cell path keeps the current suffix and exposes earlier available locations through one menu. Its local style uses an accent slash separator.",
                    overflow,
                    "path.Style = BreadcrumbStyle.Default with\n" +
                    "{\n" +
                    "    SeparatorGlyph = new ControlGlyph(new Rune('/'), new Rune('>')),\n" +
                    "    SeparatorColor = SemanticColor.Accent\n" +
                    "};")),
            new DocSection(
                "👁️",
                "Participation states",
                "Hidden retains its measured slot, Collapsed releases it, and disabled entries remain authored but cannot become current, active, or projected.",
                new DocExample(
                    "Unavailable ancestors",
                    "The available leaf remains the final represented location while the three unavailable ancestors keep their distinct ownership and layout semantics.",
                    participation)));
    }

    /// <summary>Creates one mnemonic-aware command item with observable activation.</summary>
    /// <param name="text">The authored caption.</param>
    /// <param name="name">The plain location name used by the activity readout.</param>
    /// <param name="appendActivity">The retained ordered readout callback updated by item callbacks.</param>
    /// <returns>The initialized breadcrumb item.</returns>
    private static BreadcrumbItem CreateItem(string text, string name, Action<string> appendActivity)
    {
        var item = new BreadcrumbItem { Text = text, CommandParameter = name };
        item.Invoked += (_, eventArgs) => appendActivity($"Invoked: {name} ({eventArgs.Cause})");
        item.Command = new ShowcaseCommand(
            parameter => appendActivity($"Command: {parameter}"),
            static _ => true);
        return item;
    }

    /// <summary>Returns a current-item caption without mnemonic syntax for an event readout.</summary>
    /// <param name="item">The current or previous item, or null.</param>
    /// <returns>The plain caption or <c>none</c>.</returns>
    private static string Plain(BreadcrumbItem? item) =>
        item is null ? "none" : DocCaption.PlainCaption(item.Text);
}
