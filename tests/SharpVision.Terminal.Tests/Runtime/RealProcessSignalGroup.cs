// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Serializes fixtures that send or observe process-wide Unix signals, preventing one
/// fixture's SIGWINCH from satisfying another fixture's pending resize read.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealProcessSignalGroup
{
    /// <summary>Gets the collection shared by real signal producers and observers.</summary>
    public const string Name = "Real process signals";
}
