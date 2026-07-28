// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Selects the built-in visual mark family used by a CheckBox.</summary>
[PublicAPI]
public enum CheckBoxMarkStyle
{
    /// <summary>Uses the configurable one-cell <see cref="CheckBoxGlyphs"/> value.</summary>
    Square,

    /// <summary>Uses fixed-width ASCII-compatible bracket marks such as [x].</summary>
    Brackets,

    /// <summary>Uses Unicode circle and checkmark marks.</summary>
    Tick
}
