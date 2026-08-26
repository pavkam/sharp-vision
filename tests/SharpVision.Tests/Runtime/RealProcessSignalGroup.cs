// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Serializes tests that raise a real POSIX signal against the current process.</summary>
/// <remarks>
/// Signal delivery is process-wide: every live <c>PosixSignalRegistration</c> for a given signal
/// number is invoked regardless of which test created it. Two such tests running concurrently in
/// different xUnit collections (the default for classes without an explicit <c>[Collection]</c>)
/// can each observe the other's raised signal, or race the other's registration teardown - this
/// group keeps every real-signal test single-file to eliminate that cross-talk.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealProcessSignalGroup
{
    /// <summary>Gets the xUnit collection name used by real-signal tests.</summary>
    public const string Name = "RealProcessSignal";
}
