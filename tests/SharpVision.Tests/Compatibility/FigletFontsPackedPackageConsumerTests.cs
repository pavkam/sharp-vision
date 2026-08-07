// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

/// <summary>Verifies the optional FIGlet catalog through only packed NuGet dependencies.</summary>
[Collection(PackedPackageGroup.Name)]
public sealed class FigletFontsPackedPackageConsumerTests
{
    /// <summary>Verifies the package graph, embedded-resource boundary, and external rendering path.</summary>
    [Fact]
    public async Task FigletFonts_WhenConsumedFromPackedPackages_ResolvesAndRendersAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixtureRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "SharpVision.Tests",
            "Compatibility",
            "PackageConsumers",
            "FigletFontsConsumer");
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"sharpvision-figlet-fonts-consumer-{Guid.NewGuid():N}");
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
                "src/SharpVision.FigletFonts/SharpVision.FigletFonts.csproj",
                cancellationToken);

            var corePackage = MainPackage(packageRoot, "SharpVision");
            var fontPackage = MainPackage(packageRoot, "SharpVision.FigletFonts");
            var coreVersion = PackageVersion(corePackage, "SharpVision");
            var version = PackageVersion(fontPackage, "SharpVision.FigletFonts");
            var coreResources = ReadManifestResources(corePackage, "SharpVision.dll");
            var fontResources = ReadManifestResources(fontPackage, "SharpVision.FigletFonts.dll");

            var coreResourceDump = $"actual SharpVision.dll manifest resources: [{string.Join(", ", coreResources)}]";
            var fontResourceDump = $"actual SharpVision.FigletFonts.dll manifest resources: [{string.Join(", ", fontResources)}]";
            coreResources.ShouldNotContain(name => name.Contains("FigletFonts", StringComparison.Ordinal), coreResourceDump);
            coreResources.ShouldNotContain(name => name.EndsWith(".flf", StringComparison.Ordinal), coreResourceDump);
            coreResources.ShouldNotContain(name => name.EndsWith("fonts.zip", StringComparison.Ordinal), coreResourceDump);
            fontResources.Count(name => name.EndsWith(".flf", StringComparison.Ordinal)).ShouldBe(19, fontResourceDump);
            fontResources.ShouldContain("SharpVision.FigletFonts.Resources.fonts.manifest.json", fontResourceDump);
            fontResources.ShouldNotContain(name => name.EndsWith(".zip", StringComparison.Ordinal), fontResourceDump);

            var fontPackageEntries = ReadPackageEntries(fontPackage);
            var fontPackageEntryDump = $"actual {Path.GetFileName(fontPackage)} entries: [{string.Join(", ", fontPackageEntries)}]";
            fontPackageEntries.ShouldContain("THIRD-PARTY-NOTICES.md", fontPackageEntryDump);
            fontPackageEntries.ShouldContain("licenses/BSD-3-Clause.txt", fontPackageEntryDump);
            fontPackageEntries.ShouldContain("licenses/MIT.txt", fontPackageEntryDump);
            // Derived from the core package actually being tested, not a literal and not the font
            // package's own version - SharpVision.FigletFonts versions and publishes
            // independently of SharpVision, so the two numbers are not expected to match. As a
            // literal this asserted whatever the floor happened to say, so it could never detect
            // the floor falling behind the core it ships beside - which is exactly what had
            // happened: the packed font package declared a dependency on a one-release-old core,
            // and every other assertion here was evidence about that older core rather than the
            // one being published.
            ReadSharpVisionDependency(fontPackage).ShouldBe(
                coreVersion,
                "the packed font package must depend on the SharpVision core version it ships beside");

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
                "SharpVision.FigletFontsConsumer.csproj",
                "--configuration",
                "Release",
                $"-p:SharpVisionFigletFontsConsumerVersion={version}",
                $"-p:RestorePackagesPath={isolatedPackages}");
            await RunDotnetAsync(
                consumerRoot,
                cancellationToken,
                Path.Combine("bin", "Release", "net10.0", "SharpVision.FigletFontsConsumer.dll"));
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

    private static string[] ReadManifestResources(string package, string assemblyName)
    {
        using var archive = ZipFile.OpenRead(package);
        var entry = archive.Entries.Single(item => item.FullName.EndsWith("/" + assemblyName, StringComparison.Ordinal));
        using MemoryStream assembly = new();
        using (var entryStream = entry.Open())
        {
            entryStream.CopyTo(assembly);
        }

        assembly.Position = 0;
        using PEReader portableExecutable = new(assembly);
        var metadata = portableExecutable.GetMetadataReader();

        return
        [
            .. metadata.ManifestResources
                .Select(handle => metadata.GetString(metadata.GetManifestResource(handle).Name))
                .Order(StringComparer.Ordinal)
        ];
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

    private static string[] ReadPackageEntries(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        return [.. archive.Entries.Select(entry => entry.FullName)];
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
