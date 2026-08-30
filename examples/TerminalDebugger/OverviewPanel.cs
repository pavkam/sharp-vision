// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Displays terminal identity, profile, services, modes, and renderer selection.</summary>
internal sealed class OverviewPanel: CompositeControlBase
{
    private readonly Text _content;

    /// <summary>Initializes the scrollable overview.</summary>
    internal OverviewPanel()
    {
        _content = new Text("<d>Waiting for terminal diagnostics…</d>")
        {
            Overflow = Overflow.Wrap
        };
        InitializeContent(new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Padding = new Thickness(1),
            Children = { _content }
        });
    }

    /// <summary>Refreshes all public environment and runtime facts.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void Refresh(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var description = application.Terminal.Description;
        var diagnostics = application.TerminalDiagnostics;
        var modes = diagnostics.Modes;
        var evidence = diagnostics.BackendEvidence.Count == 0
            ? "none; conservative fallback"
            : string.Join(", ", diagnostics.BackendEvidence.Select(
                static item => $"{item.Family} from {item.Source}"));

        _content.Content =
            $"<accent><b>Connection</b></accent>\n" +
            $"Description: <info>{TextMarkup.Escape(description.Name)}</info> ({description.Origin}, {description.Suitability})\n" +
            $"Geometry: <info>{application.Size.Width}×{application.Size.Height}</info> cells\n" +
            $"Color: {application.Capabilities.ColorDepth} ({application.Capabilities.ColorOrigin})\n" +
            $"Unicode: {application.Capabilities.UnicodeVersion}; ambiguous width {application.Capabilities.AmbiguousWidth}\n\n" +
            $"<accent><b>Fixed backend identity</b></accent>\n" +
            $"Selected: <info>{TextMarkup.Escape(diagnostics.BackendName)}</info> ({diagnostics.BackendFamily})\n" +
            $"Composition: {string.Join(" → ", diagnostics.BackendExtensions)}\n" +
            $"Evidence: {TextMarkup.Escape(evidence)}\n" +
            "Identity is fixed for this application lifetime; capability evidence may refine.\n\n" +
            $"<accent><b>Runtime selections</b></accent>\n" +
            $"Negotiation: {diagnostics.NegotiationState}\n" +
            $"Graphics backend: <info>{diagnostics.GraphicsBackend}</info>\n" +
            $"Focus: {ConfiguredState(modes.FocusReportingConfigured, modes.FocusReportingAuthorized, modes.FocusReportingActive)}\n" +
            $"Bracketed paste: {ConfiguredState(modes.BracketedPasteConfigured, modes.BracketedPasteAuthorized, modes.BracketedPasteActive)}\n" +
            $"Mouse: {FormatMouse(modes)}\n" +
            $"Kitty keyboard: {ConfiguredState(modes.KittyKeyboardEnhancements.HasValue, modes.KittyKeyboardAuthorized, modes.KittyKeyboardActive)}" +
            $" ({modes.KittyKeyboardEnhancements?.ToString() ?? "not configured"})\n" +
            $"xterm modifyOtherKeys: {ConfiguredState(modes.ModifyOtherKeysLevel.HasValue, modes.ModifyOtherKeysAuthorized, modes.ModifyOtherKeysActive)}" +
            $" (level {modes.ModifyOtherKeysLevel?.ToString(CultureInfo.InvariantCulture) ?? "—"})\n" +
            $"Kitty clipboard paste events: {ConfiguredState(modes.ClipboardPasteEventsConfigured, modes.ClipboardPasteEventsAuthorized, modes.ClipboardPasteEventsActive)}\n\n" +
            $"<accent><b>Public services</b></accent>\n" +
            $"Title {YesNo(application.Terminal.IsTitleSupported)} · " +
            $"Bell {YesNo(application.Terminal.Bell.IsSupported)} · " +
            $"Clipboard {YesNo(application.Terminal.Clipboard.IsSupported)} · " +
            $"Notifications {YesNo(application.Terminal.Notifications.IsSupported)}";
    }

    private static string ConfiguredState(bool configured, bool authorized, bool active) => active
        ? "<success>active</success>"
        : configured
            ? authorized
                ? "<warning>authorized but not active</warning>"
                : "<warning>requested but not authorized</warning>"
            : "<d>disabled</d>";

    private static string FormatMouse(TerminalModeDiagnostics modes) => modes.MouseActive
        ? $"<success>active</success> ({modes.MouseTracking}, {modes.MouseCoordinates})"
        : modes.MouseTracking.HasValue
            ? modes.MouseAuthorized
                ? $"<warning>authorized but not active</warning> ({modes.MouseTracking}, {modes.MouseCoordinates})"
                : $"<warning>requested but not authorized</warning> ({modes.MouseTracking}, {modes.MouseCoordinates})"
            : "<d>disabled</d>";

    private static string YesNo(bool value) => value ? "<success>available</success>" : "<d>unavailable</d>";
}
