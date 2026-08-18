// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Backends;

/// <summary>Adapts one trusted source into one redacted terminal-backend evidence value.</summary>
internal interface IBackendEvidenceAdapter
{
    /// <summary>Attempts to publish the recognized backend family without exposing the source value.</summary>
    /// <param name="evidence">The redacted typed evidence when recognition succeeds.</param>
    /// <returns><see langword="true"/> when one backend family was recognized; otherwise <see langword="false"/>.</returns>
    public bool TryAdapt(out BackendEvidence evidence);
}
