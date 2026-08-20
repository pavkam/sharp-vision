// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>Maps a percentage reading to the semantic color a viewer should read it as.</summary>
internal static class SeverityPalette
{
    /// <summary>Chooses the semantic color for one percentage reading against fixed thresholds.</summary>
    /// <param name="percent">The reading to classify.</param>
    /// <param name="highThreshold">The inclusive threshold at or above which the reading is severe.</param>
    /// <param name="mediumThreshold">The inclusive threshold at or above which the reading is
    /// elevated but not yet severe.</param>
    /// <returns><see cref="SemanticColor.Error"/>, <see cref="SemanticColor.Warning"/>, or
    /// <see cref="SemanticColor.ControlText"/>.</returns>
    internal static SemanticColor ForPercent(double percent, double highThreshold, double mediumThreshold) => percent switch
    {
        _ when percent >= highThreshold => SemanticColor.Error,
        _ when percent >= mediumThreshold => SemanticColor.Warning,
        _ => SemanticColor.ControlText
    };
}
