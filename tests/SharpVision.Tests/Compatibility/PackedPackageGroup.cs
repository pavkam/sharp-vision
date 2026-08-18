// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

/// <summary>Serializes packed-NuGet-package consumer tests against unrelated test activity, since
/// they rebuild and read the shared src bin/obj outputs and must not overlap.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PackedPackageGroup
{
    /// <summary>Gets the xUnit collection name used by packed-package consumer tests.</summary>
    public const string Name = "PackedPackage";
}
