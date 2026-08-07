// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery;

/// <summary>Defines the fixed precedence phases for semantic capability discovery.</summary>
internal enum DiscoveryPhase
{
    /// <summary>Applies caller-supplied environment hints and safety narrowing.</summary>
    Environment,

    /// <summary>Applies bounded, validated query evidence after environment hints.</summary>
    Query,

    /// <summary>Applies explicit caller policy after every inferred evidence source.</summary>
    Override
}
