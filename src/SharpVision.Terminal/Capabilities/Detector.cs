// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Combines conservative defaults, environment hints, queries, and overrides.
/// </summary>
public static class Detector
{
    /// <summary>
    /// Produces a new immutable profile without reading process-global state.
    /// </summary>
    /// <param name="environment">Caller-supplied environment values.</param>
    /// <param name="queries">Optional bounded query results.</param>
    /// <param name="overrides">Optional explicit caller overrides.</param>
    /// <returns>A newly published immutable capability profile.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="environment"/> is <see langword="null"/>.
    /// </exception>
    public static TerminalCapabilities Detect(
        IReadOnlyDictionary<string, string?> environment,
        Queries? queries = null,
        Settings? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var capabilities = TerminalCapabilities.Conservative;
        _ = environment.TryGetValue("TERM", out var term);
        _ = environment.TryGetValue("COLORTERM", out var colorTerm);
        _ = environment.TryGetValue("TERM_PROGRAM", out var program);
        var kitty = Contains(term, "kitty");
        var xterm = Contains(term, "xterm");

        if (Contains(colorTerm, "truecolor") || Contains(colorTerm, "24bit") || kitty)
        {
            capabilities = capabilities with
            {
                ColorDepth = ColorDepth.TrueColor,
                ColorOrigin = Origin.Environment,
            };
        }
        else if (Contains(term, "256color"))
        {
            capabilities = capabilities with
            {
                ColorDepth = ColorDepth.Indexed256,
                ColorOrigin = Origin.Environment,
            };
        }

        if (kitty)
        {
            var hint = new Feature(Support.Tentative, Origin.Environment);
            capabilities = capabilities with
            {
                SynchronizedOutput = hint,
                FocusReporting = hint,
                BracketedPaste = hint,
                PixelMouse = hint,
                CellMouse = hint,
                KittyKeyboard = hint,
                Osc52 = hint,
                KittyClipboard = hint,
                KittyGraphics = hint,
                StyledUnderlines = hint,
                UnderlineColor = hint,
                Overline = hint,
            };
        }
        else if (xterm)
        {
            var hint = new Feature(Support.Tentative, Origin.Environment);
            capabilities = capabilities with
            {
                FocusReporting = hint,
                BracketedPaste = hint,
                CellMouse = hint,
                Osc52 = hint,
                StyledUnderlines = hint,
                UnderlineColor = hint,
                Overline = hint,
            };
        }

        if (string.Equals(program, "iTerm.app", StringComparison.OrdinalIgnoreCase))
        {
            capabilities = capabilities with
            {
                ItermImages = new Feature(Support.Tentative, Origin.Environment),
            };
        }

        var multiplexer = environment.ContainsKey("TMUX") || Contains(term, "screen");
        var remote = environment.ContainsKey("SSH_CONNECTION") ||
            environment.ContainsKey("SSH_TTY");

        if (multiplexer)
        {
            var unavailable = new Feature(Support.Unsupported, Origin.Environment);
            capabilities = capabilities with
            {
                KittyClipboard = unavailable,
                KittyGraphics = unavailable,
                ItermImages = unavailable,
            };
        }

        if (remote)
        {
            capabilities = capabilities with
            {
                Osc52 = Feature.Unknown,
                KittyClipboard = new Feature(Support.Unsupported, Origin.Environment),
            };
        }

        capabilities = ApplyQueries(capabilities, queries);
        capabilities = ApplyOverrides(capabilities, overrides);

        return capabilities;
    }

    private static TerminalCapabilities ApplyOverrides(TerminalCapabilities value, Settings? overrides) =>
        overrides is null
            ? value
            : value with
            {
                ColorDepth = overrides.ColorDepth ?? value.ColorDepth,
                ColorOrigin = overrides.ColorDepth.HasValue ? Origin.Override : value.ColorOrigin,
                AmbiguousWidth = overrides.AmbiguousWidth ?? value.AmbiguousWidth,
                SynchronizedOutput = Apply(value.SynchronizedOutput, overrides.SynchronizedOutput, Origin.Override),
                FocusReporting = Apply(value.FocusReporting, overrides.FocusReporting, Origin.Override),
                BracketedPaste = Apply(value.BracketedPaste, overrides.BracketedPaste, Origin.Override),
                PixelMouse = Apply(value.PixelMouse, overrides.PixelMouse, Origin.Override),
                CellMouse = Apply(value.CellMouse, overrides.CellMouse, Origin.Override),
                KittyKeyboard = Apply(value.KittyKeyboard, overrides.KittyKeyboard, Origin.Override),
                Osc52 = Apply(value.Osc52, overrides.Osc52, Origin.Override),
                KittyClipboard = Apply(value.KittyClipboard, overrides.KittyClipboard, Origin.Override),
                KittyGraphics = Apply(value.KittyGraphics, overrides.KittyGraphics, Origin.Override),
                Sixel = Apply(value.Sixel, overrides.Sixel, Origin.Override),
                ItermImages = Apply(value.ItermImages, overrides.ItermImages, Origin.Override),
                StyledUnderlines = Apply(value.StyledUnderlines, overrides.StyledUnderlines, Origin.Override),
                UnderlineColor = Apply(value.UnderlineColor, overrides.UnderlineColor, Origin.Override),
                Overline = Apply(value.Overline, overrides.Overline, Origin.Override),
            };

    private static TerminalCapabilities ApplyQueries(TerminalCapabilities value, Queries? queries) =>
        queries is null
            ? value
            : value with
            {
                SynchronizedOutput = Apply(value.SynchronizedOutput, queries.SynchronizedOutput, Origin.Query),
                FocusReporting = Apply(value.FocusReporting, queries.FocusReporting, Origin.Query),
                BracketedPaste = Apply(value.BracketedPaste, queries.BracketedPaste, Origin.Query),
                PixelMouse = Apply(value.PixelMouse, queries.PixelMouse, Origin.Query),
                CellMouse = Apply(value.CellMouse, queries.CellMouse, Origin.Query),
                KittyKeyboard = Apply(value.KittyKeyboard, queries.KittyKeyboard, Origin.Query),
                Osc52 = Apply(value.Osc52, queries.Osc52, Origin.Query),
                KittyClipboard = Apply(value.KittyClipboard, queries.KittyClipboard, Origin.Query),
                KittyGraphics = Apply(value.KittyGraphics, queries.KittyGraphics, Origin.Query),
                Sixel = Apply(value.Sixel, queries.Sixel, Origin.Query),
                ItermImages = Apply(value.ItermImages, queries.ItermImages, Origin.Query),
                StyledUnderlines = Apply(value.StyledUnderlines, queries.StyledUnderlines, Origin.Query),
                UnderlineColor = Apply(value.UnderlineColor, queries.UnderlineColor, Origin.Query),
                Overline = Apply(value.Overline, queries.Overline, Origin.Query),
            };

    private static Feature Apply(Feature current, bool? supported, Origin origin) =>
        supported.HasValue
            ? new Feature(
                supported.Value ? Support.Supported : Support.Unsupported,
                origin)
            : current;

    private static bool Contains(string? value, string fragment) =>
        value?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;
}
