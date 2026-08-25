// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies non-appearance root Theme values a control has resolved.</summary>
[Flags]
internal enum ThemeStructuralDependency
{
    /// <summary>No structural root value has participated.</summary>
    None = 0,

    /// <summary>The shared input affix gap affects layout.</summary>
    InputAffixGap = 1,

    /// <summary>The shared input disclosure glyph affects rendering.</summary>
    InputDropDownGlyph = 2,

    /// <summary>The shared popup anchor glyph family affects rendering.</summary>
    PopupAnchorGlyphs = 4
}
