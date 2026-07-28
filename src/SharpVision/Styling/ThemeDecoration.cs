// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Identifies one library-defined global terminal-attribute role.</summary>
public enum ThemeDecoration
{
    /// <summary>Attributes for ordinary text.</summary>
    NormalText,
    /// <summary>Attributes for active text.</summary>
    ActiveText,
    /// <summary>Attributes for focused text.</summary>
    FocusedText,
    /// <summary>Attributes for pressed text.</summary>
    PressedText,
    /// <summary>Attributes for selected text.</summary>
    SelectedText,
    /// <summary>Attributes for disabled text.</summary>
    DisabledText,
    /// <summary>Attributes for border cells.</summary>
    Border,
    /// <summary>Attributes for shadow cells.</summary>
    Shadow,
    /// <summary>Attributes for access-key text.</summary>
    Hotkey
}
