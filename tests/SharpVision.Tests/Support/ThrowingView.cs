// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Provides a view whose Build always throws, for measure-failure propagation tests.</summary>
internal sealed class ThrowingView: View
{
    /// <inheritdoc/>
    protected override Control Build() => throw new InvalidOperationException("build failed");
}
