// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Capabilities;

/// <summary>Translates bounded query results into query-origin semantic evidence.</summary>
internal static class QueryEvidenceAdapter
{
    /// <summary>Applies optional bounded query results to one immutable capability snapshot.</summary>
    /// <param name="capabilities">The non-null semantic capabilities to refine.</param>
    /// <param name="queries">The optional bounded query results.</param>
    /// <returns>The original reference when <paramref name="queries"/> is null; otherwise a query-refined snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    public static TerminalCapabilities Apply(TerminalCapabilities capabilities, Queries? queries)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return queries is null
            ? capabilities
            : RefineColor(capabilities with
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
    }

    private static TerminalCapabilities RefineColor(
        TerminalCapabilities value,
        CapabilityResponse? response)
    {
        if (value.ColorOrigin is not (Origin.Default or Origin.Environment) ||
            response is not { IsValid: true } ||
            !response.Items.TryGetValue(CapabilityName.DirectColor, out var directColor))
        {
            return value;
        }

        var bytes = directColor.Span;
        var supported = bytes.SequenceEqual("24"u8) ||
                        bytes.SequenceEqual("true"u8) ||
                        bytes.SequenceEqual("yes"u8) ||
                        bytes.SequenceEqual("8/8/8"u8);
        return supported
            ? value with { ColorDepth = ColorDepth.TrueColor, ColorOrigin = Origin.Query }
            : value;
    }

    private static Feature Apply(Feature current, bool? supported, Origin origin) =>
        supported.HasValue && (origin == Origin.Override || current.Origin != Origin.Override)
            ? new Feature(
                supported.Value ? Support.Supported : Support.Unsupported,
                origin)
            : current;
}
