// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Chooses whether a <see cref="NumberInput"/> edits whole integers or decimal values.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Integer and Decimal are the established domain terms for this control's two editing modes.")]
[PublicAPI]
public enum NumberInputMode
{
    /// <summary>Restricts editing to whole numbers: the decimal-separator keystroke is rejected and
    /// the committed value is always rounded to zero decimal places.</summary>
    Integer,

    /// <summary>Allows a fractional value up to the configured number of decimal places.</summary>
    Decimal
}
