// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>A minimal <see cref="NumericInputBase"/> derivative implementing only the required
/// seams with identity behavior, proving the base's shared range, commit, event, and measurement
/// contract independent of any concrete field's own formatting policy.</summary>
internal sealed class NumericInputBaseProbe: NumericInputBase
{
    /// <inheritdoc/>
    protected override string FormatValue(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    protected override NumberFormatInfo BuildBufferFormat() => Culture.NumberFormat;

    /// <inheritdoc/>
    protected override int EffectiveDecimalPlaces => 2;

    /// <inheritdoc/>
    protected override bool IsIntegerOnly => false;

    /// <inheritdoc/>
    protected override decimal ResolveCommitRounding(decimal parsed) =>
        NumericInputCommitCoordinator.RoundAtAcceptedPrecision(parsed, EffectiveDecimalPlaces, RoundingMode);
}
