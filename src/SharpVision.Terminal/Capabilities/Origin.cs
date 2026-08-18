// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies the evidence that produced a capability value.</summary>
[PublicAPI]
public enum Origin
{
    /// <summary>The conservative built-in profile supplied the value.</summary>
    Default,

    /// <summary>Environment hints supplied tentative or narrowing evidence.</summary>
    Environment,

    /// <summary>A bounded terminal query supplied the value.</summary>
    Query,

    /// <summary>An explicit caller override supplied the value.</summary>
    Override,

    /// <summary>A validated terminal-description database supplied the value.</summary>
    Database
}
