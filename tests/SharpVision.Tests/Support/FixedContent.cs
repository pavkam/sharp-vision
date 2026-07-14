// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Provides a control with a fixed measured content size for override-seam tests.</summary>
internal sealed class FixedContent: Control
{
    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => new(7, 3);
}
