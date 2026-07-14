// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>A probe that shrink-wraps its width for arrange-seam tests.</summary>
internal sealed class ShrinkProbe: Control
{
    private readonly Size _intrinsic;

    /// <summary>Initializes the probe with one intrinsic size.</summary>
    /// <param name="intrinsic">The non-negative intrinsic content size.</param>
    internal ShrinkProbe(Size intrinsic) => _intrinsic = intrinsic;

    /// <inheritdoc/>
    internal override bool ShrinkWrapsWidth => true;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => _intrinsic;
}
