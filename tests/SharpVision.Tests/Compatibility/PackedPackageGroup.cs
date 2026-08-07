// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

/// <summary>Serializes every packed-NuGet-package consumer test against unrelated test activity.
/// Each member packs src/SharpVision(.Terminal)/*.csproj straight from the checked-out source
/// tree - the packed nupkg lands in a per-run temporary directory, but the intermediate MSBuild
/// obj/bin the pack step reads from and writes into is the ordinary shared one under src/, since
/// pack is not given an isolated BaseIntermediateOutputPath. FigletFontsPackedPackageConsumerTests
/// packs those same two projects without --no-build, so it recompiles them into that shared
/// location; PackedPackageConsumerTests packs them with --no-build, so it only reads whatever is
/// currently there. Left in separate xUnit collections, those two classes are free to run
/// concurrently under the default full-suite parallelism, letting one test's in-flight rebuild
/// race the other's read of the same DLL and nuspec files.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PackedPackageGroup
{
    /// <summary>Gets the xUnit collection name used by packed-package consumer tests.</summary>
    public const string Name = "PackedPackage";
}
