// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Identifies the semantic effect one keyboard stroke has on an oriented range value,
/// resolved from the physical key, the owner's <see cref="Orientation"/>, and which physical
/// direction counts as the increment.</summary>
[PublicAPI]
public enum RangeKeyCommand
{
    /// <summary>The stroke has no defined effect on the range.</summary>
    None,

    /// <summary>Step the value down by its small increment.</summary>
    SmallDecrement,

    /// <summary>Step the value up by its small increment.</summary>
    SmallIncrement,

    /// <summary>Step the value down by its large increment.</summary>
    LargeDecrement,

    /// <summary>Step the value up by its large increment.</summary>
    LargeIncrement,

    /// <summary>Jump the value to its minimum.</summary>
    Minimum,

    /// <summary>Jump the value to its maximum.</summary>
    Maximum
}
