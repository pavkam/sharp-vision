// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines checkbox, radio, and menu selection marks.</summary>
public readonly record struct SelectionGlyphs
{
    /// <summary>Initializes every selection-mark state used by built-in controls.</summary>
    /// <param name="checkBoxBracketUnchecked">The unchecked bracket mark.</param>
    /// <param name="checkBoxBracketChecked">The checked bracket mark.</param>
    /// <param name="checkBoxBracketIndeterminate">The indeterminate bracket mark.</param>
    /// <param name="checkBoxTickUnchecked">The unchecked tick-style mark.</param>
    /// <param name="checkBoxTickChecked">The checked tick-style mark.</param>
    /// <param name="checkBoxTickIndeterminate">The indeterminate tick-style mark.</param>
    /// <param name="checkBoxSquareUnchecked">The unchecked square-style mark.</param>
    /// <param name="checkBoxSquareChecked">The checked square-style mark.</param>
    /// <param name="checkBoxSquareIndeterminate">The indeterminate square-style mark.</param>
    /// <param name="radioUnchecked">The unchecked radio mark.</param>
    /// <param name="radioChecked">The checked radio mark.</param>
    /// <param name="menuCheckUnchecked">The unchecked menu-check mark.</param>
    /// <param name="menuCheckChecked">The checked menu-check mark.</param>
    /// <param name="menuRadioUnchecked">The unchecked menu-radio mark.</param>
    /// <param name="menuRadioChecked">The checked menu-radio mark.</param>
    public SelectionGlyphs(
        ThemedGlyph checkBoxBracketUnchecked,
        ThemedGlyph checkBoxBracketChecked,
        ThemedGlyph checkBoxBracketIndeterminate,
        ThemedGlyph checkBoxTickUnchecked,
        ThemedGlyph checkBoxTickChecked,
        ThemedGlyph checkBoxTickIndeterminate,
        ThemedGlyph checkBoxSquareUnchecked,
        ThemedGlyph checkBoxSquareChecked,
        ThemedGlyph checkBoxSquareIndeterminate,
        ThemedGlyph radioUnchecked,
        ThemedGlyph radioChecked,
        ThemedGlyph menuCheckUnchecked,
        ThemedGlyph menuCheckChecked,
        ThemedGlyph menuRadioUnchecked,
        ThemedGlyph menuRadioChecked)
    {
        CheckBoxBracketUnchecked = checkBoxBracketUnchecked;
        CheckBoxBracketChecked = checkBoxBracketChecked;
        CheckBoxBracketIndeterminate = checkBoxBracketIndeterminate;
        CheckBoxTickUnchecked = checkBoxTickUnchecked;
        CheckBoxTickChecked = checkBoxTickChecked;
        CheckBoxTickIndeterminate = checkBoxTickIndeterminate;
        CheckBoxSquareUnchecked = checkBoxSquareUnchecked;
        CheckBoxSquareChecked = checkBoxSquareChecked;
        CheckBoxSquareIndeterminate = checkBoxSquareIndeterminate;
        RadioUnchecked = radioUnchecked;
        RadioChecked = radioChecked;
        MenuCheckUnchecked = menuCheckUnchecked;
        MenuCheckChecked = menuCheckChecked;
        MenuRadioUnchecked = menuRadioUnchecked;
        MenuRadioChecked = menuRadioChecked;
    }

    /// <summary>Gets the unchecked bracket mark.</summary>
    public ThemedGlyph CheckBoxBracketUnchecked { get; }
    /// <summary>Gets the checked bracket mark.</summary>
    public ThemedGlyph CheckBoxBracketChecked { get; }
    /// <summary>Gets the indeterminate bracket mark.</summary>
    public ThemedGlyph CheckBoxBracketIndeterminate { get; }
    /// <summary>Gets the unchecked tick-style mark.</summary>
    public ThemedGlyph CheckBoxTickUnchecked { get; }
    /// <summary>Gets the checked tick-style mark.</summary>
    public ThemedGlyph CheckBoxTickChecked { get; }
    /// <summary>Gets the indeterminate tick-style mark.</summary>
    public ThemedGlyph CheckBoxTickIndeterminate { get; }
    /// <summary>Gets the unchecked square-style mark.</summary>
    public ThemedGlyph CheckBoxSquareUnchecked { get; }
    /// <summary>Gets the checked square-style mark.</summary>
    public ThemedGlyph CheckBoxSquareChecked { get; }
    /// <summary>Gets the indeterminate square-style mark.</summary>
    public ThemedGlyph CheckBoxSquareIndeterminate { get; }
    /// <summary>Gets the unchecked radio mark.</summary>
    public ThemedGlyph RadioUnchecked { get; }
    /// <summary>Gets the checked radio mark.</summary>
    public ThemedGlyph RadioChecked { get; }
    /// <summary>Gets the unchecked menu-check mark.</summary>
    public ThemedGlyph MenuCheckUnchecked { get; }
    /// <summary>Gets the checked menu-check mark.</summary>
    public ThemedGlyph MenuCheckChecked { get; }
    /// <summary>Gets the unchecked menu-radio mark.</summary>
    public ThemedGlyph MenuRadioUnchecked { get; }
    /// <summary>Gets the checked menu-radio mark.</summary>
    public ThemedGlyph MenuRadioChecked { get; }
}
