// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Identifies the trusted boundary that recognized terminal-backend metadata.</summary>
internal enum BackendEvidenceOrigin
{
    /// <summary>Represents a recognized owned terminal-description name.</summary>
    Description,

    /// <summary>Represents a recognized caller-supplied environment value.</summary>
    Environment
}
