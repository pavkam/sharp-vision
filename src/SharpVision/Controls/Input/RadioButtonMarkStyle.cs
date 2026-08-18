// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Selects the built-in visual mark family used by a <see cref="RadioButton"/>.</summary>
[PublicAPI]
public enum RadioButtonMarkStyle
{
    /// <summary>Uses the configurable one-cell circle marks.</summary>
    Circle,

    /// <summary>Uses fixed-width parenthesized marks such as ( ) and (●).</summary>
    Parentheses
}
