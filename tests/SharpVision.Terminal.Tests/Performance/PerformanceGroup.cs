// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Performance;

/// <summary>Serializes terminal allocation measurements against unrelated test activity.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceGroup
{
    /// <summary>Gets the xUnit collection name used by terminal performance gates.</summary>
    public const string Name = "Terminal performance";
}
