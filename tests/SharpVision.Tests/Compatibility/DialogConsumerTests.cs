// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

/// <summary>Verifies public derivation contracts through the packed package without friend access.</summary>
[Collection(PackedPackageGroup.Name)]
public sealed class DialogConsumerTests
{
    /// <summary>Verifies an external package consumer can present and complete a derived Dialog
    /// through the protected PresentAsync(ControlBase, ControlBase?, CancellationToken) overload.</summary>
    [Fact]
    public async Task Dialog_WhenDerivedFromPackedPackage_CanPresentAndCompleteAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixtureRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "SharpVision.Tests",
            "Compatibility",
            "PackageConsumers",
            "DialogConsumer");
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"sharpvision-dialog-consumer-{Guid.NewGuid():N}");
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
                "--no-build",
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
                "--no-build",
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
                "SharpVision.DialogConsumer.csproj",
                "--configuration",
                "Release",
                $"-p:SharpVisionConsumerVersion={packageVersion}",
                $"-p:RestorePackagesPath={isolatedPackages}");
            await RunDotnetAsync(
                consumerRoot,
                cancellationToken,
                Path.Combine("bin", "Release", "net10.0", "SharpVision.DialogConsumer.dll"));
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
