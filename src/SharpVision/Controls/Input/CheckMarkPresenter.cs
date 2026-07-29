// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Formats one check mark into exact terminal cells for every mark family.</summary>
/// <remarks>
/// Shared so CheckBox and tree rows cannot disagree about mark geometry or degradation. It also
/// centralizes the per-state fallback: an unresolvable checked glyph must degrade to a checked
/// substitute, never to the unchecked one, which would silently misreport the value.
/// </remarks>
internal static class CheckMarkPresenter
{
    /// <summary>Formats the mark for one state, resolving each glyph against its own fallback.</summary>
    /// <param name="mark">The resolved mark layout and glyph family.</param>
    /// <param name="state">The checked, unchecked, or indeterminate state.</param>
    /// <param name="ambiguousWidth">The active ambiguous-width policy.</param>
    /// <returns>The exact cells to draw, three wide for brackets and one otherwise.</returns>
    internal static string Format(CheckMark mark, bool? state, Ambiguous ambiguousWidth)
    {
        var resolved = Resolve(mark, state, ambiguousWidth);

        return mark.MarkStyle == CheckBoxMarkStyle.Brackets
            ? $"[{resolved}]"
            : resolved.ToString();
    }

    private static Rune Resolve(CheckMark mark, bool? state, Ambiguous ambiguousWidth)
    {
        var selection = ControlGlyphs.Selection;
        var fallback = mark.MarkStyle switch
        {
            CheckBoxMarkStyle.Brackets => state switch
            {
                true => selection.CheckBoxBracketChecked.Fallback,
                false => selection.CheckBoxBracketUnchecked.Fallback,
                null => selection.CheckBoxBracketIndeterminate.Fallback
            },
            CheckBoxMarkStyle.Tick => state switch
            {
                true => selection.CheckBoxTickChecked.Fallback,
                false => selection.CheckBoxTickUnchecked.Fallback,
                null => selection.CheckBoxTickIndeterminate.Fallback
            },
            CheckBoxMarkStyle.Square => state switch
            {
                true => selection.CheckBoxSquareChecked.Fallback,
                false => selection.CheckBoxSquareUnchecked.Fallback,
                null => selection.CheckBoxSquareIndeterminate.Fallback
            },
            _ => throw new UnreachableException()
        };

        return CellGlyphResolver.Resolve(mark.GlyphFor(state), fallback, ambiguousWidth);
    }
}
