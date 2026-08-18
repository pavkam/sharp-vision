// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

/// <summary>Verifies InputBase's protected capability seams - EnablePressActivation, EnablePopup,
/// TryGetStepDelta, ResolveDropDownGlyph, and VerifyMutable - resolve from a derived type declared
/// outside the packed SharpVision assembly, without leaking any internal type (SegmentFieldBehavior,
/// PressBehavior, PopupDropDownCoordinator, OwnedControlOptions, and so on) through the public
/// surface.</summary>
[Collection(PackedPackageGroup.Name)]
public sealed class InputBaseConsumerTests
{
    /// <summary>Verifies an external package consumer can derive InputBase directly and exercise
    /// every protected capability seam through a public attached application host.</summary>
    [Fact]
    public async Task InputBase_WhenDerivedFromPackedPackage_ExercisesEveryCapabilitySeamAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixtureRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "SharpVision.Tests",
            "Compatibility",
            "PackageConsumers",
            "InputBaseConsumer");
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"sharpvision-inputbase-consumer-{Guid.NewGuid():N}");
        var packageRoot = Path.Combine(temporaryRoot, "packages");
        var consumerRoot = Path.Combine(temporaryRoot, "consumer");
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            _ = Directory.CreateDirectory(packageRoot);
            _ = Directory.CreateDirectory(consumerRoot);

            foreach (var fixturePath in Directory.GetFiles(fixtureRoot))
            {
                File.Copy(fixturePath, Path.Combine(consumerRoot, Path.GetFileName(fixturePath)));
            }

            await RunDotnetAsync(
                repositoryRoot,
                cancellationToken,
                "pack",
                "src/SharpVision.Terminal/SharpVision.Terminal.csproj",
                "--configuration",
                "Release",
                "-p:IsPackable=true",
                "--output",
                packageRoot);
            await RunDotnetAsync(
                repositoryRoot,
                cancellationToken,
                "pack",
                "src/SharpVision/SharpVision.csproj",
                "--configuration",
                "Release",
                "--output",
                packageRoot);

            var packagePath = Directory.GetFiles(packageRoot, "SharpVision.*.nupkg")
                .Single(path => !Path.GetFileName(path).StartsWith("SharpVision.Terminal.", StringComparison.Ordinal));
            var packageFileName = Path.GetFileName(packagePath);
            var packageVersion = packageFileName["SharpVision.".Length..^".nupkg".Length];
            var nugetConfigPath = Path.Combine(consumerRoot, "NuGet.config");
            await File.WriteAllTextAsync(
                nugetConfigPath,
                $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="packed-sharpvision" value="{{packageRoot}}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                </configuration>
                """,
                cancellationToken);

            var isolatedPackages = Path.Combine(temporaryRoot, "restore");
            await RunDotnetAsync(
                consumerRoot,
                cancellationToken,
                "build",
                "SharpVision.InputBaseConsumer.csproj",
                "--configuration",
                "Release",
                $"-p:SharpVisionConsumerVersion={packageVersion}",
                $"-p:RestorePackagesPath={isolatedPackages}");
            await RunDotnetAsync(
                consumerRoot,
                cancellationToken,
                Path.Combine("bin", "Release", "net10.0", "SharpVision.InputBaseConsumer.dll"));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharpVision.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The SharpVision repository root could not be located.");
    }

    private static async Task RunDotnetAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start().ShouldBeTrue();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;
        process.ExitCode.ShouldBe(
            0,
            $"dotnet {string.Join(' ', arguments)} failed.{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }
}
