// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Capabilities;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>Translates explicit nullable caller settings into final override-origin evidence.</summary>
internal static class OverrideEvidenceAdapter
{
    extension(TerminalCapabilities capabilities)
    {
        /// <summary>Applies optional explicit settings to one immutable capability snapshot.</summary>
        /// <param name="overrides">The optional explicit caller settings.</param>
        /// <returns>The original reference when <paramref name="overrides"/> is null; otherwise a settings-refined snapshot.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
        [Pure]
        [MustUseReturnValue]
        public TerminalCapabilities Apply(CapabilityOverrides? overrides)
        {
            ArgumentNullException.ThrowIfNull(capabilities);

            return overrides is null
                ? capabilities
                : capabilities with
                {
                    ColorDepth = overrides.ColorDepth ?? capabilities.ColorDepth,
                    ColorOrigin = overrides.ColorDepth.HasValue ? Origin.Override : capabilities.ColorOrigin,
                    AmbiguousWidth = overrides.AmbiguousWidth ?? capabilities.AmbiguousWidth,
                    SynchronizedOutput = Apply(capabilities.SynchronizedOutput, overrides.SynchronizedOutput),
                    FocusReporting = Apply(capabilities.FocusReporting, overrides.FocusReporting),
                    BracketedPaste = Apply(capabilities.BracketedPaste, overrides.BracketedPaste),
                    PixelMouse = Apply(capabilities.PixelMouse, overrides.PixelMouse),
                    CellMouse = Apply(capabilities.CellMouse, overrides.CellMouse),
                    KittyKeyboard = Apply(capabilities.KittyKeyboard, overrides.KittyKeyboard),
                    XtermKeyboard = Apply(capabilities.XtermKeyboard, overrides.XtermKeyboard),
                    Osc52 = Apply(capabilities.Osc52, overrides.Osc52),
                    KittyClipboard = Apply(capabilities.KittyClipboard, overrides.KittyClipboard),
                    KittyGraphics = Apply(capabilities.KittyGraphics, overrides.KittyGraphics),
                    Sixel = Apply(capabilities.Sixel, overrides.Sixel),
                    ItermImages = Apply(capabilities.ItermImages, overrides.ItermImages),
                    StyledUnderlines = Apply(capabilities.StyledUnderlines, overrides.StyledUnderlines),
                    UnderlineColor = Apply(capabilities.UnderlineColor, overrides.UnderlineColor),
                    Overline = Apply(capabilities.Overline, overrides.Overline),
                    Notifications = Apply(capabilities.Notifications, overrides.Notifications)
                };
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static Feature Apply(Feature current, bool? supported) =>
        supported.HasValue
            ? new Feature(
                supported.Value ? CapabilitySupport.Supported : CapabilitySupport.Unsupported,
                Origin.Override)
            : current;
}
