// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

using Kitty.Keyboard;

using Runtime;

/// <summary>Describes configured, authorized, and successfully activated terminal modes.</summary>
[PublicAPI]
public sealed class TerminalModeDiagnostics
{
    private readonly MultiplexerRoute? _clipboardRoute;
    private readonly bool _clipboardCapabilityAuthorized;

    /// <summary>Initializes one mode snapshot from validated runtime options and capabilities.</summary>
    /// <param name="options">The non-null session options.</param>
    /// <param name="capabilities">The non-null active capability evidence.</param>
    /// <exception cref="ArgumentNullException">A required value is null.</exception>
    internal TerminalModeDiagnostics(TerminalOptions options, TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(capabilities);

        AlternateScreenConfigured = options.AlternateScreen;
        CursorHiddenConfigured = options.HideCursor;
        FocusReportingConfigured = options.Focus;
        BracketedPasteConfigured = options.Paste;
        MouseTracking = options.Tracking;
        MouseCoordinates = options.Coordinates;
        KittyKeyboardEnhancements = options.Keyboard;
        ModifyOtherKeysLevel = options.ModifyOtherKeys;
        ClipboardPasteEventsConfigured = options.ClipboardPasteEvents;
        var policy = options.Multiplexing ?? options.Negotiation?.Multiplexing;
        _clipboardRoute = policy is { Layers.Count: > 0 } ? new MultiplexerRoute(policy) : null;
        _clipboardCapabilityAuthorized = capabilities.KittyClipboard.Authoritative;
        var authorization = ClassifyAuthorization(this, capabilities);
        FocusReportingAuthorized = authorization.FocusReporting;
        BracketedPasteAuthorized = authorization.BracketedPaste;
        MouseAuthorized = authorization.Mouse;
        KittyKeyboardAuthorized = authorization.KittyKeyboard;
        ModifyOtherKeysAuthorized = authorization.ModifyOtherKeys;
    }

    private TerminalModeDiagnostics(TerminalModeDiagnostics source, TerminalCapabilities capabilities)
    {
        AlternateScreenConfigured = source.AlternateScreenConfigured;
        CursorHiddenConfigured = source.CursorHiddenConfigured;
        FocusReportingConfigured = source.FocusReportingConfigured;
        BracketedPasteConfigured = source.BracketedPasteConfigured;
        MouseTracking = source.MouseTracking;
        MouseCoordinates = source.MouseCoordinates;
        KittyKeyboardEnhancements = source.KittyKeyboardEnhancements;
        ModifyOtherKeysLevel = source.ModifyOtherKeysLevel;
        ClipboardPasteEventsConfigured = source.ClipboardPasteEventsConfigured;
        _clipboardRoute = source._clipboardRoute;
        _clipboardCapabilityAuthorized = capabilities.KittyClipboard.Authoritative;
        AlternateScreenActive = source.AlternateScreenActive;
        CursorHiddenActive = source.CursorHiddenActive;
        var authorization = ClassifyAuthorization(this, capabilities);
        FocusReportingAuthorized = authorization.FocusReporting;
        BracketedPasteAuthorized = authorization.BracketedPaste;
        MouseAuthorized = authorization.Mouse;
        KittyKeyboardAuthorized = authorization.KittyKeyboard;
        ModifyOtherKeysAuthorized = authorization.ModifyOtherKeys;
    }

