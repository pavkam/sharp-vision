// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>Reports one graphics placement that fell back to ordinary cells during the most recent frame.</summary>
/// <param name="ImageIdentity">The skipped placement's stable process-local image identity.</param>
/// <param name="Reason">Why the placement could not be encoded.</param>
[PublicAPI]
public readonly record struct GraphicsPlacementDiagnostic(ulong ImageIdentity, GraphicsPlacementSkipReason Reason);
