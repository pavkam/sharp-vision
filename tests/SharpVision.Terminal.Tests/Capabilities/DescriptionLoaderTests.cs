// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>Verifies deterministic platform terminal-description selection and fallback.</summary>
public sealed class DescriptionLoaderTests
{
    /// <summary>Verifies an explicit owned profile bypasses every platform provider.</summary>
    [Fact]
    public void Load_WhenExplicitProfileExists_ReturnsItWithoutCallingProviders()
    {
        var unix = new FakeDescriptionProvider();
        var windows = new FakeDescriptionProvider();
        var explicitProfile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        var loader = new DescriptionLoader(unix, windows);

        var result = loader.Load(Request(explicitProfile: explicitProfile));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        result.Profile.ShouldBeSameAs(explicitProfile);
        unix.Request.ShouldBeNull();
        windows.Request.ShouldBeNull();
    }

    /// <summary>Verifies Unix selection returns the provider-owned profile and exact request.</summary>
    [Fact]
    public void Load_WhenUnixProviderLoads_ReturnsProviderResult()
    {
        var profile = Profile(Suitability.Usable);
        var unix = new FakeDescriptionProvider
        {
            Result = DescriptionResult.Loaded(profile, Array.Empty<DescriptionDiagnostic>())
        };
        var windows = new FakeDescriptionProvider();
        var loader = new DescriptionLoader(unix, windows);
        var request = Request();

        var result = loader.Load(request);

        result.Profile.ShouldBeSameAs(profile);
        unix.Request.ShouldBeSameAs(request);
        windows.Request.ShouldBeNull();
    }

    /// <summary>Verifies Windows VT selection calls only the Windows provider.</summary>
    [Fact]
    public void Load_WhenWindowsVtIsEstablished_UsesWindowsProvider()
    {
        var profile = Profile(Suitability.Usable);
        var unix = new FakeDescriptionProvider();
        var windows = new FakeDescriptionProvider
        {
            Result = DescriptionResult.Loaded(profile, Array.Empty<DescriptionDiagnostic>())
        };
        var loader = new DescriptionLoader(unix, windows);
        var request = Request(DescriptionPlatform.Windows, windowsVirtualTerminal: true);

        var result = loader.Load(request);

        result.Profile.ShouldBeSameAs(profile);
        unix.Request.ShouldBeNull();
        windows.Request.ShouldBeSameAs(request);
    }

    /// <summary>Verifies a Windows console without established VT support emits no ANSI assumption.</summary>
    [Fact]
    public void Load_WhenWindowsVtIsNotEstablished_ReturnsUnavailableWithoutCallingProviders()
    {
        var unix = new FakeDescriptionProvider();
        var windows = new FakeDescriptionProvider();
        var loader = new DescriptionLoader(unix, windows);

        var result = loader.Load(Request(
            DescriptionPlatform.Windows,
            windowsVirtualTerminal: false,
            allowAnsiFallback: true));

        result.Status.ShouldBe(DescriptionLoadStatus.PlatformUnavailable);
        result.Profile.ShouldBeNull();
        unix.Request.ShouldBeNull();
        windows.Request.ShouldBeNull();
    }

    /// <summary>Verifies missing Unix evidence remains a typed unsuitable result by default.</summary>
    [Theory]
    [InlineData((int) DescriptionLoadStatus.MissingOrGeneric)]
    [InlineData((int) DescriptionLoadStatus.PlatformUnavailable)]
    public void Load_WhenUnixEvidenceIsAbsentByDefault_DoesNotInventAnsi(
        int statusValue)
    {
        var status = (DescriptionLoadStatus) statusValue;
        var unix = new FakeDescriptionProvider { Result = Absent(status) };
        var loader = new DescriptionLoader(unix, new FakeDescriptionProvider());

        var result = loader.Load(Request());

        result.Status.ShouldBe(status);
        result.Profile.ShouldBeNull();
    }

    /// <summary>Verifies opt-in fallback applies only to provider absence and retains diagnostics.</summary>
    [Fact]
    public void Load_WhenUnixProviderIsUnavailableAndFallbackAllowed_ReturnsAnsi()
    {
        var diagnostic = new DescriptionDiagnostic(DescriptionDiagnosticCode.MissingOrGeneric);
        var unix = new FakeDescriptionProvider
        {
            Result = DescriptionResult.PlatformUnavailable([diagnostic])
        };
        var loader = new DescriptionLoader(unix, new FakeDescriptionProvider());

        var result = loader.Load(Request(allowAnsiFallback: true));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        var profile = result.Profile.ShouldNotBeNull();
        profile.Description.Name.ShouldBe("ansi");
        profile.Description.Suitability.ShouldBe(Suitability.Usable);
        profile.Capabilities.ShouldBeSameAs(TerminalCapabilities.Conservative);
        result.Diagnostics.ShouldContain(diagnostic);
        result.Diagnostics.ShouldContain(item => item.Code == DescriptionDiagnosticCode.AnsiFallback);
    }

