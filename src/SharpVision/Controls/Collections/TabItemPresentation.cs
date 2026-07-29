// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>
/// Captures the authored Visibility, Width, and Height a caller set on a
/// <see cref="TabItem"/> before <see cref="TabControl"/> overwrote them with its
/// own private presentation policy, so they can be restored unchanged when the
/// item leaves the control.
/// </summary>
internal readonly record struct TabItemPresentation
{
    /// <summary>Initializes an immutable snapshot of one item's authored presentation.</summary>
    /// <param name="visibility">The caller-authored visibility at attach time.</param>
    /// <param name="width">The caller-authored width at attach time.</param>
    /// <param name="height">The caller-authored height at attach time.</param>
    public TabItemPresentation(Visibility visibility, Length width, Length height)
    {
        Visibility = visibility;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the authored visibility.</summary>
    public Visibility Visibility { get; }

    /// <summary>Gets the authored width.</summary>
    public Length Width { get; }

    /// <summary>Gets the authored height.</summary>
    public Length Height { get; }
}
