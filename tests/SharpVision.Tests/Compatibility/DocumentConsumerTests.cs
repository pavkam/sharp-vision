// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

using System.IO.Compression;
using System.Xml.Linq;

/// <summary>Verifies the optional Document package through only packed NuGet dependencies.</summary>
[Collection(PackedPackageGroup.Name)]
public sealed class DocumentConsumerTests
{
    /// <summary>Verifies package dependencies and an unfriended Markdown-to-control consumer.</summary>
    [Fact]
    public async Task Document_WhenConsumedFromPackedPackages_LoadsMarkdownAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixtureRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "SharpVision.Tests",
            "Compatibility",
            "PackageConsumers",
            "DocumentConsumer");
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"sharpvision-document-consumer-{Guid.NewGuid():N}");
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

            await PackAsync(
                repositoryRoot,
                packageRoot,
                "src/SharpVision.Terminal/SharpVision.Terminal.csproj",
                cancellationToken,
                "-p:IsPackable=true");
            await PackAsync(
                repositoryRoot,
                packageRoot,
                "src/SharpVision/SharpVision.csproj",
                cancellationToken);
            await PackAsync(
                repositoryRoot,
                packageRoot,
                "src/SharpVision.Document/SharpVision.Document.csproj",
                cancellationToken);

            var corePackage = MainPackage(packageRoot, "SharpVision");
            var documentPackage = MainPackage(packageRoot, "SharpVision.Document");
            var coreVersion = PackageVersion(corePackage, "SharpVision");
            var documentVersion = PackageVersion(documentPackage, "SharpVision.Document");
            ReadSharpVisionDependency(documentPackage).ShouldBe(
                coreVersion,
                "the packed Document package must depend on the core version it ships beside");

            await File.WriteAllTextAsync(
                Path.Combine(consumerRoot, "NuGet.config"),
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
                "SharpVision.DocumentConsumer.csproj",
                "--configuration",
                "Release",
                $"-p:SharpVisionDocumentConsumerVersion={documentVersion}",
                $"-p:RestorePackagesPath={isolatedPackages}");
            await RunDotnetAsync(
                consumerRoot,
                cancellationToken,
                Path.Combine("bin", "Release", "net10.0", "SharpVision.DocumentConsumer.dll"));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static Task PackAsync(
        string repositoryRoot,
        string packageRoot,
        string project,
        CancellationToken cancellationToken,
        params string[] extraArguments) =>
        RunDotnetAsync(
            repositoryRoot,
            cancellationToken,
            [
                "pack",
                project,
                "--configuration",
                "Release",
                "--no-restore",
                "--output",
                packageRoot,
                .. extraArguments
            ]);

    private static string MainPackage(string root, string packageId) =>
        Directory.GetFiles(root, $"{packageId}.*.nupkg")
            .Single(path =>
                !path.EndsWith(".snupkg", StringComparison.Ordinal) &&
                char.IsAsciiDigit(Path.GetFileName(path)[packageId.Length + 1]));

    private static string PackageVersion(string package, string packageId)
    {
        var fileName = Path.GetFileName(package);
        return fileName[(packageId.Length + 1)..^".nupkg".Length];
    }

    private static string ReadSharpVisionDependency(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        var entry = archive.Entries.Single(item => item.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        var ns = document.Root!.Name.Namespace;

        return document.Descendants(ns + "dependency")
            .Single(element => element.Attribute("id")?.Value == "SharpVision")
            .Attribute("version")!
            .Value;
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
