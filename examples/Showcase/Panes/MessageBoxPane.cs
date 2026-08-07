// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents standard asynchronous MessageBox layouts and modal result handling.</summary>
internal sealed class MessageBoxPane: CompositeControlBase
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
        var longMessage = CreateLauncher(
            status,
            "&Long message",
            MessageBoxButtons.Ok,
            "This message is deliberately long enough to demonstrate the responsive width cap: " +
            "the dialog grows toward, but never past, 80% of the available presentation width, " +
            "wraps its prose within that width, and separates the message from the action row " +
            "with a horizontal divider.");
        var localized = CreateLocalizedLauncher(status);
        var launchers = Doc.Row(ok, okCancel, yesNo, yesNoCancel, longMessage, localized);
        launchers.Spacing = 1;

        return Doc.Page(
            Title,
            "<info>MessageBox</info> shows a measured, centered dialog on the theme's raised window surface; its title, wrapped message, and buttons scale with the available terminal space, capped at 80% of the available width.",
            Doc.Section(
                "💬",
                "Standard button layouts",
                "Open each variant to present a centered dialog over the complete showcase. <reverse>Tab</reverse> moves between buttons; <reverse>Escape</reverse> dismisses without a positive result. \"Long message\" demonstrates the responsive width cap - the dialog grows toward, but never past, 80% of the available presentation width, wrapping by grapheme cluster and separating the message from its action row with a divider. \"Localized (options)\" demonstrates the MessageBoxOptions carrier: custom captions and a local style configured in one call, without exposing the generated Buttons or divider.",
                Doc.Column(launchers, status)));
    }

    private static Button CreateLauncher(
        Text status,
        string label,
        MessageBoxButtons buttons,
        string message)
    {
        var launcher = new Button { Text = label };
        launcher.Click += async (_, _) =>
        {
            var result = await MessageBox.ShowAsync(launcher, message, "MessageBox", buttons);
            var dispatcher = status.Dispatcher ?? throw new InvalidOperationException(
                "The showcase status must remain attached while the MessageBox is open.");
            dispatcher.Post(() => status.Content = $"Result: {result}");
        };
        return launcher;
    }

    private static Button CreateLocalizedLauncher(Text status)
    {
        var launcher = new Button { Text = "Localized (&options)" };
        launcher.Click += async (_, _) =>
        {
            var options = new MessageBoxOptions
            {
                Title = "Confirmar",
                Buttons = MessageBoxButtons.YesNoCancel,
                YesText = "&Sí",
                NoText = "&No",
                CancelText = "&Cancelar",
                Style = MessageBoxStyle.Default with { ActionBarMargin = new Thickness(3, 0) }
            };
            var result = await MessageBox.ShowAsync(launcher, "¿Eliminar el borrador?", options);
            var dispatcher = status.Dispatcher ?? throw new InvalidOperationException(
                "The showcase status must remain attached while the MessageBox is open.");
            dispatcher.Post(() => status.Content = $"Result: {result}");
        };
        return launcher;
    }
}