    private TerminalModeDiagnostics(
        TerminalModeDiagnostics source,
        bool alternateScreenActive,
        bool cursorHiddenActive,
        bool focusReportingActive,
        bool bracketedPasteActive,
        bool mouseActive,
        bool kittyKeyboardActive,
        bool modifyOtherKeysActive,
        bool clipboardPasteEventsActive)
    {
        AlternateScreenConfigured = source.AlternateScreenConfigured;
        CursorHiddenConfigured = source.CursorHiddenConfigured;
        FocusReportingConfigured = source.FocusReportingConfigured;
        FocusReportingAuthorized = source.FocusReportingAuthorized;
        BracketedPasteConfigured = source.BracketedPasteConfigured;
        BracketedPasteAuthorized = source.BracketedPasteAuthorized;
        MouseTracking = source.MouseTracking;
        MouseCoordinates = source.MouseCoordinates;
        MouseAuthorized = source.MouseAuthorized;
        KittyKeyboardEnhancements = source.KittyKeyboardEnhancements;
        KittyKeyboardAuthorized = source.KittyKeyboardAuthorized;
        ModifyOtherKeysLevel = source.ModifyOtherKeysLevel;
        ModifyOtherKeysAuthorized = source.ModifyOtherKeysAuthorized;
        ClipboardPasteEventsConfigured = source.ClipboardPasteEventsConfigured;
        _clipboardRoute = source._clipboardRoute;
        _clipboardCapabilityAuthorized = source._clipboardCapabilityAuthorized;
        AlternateScreenActive = alternateScreenActive;
        CursorHiddenActive = cursorHiddenActive;
        FocusReportingActive = focusReportingActive;
        BracketedPasteActive = bracketedPasteActive;
        MouseActive = mouseActive;
        KittyKeyboardActive = kittyKeyboardActive;
        ModifyOtherKeysActive = modifyOtherKeysActive;
        ClipboardPasteEventsActive = clipboardPasteEventsActive;
    }

    /// <summary>Gets whether alternate-screen entry was requested.</summary>
    public bool AlternateScreenConfigured { get; }

    /// <summary>Gets whether alternate-screen entry bytes were written and flushed successfully.</summary>
    public bool AlternateScreenActive { get; }

    /// <summary>Gets whether cursor hiding was requested.</summary>
    public bool CursorHiddenConfigured { get; }

    /// <summary>Gets whether cursor-hiding bytes were written and flushed successfully.</summary>
    public bool CursorHiddenActive { get; }

    /// <summary>Gets whether focus reporting was requested.</summary>
    public bool FocusReportingConfigured { get; }

    /// <summary>Gets whether current capability evidence authorizes requested focus reporting.</summary>
    public bool FocusReportingAuthorized { get; }

    /// <summary>Gets whether focus-reporting enable bytes were written and flushed successfully.</summary>
    public bool FocusReportingActive { get; }

    /// <summary>Gets whether bracketed paste was requested.</summary>
    public bool BracketedPasteConfigured { get; }

    /// <summary>Gets whether current capability evidence authorizes requested bracketed paste.</summary>
    public bool BracketedPasteAuthorized { get; }

    /// <summary>Gets whether bracketed-paste enable bytes were written and flushed successfully.</summary>
    public bool BracketedPasteActive { get; }

    /// <summary>Gets the requested mouse tracking level, or null when disabled.</summary>
    public MouseTracking? MouseTracking { get; }

    /// <summary>Gets the requested mouse coordinate encoding.</summary>
    public MouseCoordinates MouseCoordinates { get; }

    /// <summary>Gets whether current capability evidence authorizes the requested mouse mode.</summary>
    public bool MouseAuthorized { get; }

    /// <summary>Gets whether mouse-mode enable bytes were written and flushed successfully.</summary>
    public bool MouseActive { get; }

    /// <summary>Gets the requested Kitty keyboard enhancements, or null when disabled.</summary>
    public KittyKeyboardEnhancement? KittyKeyboardEnhancements { get; }

    /// <summary>Gets whether current capability evidence authorizes the requested Kitty keyboard mode.</summary>
    public bool KittyKeyboardAuthorized { get; }

    /// <summary>Gets whether Kitty keyboard enable bytes were written and flushed successfully.</summary>
    public bool KittyKeyboardActive { get; }

    /// <summary>Gets the requested xterm modifyOtherKeys level, or null when disabled.</summary>
    public int? ModifyOtherKeysLevel { get; }

    /// <summary>Gets whether xterm modifyOtherKeys is the authorized keyboard fallback.</summary>
    public bool ModifyOtherKeysAuthorized { get; }

    /// <summary>Gets whether xterm modifyOtherKeys enable bytes were written and flushed successfully.</summary>
    public bool ModifyOtherKeysActive { get; }

