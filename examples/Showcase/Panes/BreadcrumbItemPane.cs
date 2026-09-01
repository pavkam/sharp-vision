// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents BreadcrumbItem interaction, current state, availability, Unicode, and styling.</summary>
internal sealed class BreadcrumbItemPane: CompositeControlBase
{
    /// <summary>Initializes the retained BreadcrumbItem documentation page.</summary>
    internal BreadcrumbItemPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "BreadcrumbItem";

    /// <summary>Creates the retained documentation page and its owner-backed item specimen.</summary>
    /// <returns>The complete page root.</returns>
    private static DocPage CreateContent()
    {
        var itemLog = new Text("BreadcrumbItem.Invoked: waiting") { Overflow = Overflow.Wrap };
        var currentLog = new Text("Breadcrumb.CurrentChanged: waiting") { Overflow = Overflow.Wrap };
        var commandLog = new Text("ICommand: waiting") { Overflow = Overflow.Wrap };
        var availabilityLog = new Text("Design availability: enabled") { Overflow = Overflow.Wrap };

        var home = new BreadcrumbItem { Text = "&Home" };
        var design = new BreadcrumbItem
        {
            Text = "界 &Design 🚀",
            Style = BreadcrumbItemStyle.Default with
            {
                Face = BreadcrumbItemStyle.Default.Face with
                {
                    Foreground = SemanticColor.Accent,
                    Attributes = TerminalAttributes.Bold
                }
            },
            CommandParameter = "design-system"
        };
        design.Invoked += (_, eventArgs) =>
            itemLog.Content = $"BreadcrumbItem.Invoked: 界 Design 🚀 ({eventArgs.Cause})";
        design.Command = new ShowcaseCommand(
            parameter => commandLog.Content = $"ICommand: navigated to {parameter}",
            static _ => true);
        var archive = new BreadcrumbItem { Text = "&Archive", IsEnabled = false };

        var path = new Breadcrumb { Width = Length.Cells(42) };
        path.Items.Add(home);
        path.Items.Add(archive);
        path.Items.Add(design);
        path.CurrentChanged += (_, eventArgs) =>
            currentLog.Content =
                $"Breadcrumb.CurrentChanged: {Plain(eventArgs.PreviousItem)} → {Plain(eventArgs.CurrentItem)}";

        var invoke = new Button { Text = "Invoke &programmatically" };
        invoke.Click += (_, _) => design.PerformInvoke();
        var availability = new Button { Text = "Toggle a&vailability" };
        availability.Click += (_, _) =>
        {
            design.IsEnabled = !design.IsEnabled;
            availabilityLog.Content = design.IsEnabled
                ? "Design availability: enabled"
                : "Design availability: disabled · activation is inert";
        };

        return new DocPage(
            Title,
            "<info>BreadcrumbItem</info> is one command-bearing retained location. Its owner keeps the item out of the Tab sequence, commits semantic current state first, and then publishes the item's event and captured command.",
            new DocSection(
                "🧭",
                "Semantic path item",
                "The styled Unicode Design item exposes mnemonic text, current state, availability, an item event, and ICommand execution. Archive is a visible unavailable ancestor before the current Design item.",
                new DocExample(
                    "Current item activation and availability",
                    "Activate Home or Design through the owner, invoke Design programmatically, or disable it. The readouts show the named item's event after current-state commit and before command execution.",
                    new DocColumn(
                        path,
                        new DocRow(invoke, availability),
                        availabilityLog,
                        currentLog,
                        itemLog,
                        commandLog),
                    "var item = new BreadcrumbItem { Text = \"界 &Design 🚀\" };\nitem.Invoked += (_, args) => Log(args.Cause);\npath.Items.Add(item);\nitem.PerformInvoke();")));
    }

    /// <summary>Returns a breadcrumb-item caption without mnemonic syntax.</summary>
    /// <param name="item">The current or previous item, or null.</param>
    /// <returns>The plain caption or <c>none</c>.</returns>
    private static string Plain(BreadcrumbItem? item) =>
        item is null ? "none" : DocCaption.PlainCaption(item.Text);
}
