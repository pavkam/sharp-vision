// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Classifies one platform terminal-description lookup.</summary>
[PublicAPI]
public enum DescriptionLoadStatus
{
    /// <summary>An owned description was loaded, including an unsuitable description.</summary>
    Loaded,

    /// <summary>ncurses could not distinguish a missing entry from a generic description.</summary>
    MissingOrGeneric,

    /// <summary>The platform or native provider library is unavailable.</summary>
    PlatformUnavailable,

    /// <summary>The native database or accepted entry failed validation.</summary>
    ProviderFailed
}
