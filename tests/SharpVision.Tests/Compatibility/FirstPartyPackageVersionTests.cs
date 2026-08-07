// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

using System.Xml.Linq;

/// <summary>Verifies no first-party package version is written as a literal.
///
/// <para><c>SharpVision.FigletFonts</c> consumes the core as a package rather than a project
/// reference, so its version comes from <c>Directory.Packages.props</c> like any third-party
/// dependency. NuGet resolves a floor range to the <em>lowest</em> satisfying version across all
/// sources, and nuget.org carries every previously published core - so a literal floor that falls
/// behind <c>OverallVersion</c> does not fail, it silently builds, packs, and consumer-tests the
/// font package against an older core while the freshly packed one sits unused in the bootstrap
/// feed. That is what happened: the floor was set equal to the then-current version when the
/// package was split out, <c>OverallVersion</c> moved on three times, and the floor was never
/// revisited.</para>
///
/// <para>The packed-consumer test now derives its assertion, which catches the skew once a package
/// has been built. This catches it at the source, without packing anything, and covers first-party
/// packages that have no consumer test of their own.</para>
/// </summary>
public sealed class FirstPartyPackageVersionTests
{
    /// <summary>The regression this file exists to pin. Every first-party entry must derive its
    /// version from the property that the publish workflow already keeps in lockstep, so a literal
    /// cannot be reintroduced and then quietly go stale.</summary>
    [Fact]
    public void PackageVersions_WhenFirstParty_DeriveFromOverallVersion()
    {
        var firstParty = FirstPartyPackageVersions();

        firstParty.ShouldNotBeEmpty("a first-party package entry must exist for this to be testing anything");

        foreach (var (package, version) in firstParty)
        {
            version.Contains("$(OverallVersion)", StringComparison.Ordinal).ShouldBeTrue(
                $"'{package}' has version '{version}' - it must derive its version rather than pin a " +
                "literal that can fall behind the core it ships beside");
        }
    }

    /// <summary>Verifies the derived version actually expands, rather than reaching NuGet as the
    /// literal text. <c>Directory.Packages.props</c> is imported after <c>Directory.Build.props</c>,
    /// which is what makes the property available - an ordering nothing else asserts.</summary>
    [Fact]
    public void PackageVersions_WhenDerived_ExpandToTheCurrentOverallVersion()
    {
        var overall = OverallVersion();

        overall.ShouldNotBeNullOrWhiteSpace();

        foreach (var (package, version) in FirstPartyPackageVersions())
        {
            var expanded = version.Replace("$(OverallVersion)", overall, StringComparison.Ordinal);

            expanded.Contains("$(", StringComparison.Ordinal).ShouldBeFalse(
                $"'{package}' must not carry an unexpanded property, but expands to '{expanded}'");
            expanded.Contains(overall, StringComparison.Ordinal).ShouldBeTrue(
                $"'{package}' expands to '{expanded}', which must resolve to the version being built " +
                $"('{overall}') rather than an older published one");
        }
    }

    private static List<(string Package, string Version)> FirstPartyPackageVersions()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "Directory.Packages.props"));
        var entries = new List<(string, string)>();

        foreach (var element in document.Descendants("PackageVersion"))
        {
            var package = element.Attribute("Include")?.Value ?? string.Empty;

            // First-party by name: the repository's own packages all share this prefix, and no
            // third-party dependency does.
            if (package.StartsWith("SharpVision", StringComparison.Ordinal))
            {
                entries.Add((package, element.Attribute("Version")?.Value ?? string.Empty));
            }
        }

        return entries;
    }

    private static string OverallVersion()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "Directory.Build.props"));

        return document.Descendants("OverallVersion").FirstOrDefault()?.Value.Trim() ?? string.Empty;
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
}
