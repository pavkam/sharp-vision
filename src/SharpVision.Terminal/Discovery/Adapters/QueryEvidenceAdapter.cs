// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Capabilities;

using Xterm;

/// <summary>Translates bounded query results into query-origin semantic evidence.</summary>
internal static class QueryEvidenceAdapter
{
    private static readonly Version _multipartMinimumVersion = new(3, 5);

    extension(TerminalCapabilities capabilities)
    {
        /// <summary>Applies optional bounded query results to one immutable capability snapshot.</summary>
        /// <param name="queries">The optional bounded query results.</param>
        /// <param name="environment">The non-null caller-supplied environment, used only to narrow iTerm2 evidence.</param>
        /// <returns>The original reference when <paramref name="queries"/> is null; otherwise a query-refined snapshot.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> or <paramref name="environment"/> is null.</exception>
        public TerminalCapabilities Apply(QueryResults? queries, IReadOnlyDictionary<string, string?> environment)
        {
            ArgumentNullException.ThrowIfNull(capabilities);
            ArgumentNullException.ThrowIfNull(environment);

            if (queries is null)
            {
                return capabilities;
            }

            var refined = RefineColor(capabilities with
            {
                SynchronizedOutput = Apply(capabilities.SynchronizedOutput, queries.SynchronizedOutput, Origin.Query),
                FocusReporting = Apply(capabilities.FocusReporting, queries.FocusReporting, Origin.Query),
                BracketedPaste = Apply(capabilities.BracketedPaste, queries.BracketedPaste, Origin.Query),
                PixelMouse = Apply(capabilities.PixelMouse, queries.PixelMouse, Origin.Query),
                CellMouse = Apply(capabilities.CellMouse, queries.CellMouse, Origin.Query),
                KittyKeyboard = Apply(capabilities.KittyKeyboard, queries.KittyKeyboard, Origin.Query),
                XtermKeyboard = Apply(capabilities.XtermKeyboard, queries.XtermKeyboard, Origin.Query),
                Osc52 = Apply(capabilities.Osc52, queries.Osc52, Origin.Query),
                KittyClipboard = Apply(capabilities.KittyClipboard, queries.KittyClipboard, Origin.Query),
                KittyGraphics = Apply(capabilities.KittyGraphics, queries.KittyGraphics, Origin.Query),
                Sixel = Apply(capabilities.Sixel, queries.Sixel, Origin.Query),
                ItermImages = Apply(capabilities.ItermImages, queries.ItermImages, Origin.Query),
                StyledUnderlines = Apply(capabilities.StyledUnderlines, queries.StyledUnderlines, Origin.Query),
                UnderlineColor = Apply(capabilities.UnderlineColor, queries.UnderlineColor, Origin.Query),
                Overline = Apply(capabilities.Overline, queries.Overline, Origin.Query)
            }, queries.CapabilityString);

            // Narrowing only, per docs/protocols/iterm2.md: multipart file transfer (the only
            // iTerm2 image form this library emits) arrived in iTerm2 3.5, but the OSC 1337
            // Capabilities FILE flag predates it and shares its wire code with FOCUS_REPORTING,
            // so a bare FILE=true reply cannot alone prove multipart support. A parsed
            // TERM_PROGRAM_VERSION below 3.5 withholds Supported evidence gathered above by
            // downgrading it to Unsupported; an absent, unparseable, or >=3.5 version leaves it
            // untouched. This can never promote evidence to Supported by itself — only Query (or
            // a later explicit Override) evidence can do that, and Override always runs after
            // this phase and is never touched here.
            return refined with { ItermImages = NarrowItermImagesByVersion(refined.ItermImages, environment) };
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static TerminalCapabilities RefineColor(
        TerminalCapabilities value,
        CapabilityResponse? response)
    {
        if (value.ColorOrigin is not (Origin.Default or Origin.Environment) ||
            response is not { Valid: true } ||
            !response.Items.TryGetValue(CapabilityName.DirectColor, out var directColor))
        {
            return value;
        }

        var bytes = directColor.Span;
        var supported = bytes.SequenceEqual("24"u8) ||
                        bytes.SequenceEqual("true"u8) ||
                        bytes.SequenceEqual("yes"u8) ||
                        bytes.SequenceEqual("8"u8) ||
                        bytes.SequenceEqual("8/8/8"u8);
        return supported
            ? value with { ColorDepth = ColorDepth.TrueColor, ColorOrigin = Origin.Query }
            : value;
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static Feature NarrowItermImagesByVersion(
        Feature current,
        IReadOnlyDictionary<string, string?> environment) =>
        current.State == CapabilitySupport.Supported &&
        environment.TryGetValue(EvidenceEnvironmentVars.TermProgramVersion, out var raw) &&
        Version.TryParse(raw, out var version) &&
        version < _multipartMinimumVersion
            ? new Feature(CapabilitySupport.Unsupported, current.Origin)
            : current;

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static Feature Apply(Feature current, bool? supported, Origin origin) =>
        supported.HasValue && (origin == Origin.Override || current.Origin != Origin.Override)
            ? new Feature(
                supported.Value ? CapabilitySupport.Supported : CapabilitySupport.Unsupported,
                origin)
            : current;
}
