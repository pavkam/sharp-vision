// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using SharpVision.Terminal.Graphics;

/// <summary>Provides graphics placements that fell back to ordinary cells during one committed frame.</summary>
[PublicAPI]
public sealed class GraphicsDiagnosticEventArgs: EventArgs
{
    /// <summary>Initializes one graphics diagnostic event.</summary>
    /// <param name="placements">The non-null, non-empty skipped placements.</param>
    /// <exception cref="ArgumentNullException"><paramref name="placements"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="placements"/> is empty.</exception>
    public GraphicsDiagnosticEventArgs(IReadOnlyList<GraphicsPlacementDiagnostic> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        if (placements.Count == 0)
        {
            throw new ArgumentException(
                "A graphics diagnostic event requires at least one skipped placement.",
                nameof(placements));
        }

        Placements = Array.AsReadOnly(placements.ToArray());
    }

    /// <summary>Gets the immutable snapshot of graphics placements that fell back to ordinary cells.</summary>
    public IReadOnlyList<GraphicsPlacementDiagnostic> Placements { get; }
}
