// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Provides a view whose Build result is invalid, for null-rejection tests.</summary>
internal sealed class NullView: View
{
    /// <inheritdoc/>
    protected override Control Build() => null!;
}
