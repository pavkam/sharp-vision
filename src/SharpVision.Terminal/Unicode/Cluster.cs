// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Unicode;

/// <summary>Classifies one borrowed cluster's width and safe presentation.</summary>
internal readonly record struct Cluster
{
    /// <summary>Initializes validated non-owning cluster classification.</summary>
    /// <param name="width">The contextual, narrow, or wide cell width.</param>
    /// <param name="requiresReplacement">Whether terminal output must use U+FFFD.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> is unknown.</exception>
    /// <exception cref="ArgumentException">A contextual control requests replacement.</exception>
    public Cluster(CellWidth width, bool requiresReplacement)
    {
        if (!Enum.IsDefined(width))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The cluster width is unknown.");
        }

        if (width == CellWidth.Control && requiresReplacement)
        {
            throw new ArgumentException(
                "A contextual control cannot own a replacement cell.",
                nameof(requiresReplacement));
        }

        Width = width;
        RequiresReplacement = requiresReplacement;
    }

    /// <summary>Gets the contextual, narrow, or wide cell width.</summary>
    public CellWidth Width { get; }

    /// <summary>Gets whether terminal output must use U+FFFD.</summary>
    public bool RequiresReplacement { get; }
}
