// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Compatibility.Tests;

/// <summary>
/// Guards the complete public surfaces of the published SharpVision assemblies.
/// </summary>
public sealed class PublicApiCompatibilityTests
{
    /// <summary>
    /// Verifies the terminal library against the baseline for the current package version.
    /// </summary>
    [Fact]
    public Task SharpVisionTerminal_WhenComparedWithVersionedBaseline_MatchesApprovedPublicApi()
    {
        var assembly = typeof(Terminal.Geometry.Point).Assembly;

        return VerifyPublicApiAsync(assembly, "SharpVision.Terminal");
    }

    /// <summary>
    /// Verifies the UI library against the baseline for the current package version.
    /// </summary>
    [Fact]
    public Task SharpVision_WhenComparedWithVersionedBaseline_MatchesApprovedPublicApi()
    {
        var assembly = typeof(Controls.Control).Assembly;

        return VerifyPublicApiAsync(assembly, "SharpVision");
    }

    private static Task VerifyPublicApiAsync(Assembly assembly, string assemblyName)
    {
        var publicApi = assembly.GeneratePublicApi();
        var version = typeof(PublicApiCompatibilityTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "OverallVersion")
            .Value;

        assembly.GetName().Name.ShouldBe(assemblyName);
        publicApi.ShouldNotBeNullOrWhiteSpace();
        version.ShouldNotBeNullOrWhiteSpace();

        var settings = new VerifySettings();
        settings.UseDirectory(Path.Combine("Snapshots", version));
        settings.UseFileName(assemblyName);

        return Verify(publicApi, settings);
    }
}