    /// <summary>Gets whether Kitty clipboard paste notifications were requested.</summary>
    public bool ClipboardPasteEventsConfigured { get; }

    /// <summary>Gets whether current evidence and routing authorize clipboard paste notifications. This
    /// re-reads the retained multiplexer route's <see cref="MultiplexerRoute.CanRouteClipboard"/> on every
    /// access, because the underlying <see cref="MultiplexingPolicy.PaneVisible"/> is mutated live by the
    /// running application as outer-terminal focus reports arrive; caching this value at snapshot
    /// construction would keep reporting a stale answer for the rest of the session.</summary>
    public bool ClipboardPasteEventsAuthorized =>
        ClipboardPasteEventsConfigured &&
        _clipboardCapabilityAuthorized &&
        (_clipboardRoute is null || _clipboardRoute.CanRouteClipboard);

    /// <summary>Gets whether clipboard-paste-event enable bytes were written and flushed successfully.</summary>
    public bool ClipboardPasteEventsActive { get; }

    /// <summary>Recomputes authorization from refined capabilities without retaining the source options.</summary>
    /// <param name="capabilities">The non-null refined capability evidence.</param>
    /// <returns>A new snapshot that preserves successfully activated base modes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    internal TerminalModeDiagnostics WithCapabilities(TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return new TerminalModeDiagnostics(this, capabilities);
    }

    /// <summary>Creates a snapshot with successfully activated base modes.</summary>
    internal TerminalModeDiagnostics WithBaseActivation(bool alternateScreen, bool cursorHidden)
    {
        return AlternateScreenActive == alternateScreen && CursorHiddenActive == cursorHidden
            ? this
            : new TerminalModeDiagnostics(
                this,
                alternateScreen,
                cursorHidden,
                focusReportingActive: false,
                bracketedPasteActive: false,
                mouseActive: false,
                kittyKeyboardActive: false,
                modifyOtherKeysActive: false,
                clipboardPasteEventsActive: false);
    }

    /// <summary>Creates a snapshot with successfully activated optional modes.</summary>
    internal TerminalModeDiagnostics WithOptionalActivation(
        bool focusReporting,
        bool bracketedPaste,
        bool mouse,
        bool kittyKeyboard,
        bool modifyOtherKeys,
        bool clipboardPasteEvents)
    {
        return FocusReportingActive == focusReporting &&
               BracketedPasteActive == bracketedPaste &&
               MouseActive == mouse &&
               KittyKeyboardActive == kittyKeyboard &&
               ModifyOtherKeysActive == modifyOtherKeys &&
               ClipboardPasteEventsActive == clipboardPasteEvents
            ? this
            : new TerminalModeDiagnostics(
                this,
                AlternateScreenActive,
                CursorHiddenActive,
                focusReporting,
                bracketedPaste,
                mouse,
                kittyKeyboard,
                modifyOtherKeys,
                clipboardPasteEvents);
    }

    private static (
        bool FocusReporting,
        bool BracketedPaste,
        bool Mouse,
        bool KittyKeyboard,
        bool ModifyOtherKeys) ClassifyAuthorization(
        TerminalModeDiagnostics source,
        TerminalCapabilities capabilities)
    {
        var focus = source.FocusReportingConfigured && capabilities.FocusReporting.Authoritative;
        var paste = source.BracketedPasteConfigured && capabilities.BracketedPaste.Authoritative;
        var mouseFeature = source.MouseCoordinates == MouseCoordinates.Pixel
            ? capabilities.PixelMouse
            : capabilities.CellMouse;
        var mouse = source.MouseTracking.HasValue && mouseFeature.Authoritative;
        var kittyKeyboard = source.KittyKeyboardEnhancements.HasValue && capabilities.KittyKeyboard.Authoritative;
        var modifyOtherKeys = !kittyKeyboard &&
                              source.ModifyOtherKeysLevel.HasValue &&
                              capabilities.XtermKeyboard.Authoritative;
        return (focus, paste, mouse, kittyKeyboard, modifyOtherKeys);
    }
}
