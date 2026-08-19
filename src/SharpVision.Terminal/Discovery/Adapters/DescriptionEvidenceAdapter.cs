// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Capabilities;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>
/// Validates database-origin semantic claims against their exact retained
/// terminal-description programs.
/// </summary>
internal static class DescriptionEvidenceAdapter
{
    extension(TerminalCapabilities capabilities)
    {
        /// <summary>
        /// Applies finite semantic support only when a validated database
        /// description retains each feature's exact non-empty backing program.
        /// </summary>
        /// <param name="description">The non-null validated description metadata.</param>
        /// <param name="programs">The non-null owned compiled programs.</param>
        /// <returns>The original or refined immutable capabilities.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="capabilities"/>, <paramref name="description"/>, or
        /// <paramref name="programs"/> is null.
        /// </exception>
        [Pure]
        [MustUseReturnValue]
        public TerminalCapabilities Apply(Description description, Programs programs)
        {
            ArgumentNullException.ThrowIfNull(capabilities);
            ArgumentNullException.ThrowIfNull(description);
            ArgumentNullException.ThrowIfNull(programs);

            var acceptsDatabaseEvidence =
                description.Origin == DescriptionOrigin.Database &&
                description.Suitability == Suitability.Usable;

            var synchronizedOutput = ApplyDatabaseEvidence(
                capabilities.SynchronizedOutput,
                hasBackingProgram: false);
            var focusReporting = ApplyDatabaseEvidence(
                capabilities.FocusReporting,
                acceptsDatabaseEvidence &&
                programs.Has("fe") &&
                programs.Has("fd") &&
                programs.Has("kxIN") &&
                programs.Has("kxOUT"));
            var bracketedPaste = ApplyDatabaseEvidence(
                capabilities.BracketedPaste,
                acceptsDatabaseEvidence &&
                programs.Has("BE") &&
                programs.Has("BD") &&
                programs.Has("PS") &&
                programs.Has("PE"));
            var pixelMouse = ApplyDatabaseEvidence(
                capabilities.PixelMouse,
                hasBackingProgram: false);
            var cellMouse = ApplyDatabaseEvidence(
                capabilities.CellMouse,
                acceptsDatabaseEvidence &&
                programs.Has("XM") &&
                (programs.Has("kmous") || programs.Has("xm")));
            var kittyKeyboard = ApplyDatabaseEvidence(
                capabilities.KittyKeyboard,
                hasBackingProgram: false);
            var xtermKeyboard = ApplyDatabaseEvidence(
                capabilities.XtermKeyboard,
                hasBackingProgram: false);
            var osc52 = ApplyDatabaseEvidence(
                capabilities.Osc52,
                acceptsDatabaseEvidence && programs.Has("Ms"));
            var kittyClipboard = ApplyDatabaseEvidence(
                capabilities.KittyClipboard,
                hasBackingProgram: false);
            var kittyGraphics = ApplyDatabaseEvidence(
                capabilities.KittyGraphics,
                hasBackingProgram: false);
            var sixel = ApplyDatabaseEvidence(
                capabilities.Sixel,
                hasBackingProgram: false);
            var itermImages = ApplyDatabaseEvidence(
                capabilities.ItermImages,
                hasBackingProgram: false);
            var styledUnderlines = ApplyDatabaseEvidence(
                capabilities.StyledUnderlines,
                acceptsDatabaseEvidence && programs.Has("Smulx"));
            var underlineColor = ApplyDatabaseEvidence(
                capabilities.UnderlineColor,
                acceptsDatabaseEvidence && programs.Has("Setulc"));
            var overline = ApplyDatabaseEvidence(
                capabilities.Overline,
                acceptsDatabaseEvidence && programs.Has("Smol") && programs.Has("Rmol"));

            return synchronizedOutput == capabilities.SynchronizedOutput &&
                   focusReporting == capabilities.FocusReporting &&
                   bracketedPaste == capabilities.BracketedPaste &&
                   pixelMouse == capabilities.PixelMouse &&
                   cellMouse == capabilities.CellMouse &&
                   kittyKeyboard == capabilities.KittyKeyboard &&
                   xtermKeyboard == capabilities.XtermKeyboard &&
                   osc52 == capabilities.Osc52 &&
                   kittyClipboard == capabilities.KittyClipboard &&
                   kittyGraphics == capabilities.KittyGraphics &&
                   sixel == capabilities.Sixel &&
                   itermImages == capabilities.ItermImages &&
                   styledUnderlines == capabilities.StyledUnderlines &&
                   underlineColor == capabilities.UnderlineColor &&
                   overline == capabilities.Overline
                ? capabilities
                : capabilities with
                {
                    SynchronizedOutput = synchronizedOutput,
                    FocusReporting = focusReporting,
                    BracketedPaste = bracketedPaste,
                    PixelMouse = pixelMouse,
                    CellMouse = cellMouse,
                    KittyKeyboard = kittyKeyboard,
                    XtermKeyboard = xtermKeyboard,
                    Osc52 = osc52,
                    KittyClipboard = kittyClipboard,
                    KittyGraphics = kittyGraphics,
                    Sixel = sixel,
                    ItermImages = itermImages,
                    StyledUnderlines = styledUnderlines,
                    UnderlineColor = underlineColor,
                    Overline = overline
                };
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static Feature ApplyDatabaseEvidence(
        Feature current,
        bool hasBackingProgram)
    {
        return current.State == CapabilitySupport.Supported && current.Origin == Origin.Database
            ? hasBackingProgram ? current : Feature.Unknown
            : hasBackingProgram && current == Feature.Unknown
                ? new Feature(CapabilitySupport.Supported, Origin.Database)
                : current;
    }
}
