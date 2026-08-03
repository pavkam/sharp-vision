// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>Identifies why a graphics placement fell back to ordinary cells instead of a protocol upload.</summary>
[PublicAPI]
public enum GraphicsPlacementSkipReason
{
    /// <summary>The image's pixel format has no encodable path on any protocol the active backend enabled.</summary>
    FormatNotEncodable,

    /// <summary>Every non-retained protocol the backend could otherwise use is deauthorized by the current profile.</summary>
    ProtocolNotAuthorized,

    /// <summary>The image format is supported, but its source rectangle, destination geometry, or placement mode is not.</summary>
    PlacementNotEncodable,

    /// <summary>The placement could be encoded, but not within the remaining prepared-frame byte limit.</summary>
    OutputLimitExceeded
}
