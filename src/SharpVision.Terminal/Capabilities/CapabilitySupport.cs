// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies confidence in one optional terminal feature.</summary>
[PublicAPI]
public enum CapabilitySupport
{
    /// <summary>No reliable evidence is available.</summary>
    Unknown,

    /// <summary>Evidence says the feature is unavailable.</summary>
    Unsupported,

    /// <summary>An environment hint suggests support but must not enable it.</summary>
    Tentative,

    /// <summary>Validated database evidence, a bounded query, or an explicit override proves support.</summary>
    Supported
}
