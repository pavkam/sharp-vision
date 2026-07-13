// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>Provides a readable stream that rejects write capability.</summary>
internal sealed class ReadOnlyStream: MemoryStream
{
    /// <inheritdoc/>
    public override bool CanWrite => false;
}
