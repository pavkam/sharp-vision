// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Identifies which horizontal caption edge owns a CheckBox or RadioButton selection mark.</summary>
[PublicAPI]
public enum SelectionMarkPlacement
{
    /// <summary>Places the mark before the caption in terminal reading order.</summary>
    Leading,

    /// <summary>Places the mark after the caption in terminal reading order.</summary>
    Trailing
}
