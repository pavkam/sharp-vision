// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Chooses whether a calendar commits one date or one inclusive date interval.</summary>
[PublicAPI]
public enum CalendarSelectionMode
{
    /// <summary>Commits exactly one date as a one-day interval.</summary>
    Select,

    /// <summary>Uses two activations to commit one inclusive ordered interval.</summary>
    Interval
}
