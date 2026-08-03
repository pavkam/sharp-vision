// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Capabilities;

using MultiplexingKind = Multiplexing.MultiplexerKind;
using MultiplexingPolicy = Multiplexing.Policy;

/// <summary>Translates caller-supplied environment hints into conservative semantic evidence.</summary>
internal static class EnvironmentEvidenceAdapter
{
    extension(TerminalCapabilities capabilities)
    {
        /// <summary>Applies environment hints and safety narrowing to one immutable snapshot.</summary>
        /// <param name="environment">The non-null caller-supplied environment snapshot.</param>
        /// <returns>The original or refined immutable capability snapshot.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> or <paramref name="environment"/> is null.</exception>
        public TerminalCapabilities Apply(IReadOnlyDictionary<string, string?> environment)
        {
            ArgumentNullException.ThrowIfNull(capabilities);
            ArgumentNullException.ThrowIfNull(environment);

            _ = environment.TryGetValue(EnvironmentNames.Term, out var term);
            _ = environment.TryGetValue(EnvironmentNames.ColorTerm, out var colorTerm);
            _ = environment.TryGetValue(EnvironmentNames.TermProgram, out var program);
            var kitty = Contains(term, "kitty");
            var xterm = Contains(term, "xterm");

            if (capabilities.ColorOrigin == Origin.Default &&
                (Contains(colorTerm, "truecolor") || Contains(colorTerm, "24bit") || kitty))
            {
                capabilities = capabilities with { ColorDepth = ColorDepth.TrueColor, ColorOrigin = Origin.Environment };
            }
            else if (capabilities.ColorOrigin == Origin.Default && Contains(term, "256color"))
            {
                capabilities = capabilities with { ColorDepth = ColorDepth.Indexed256, ColorOrigin = Origin.Environment };
            }

            if (kitty)
            {
                var hint = new Feature(CapabilitySupport.Tentative, Origin.Environment);
                capabilities = capabilities with
                {
                    SynchronizedOutput = ApplyHint(capabilities.SynchronizedOutput, hint),
                    FocusReporting = ApplyHint(capabilities.FocusReporting, hint),
                    BracketedPaste = ApplyHint(capabilities.BracketedPaste, hint),
                    PixelMouse = ApplyHint(capabilities.PixelMouse, hint),
                    CellMouse = ApplyHint(capabilities.CellMouse, hint),
                    KittyKeyboard = ApplyHint(capabilities.KittyKeyboard, hint),
                    Osc52 = ApplyHint(capabilities.Osc52, hint),
                    KittyClipboard = ApplyHint(capabilities.KittyClipboard, hint),
                    KittyGraphics = ApplyHint(capabilities.KittyGraphics, hint),
                    StyledUnderlines = ApplyHint(capabilities.StyledUnderlines, hint),
                    UnderlineColor = ApplyHint(capabilities.UnderlineColor, hint),
                    Overline = ApplyHint(capabilities.Overline, hint)
                };
            }
            else if (xterm)
            {
                var hint = new Feature(CapabilitySupport.Tentative, Origin.Environment);
                capabilities = capabilities with
                {
                    FocusReporting = ApplyHint(capabilities.FocusReporting, hint),
                    BracketedPaste = ApplyHint(capabilities.BracketedPaste, hint),
                    CellMouse = ApplyHint(capabilities.CellMouse, hint),
                    XtermKeyboard = ApplyHint(capabilities.XtermKeyboard, hint),
                    Osc52 = ApplyHint(capabilities.Osc52, hint),
                    StyledUnderlines = ApplyHint(capabilities.StyledUnderlines, hint),
                    UnderlineColor = ApplyHint(capabilities.UnderlineColor, hint),
                    Overline = ApplyHint(capabilities.Overline, hint)
                };
            }

            if (string.Equals(program, "iTerm.app", StringComparison.OrdinalIgnoreCase))
            {
                var hint = new Feature(CapabilitySupport.Tentative, Origin.Environment);
                capabilities = capabilities with { ItermImages = ApplyHint(capabilities.ItermImages, hint) };
            }

            var multiplexer = MultiplexingPolicy.Detect(environment).Kind != MultiplexingKind.None;
            var remote = environment.ContainsKey(EnvironmentNames.SshConnection) ||
                         environment.ContainsKey(EnvironmentNames.SshTty);

            if (multiplexer)
            {
                var unavailable = new Feature(CapabilitySupport.Unsupported, Origin.Environment);
                capabilities = capabilities with
                {
                    KittyClipboard = unavailable,
                    KittyGraphics = unavailable,
                    ItermImages = unavailable
                };
            }

            if (remote)
            {
                capabilities = capabilities with
                {
                    // OSC 52's primary use case is copying to the *local* clipboard from a *remote*
                    // SSH session, so a blanket environment-based guess must not clobber evidence
                    // that already outranks it (terminfo, an active query, or an explicit override) —
                    // only narrow it when nothing authoritative established support already (see #124).
                    Osc52 = capabilities.Osc52.IsAuthoritative ? capabilities.Osc52 : Feature.Unknown,
                    KittyClipboard = new Feature(CapabilitySupport.Unsupported, Origin.Environment)
                };
            }

            return capabilities;
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static Feature ApplyHint(Feature current, Feature hint) =>
        current.State == CapabilitySupport.Unknown ? hint : current;

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static bool Contains(string? value, string fragment) =>
        value?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;
}
