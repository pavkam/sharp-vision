// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Serializes allocation measurements against unrelated test activity.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceGroup
{
    /// <summary>Gets the xUnit collection name used by performance gates.</summary>
    public const string Name = "Performance";
}
