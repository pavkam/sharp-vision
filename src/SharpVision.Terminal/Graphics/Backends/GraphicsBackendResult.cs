// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics.Backends;

/// <summary>Reports a fully prepared bounded graphics transaction to the renderer.</summary>
internal readonly struct GraphicsBackendResult
{
    /// <summary>Initializes validated prepared operation counts.</summary>
    /// <param name="changed">Whether the prepared transaction changes remote graphics state.</param>
    /// <param name="uploads">The prepared upload count.</param>
    /// <param name="placements">The prepared replacement-placement count.</param>
    /// <param name="removals">The prepared stale-removal count.</param>
    /// <param name="fullCellRedraw">Whether cells must be reconstructed before graphics output.</param>
    /// <param name="skippedPlacements">Placements that fell back to ordinary cells this prepare, if any.</param>
    /// <exception cref="ArgumentOutOfRangeException">An operation count is negative.</exception>
    public GraphicsBackendResult(
        bool changed,
        int uploads,
        int placements,
        int removals,
        bool fullCellRedraw = false,
        IReadOnlyList<GraphicsPlacementDiagnostic>? skippedPlacements = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(uploads);
        ArgumentOutOfRangeException.ThrowIfNegative(placements);
        ArgumentOutOfRangeException.ThrowIfNegative(removals);
        Changed = changed;
        Uploads = uploads;
        Placements = placements;
        Removals = removals;
        FullCellRedraw = fullCellRedraw;
        SkippedPlacements = skippedPlacements ?? Array.Empty<GraphicsPlacementDiagnostic>();
    }

    /// <summary>Gets whether remote graphics output is required.</summary>
    public bool Changed { get; }

    /// <summary>Gets the prepared upload count.</summary>
    public int Uploads { get; }

    /// <summary>Gets the prepared replacement-placement count.</summary>
    public int Placements { get; }

    /// <summary>Gets the prepared stale-removal count.</summary>
    public int Removals { get; }

    /// <summary>Gets whether ordinary cells must clear stale graphics first.</summary>
    public bool FullCellRedraw { get; }

    /// <summary>Gets placements that fell back to ordinary cells during this prepare, if any.</summary>
    public IReadOnlyList<GraphicsPlacementDiagnostic> SkippedPlacements { get; }
}
