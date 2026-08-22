// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Identifies a <see cref="DocumentList"/>'s marker style.</summary>
[PublicAPI]
public enum DocumentListKind
{
    /// <summary>Marks each item with a bullet glyph that rotates by nesting depth.</summary>
    Bulleted,

    /// <summary>Marks each item with its one-based position formatted as "N.".</summary>
    Numbered
}
