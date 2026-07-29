// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents standard asynchronous MessageBox layouts and modal result handling.</summary>
internal sealed class MessageBoxPane: CompositeControl
{
    /// <summary>Initializes the retained MessageBox showcase content.</summary>
    internal MessageBoxPane() => InitializeContent(CreateContent());

    /// <summary>Gets the exact catalog/page name.</summary>
    internal const string Title = "MessageBox";

    private static Dock CreateContent()
    {
        var status = new Text("Result: waiting");
        var ok = CreateLauncher(status, "&OK", MessageBoxButtons.Ok, "A single affirmative action.");
        var okCancel = CreateLauncher(status, "O&K / Cancel", MessageBoxButtons.OkCancel, "Save changes before leaving?");
        var yesNo = CreateLauncher(status, "&Yes / No", MessageBoxButtons.YesNo, "Enable the experimental renderer?");
        var yesNoCancel = CreateLauncher(status, "Y&es / No / Cancel", MessageBoxButtons.YesNoCancel, "Apply this change to every workspace?");
        var launchers = Doc.Row(ok, okCancel, yesNo, yesNoCancel);
        launchers.Spacing = 1;

        return Doc.Page(
            Title,
            "<info>MessageBox</info> shows a measured, centered dialog whose title, wrapped message, and buttons scale with the available terminal space.",
            Doc.Section(
                "💬",
                "Standard button layouts",
                "Open each variant to present a centered dialog over the complete showcase. <reverse>Tab</reverse> moves between buttons; <reverse>Escape</reverse> dismisses without a positive result.",
                Doc.Column(launchers, status)));
    }

    private static Button CreateLauncher(
        Text status,
        string label,
        MessageBoxButtons buttons,
        string message)
    {
        var launcher = new Button { Content = new Text(label) };
        launcher.Click += async (_, _) =>
        {
            var result = await MessageBox.ShowAsync(launcher, message, "MessageBox", buttons);
            var dispatcher = status.Dispatcher ?? throw new InvalidOperationException(
                "The showcase status must remain attached while the MessageBox is open.");
            dispatcher.Post(() => status.Content = $"Result: {result}");
        };
        return launcher;
    }
}
