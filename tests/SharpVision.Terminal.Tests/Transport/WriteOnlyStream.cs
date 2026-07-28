// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>Provides a writable stream that rejects read capability.</summary>
internal sealed class WriteOnlyStream: MemoryStream
{
    /// <inheritdoc/>
    public override bool CanRead => false;
}