    /// <summary>Verifies ambiguous missing-or-generic evidence cannot replace a possibly generic entry.</summary>
    [Fact]
    public void Load_WhenResultIsMissingOrGenericAndFallbackAllowed_PreservesAmbiguity()
    {
        var unix = new FakeDescriptionProvider
        {
            Result = DescriptionResult.MissingOrGeneric()
        };
        var loader = new DescriptionLoader(unix, new FakeDescriptionProvider());

        var result = loader.Load(Request(allowAnsiFallback: true));

        result.Status.ShouldBe(DescriptionLoadStatus.MissingOrGeneric);
        result.Profile.ShouldBeNull();
    }

    /// <summary>Verifies provider failures remain diagnostics instead of being disguised as absence.</summary>
    [Fact]
    public void Load_WhenProviderFailsAndFallbackAllowed_PreservesFailure()
    {
        var diagnostic = new DescriptionDiagnostic(DescriptionDiagnosticCode.NativeFailure);
        var unix = new FakeDescriptionProvider
        {
            Result = DescriptionResult.ProviderFailed([diagnostic])
        };
        var loader = new DescriptionLoader(unix, new FakeDescriptionProvider());

        var result = loader.Load(Request(allowAnsiFallback: true));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Profile.ShouldBeNull();
        result.Diagnostics.ShouldBe([diagnostic]);
    }

    /// <summary>Verifies accepted unsuitable descriptions are final even when ANSI fallback is allowed.</summary>
    [Theory]
    [InlineData(Suitability.Generic)]
    [InlineData(Suitability.Hardcopy)]
    [InlineData(Suitability.Incomplete)]
    [InlineData(Suitability.UnsupportedPadding)]
    public void Load_WhenAcceptedDescriptionIsUnsuitable_DoesNotReplaceIt(Suitability suitability)
    {
        var profile = Profile(suitability);
        var unix = new FakeDescriptionProvider
        {
            Result = DescriptionResult.Loaded(profile, Array.Empty<DescriptionDiagnostic>())
        };
        var loader = new DescriptionLoader(unix, new FakeDescriptionProvider());

        var result = loader.Load(Request(allowAnsiFallback: true));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        result.Profile.ShouldBeSameAs(profile);
        profile.Description.Suitability.ShouldBe(suitability);
    }

    /// <summary>Verifies loader input is required.</summary>
    [Fact]
    public void Load_WhenRequestIsNull_Throws()
    {
        var loader = new DescriptionLoader(new FakeDescriptionProvider(), new FakeDescriptionProvider());

        _ = Should.Throw<ArgumentNullException>(() => loader.Load(request: null!));
    }

    /// <summary>Verifies both injected platform providers are required.</summary>
    [Fact]
    public void Constructor_WhenProviderIsNull_Throws()
    {
        var provider = new FakeDescriptionProvider();

        _ = Should.Throw<ArgumentNullException>(() => new DescriptionLoader(null!, provider));
        _ = Should.Throw<ArgumentNullException>(() => new DescriptionLoader(provider, null!));
    }

    private static DescriptionResult Absent(
        DescriptionLoadStatus status,
        params DescriptionDiagnostic[] diagnostics) => status switch
        {
            DescriptionLoadStatus.MissingOrGeneric => DescriptionResult.MissingOrGeneric(diagnostics),
            DescriptionLoadStatus.PlatformUnavailable => DescriptionResult.PlatformUnavailable(diagnostics),
            DescriptionLoadStatus.Loaded => throw new ArgumentOutOfRangeException(nameof(status)),
            DescriptionLoadStatus.ProviderFailed => throw new ArgumentOutOfRangeException(nameof(status)),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    private static TerminalProfile Profile(Suitability suitability) => new(
        new Description("fixture", DescriptionOrigin.Database, suitability),
        TerminalCapabilities.Conservative,
        new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[H\u001b[2J"u8)
        }),
        KeyMap.Empty);

    private static DescriptionRequest Request(
        DescriptionPlatform platform = DescriptionPlatform.Unix,
        bool windowsVirtualTerminal = false,
        bool allowAnsiFallback = false,
        TerminalProfile? explicitProfile = null) => new(
            "fixture",
            platform,
            outputFileDescriptor: 1,
            DescriptionLimits.Default,
            explicitProfile,
            allowAnsiFallback,
            windowsVirtualTerminal);
}
