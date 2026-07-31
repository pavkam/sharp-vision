// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies console connection initialization and asynchronous disposal behavior.
/// </summary>
public sealed class ConsoleConnectionTests
{
    /// <summary>
    /// Verifies that DisposeAsync restores the lease exactly once when called multiple times.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_RestoresExactlyOnceAsync()
    {
        var restore = new TrackingRestore();
        var connection = new ConsoleConnection(new FakeTransport(), new FakeResizeSource(), restore);

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        restore.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// Verifies that a null restore lease throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void Constructor_WhenRestoreNull_Throws()
    {
        _ = Should.Throw<ArgumentNullException>(() =>
            new ConsoleConnection(new FakeTransport(), new FakeResizeSource(), restore: null!));
    }

    /// <summary>
    /// Verifies that DisposeAsync restores the lease without disposing the transport, which the
    /// caller (e.g. the owning <c>Application</c>) is responsible for disposing.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalled_DoesNotDisposeTransportOrResizeAsync()
    {
        var transport = new FakeTransport();
        var connection = new ConsoleConnection(transport, new FakeResizeSource(), new TrackingRestore());

        await connection.DisposeAsync();

        transport.DisposeCount.ShouldBe(0);
    }

    /// <summary>Verifies a platform host can retain exact immutable description-resolution facts.</summary>
    [Theory]
    [InlineData((int) DescriptionPlatform.Unix, 1, false)]
    [InlineData((int) DescriptionPlatform.Windows, 1, true)]
    public void Constructor_WhenPlatformFactsAreEstablished_RetainsThem(
        int platformValue,
        int outputFileDescriptor,
        bool windowsVirtualTerminal)
    {
        var platform = (DescriptionPlatform) platformValue;
        var connection = new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore(),
            platform,
            outputFileDescriptor,
            windowsVirtualTerminal);

        connection.DescriptionPlatform.ShouldBe(platform);
        connection.OutputFileDescriptor.ShouldBe(outputFileDescriptor);
        connection.WindowsVirtualTerminal.ShouldBe(windowsVirtualTerminal);
    }

    /// <summary>Verifies the existing public constructor makes no unsupported platform claim.</summary>
    [Fact]
    public void Constructor_WhenPlatformFactsAreUnknown_LeavesThemAbsent()
    {
        var connection = new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore());

        connection.DescriptionPlatform.ShouldBeNull();
        connection.OutputFileDescriptor.ShouldBeNull();
        connection.WindowsVirtualTerminal.ShouldBeFalse();
    }

    /// <summary>Verifies inconsistent platform facts are rejected before observable state changes.</summary>
    [Fact]
    public void Constructor_WhenPlatformFactsAreInvalid_Throws()
    {
        var transport = new FakeTransport();
        var resize = new FakeResizeSource();
        var restore = new TrackingRestore();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ConsoleConnection(
            transport,
            resize,
            restore,
            DescriptionPlatform.Unix,
            -1,
            windowsVirtualTerminal: false));
        _ = Should.Throw<ArgumentException>(() => new ConsoleConnection(
            transport,
            resize,
            restore,
            DescriptionPlatform.Unix,
            1,
            windowsVirtualTerminal: true));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ConsoleConnection(
            transport,
            resize,
            restore,
            (DescriptionPlatform) int.MaxValue,
            1,
            windowsVirtualTerminal: false));
    }

    /// <summary>Verifies an explicit profile bypasses absent platform facts and native discovery.</summary>
    [Fact]
    public void ResolveProfile_WhenExplicitProfileExists_ReturnsSameInstance()
    {
        var connection = new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore());
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);

        var resolved = connection.ResolveProfile(profile);

        resolved.ShouldBeSameAs(profile);
    }

    /// <summary>Verifies a caller-built connection without platform facts cannot invent ANSI support.</summary>
    [Fact]
    public void ResolveProfile_WhenPlatformFactsAreAbsent_ReturnsNull()
    {
        var connection = new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore());

        var resolved = connection.ResolveProfile();

        resolved.ShouldBeNull();
    }

    /// <summary>Verifies the public typed result distinguishes unavailable platform facts from a null profile.</summary>
    [Fact]
    public void ResolveDescription_WhenPlatformFactsAreAbsent_ReturnsUnavailableStatus()
    {
        var connection = new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore());

        var resolved = connection.ResolveDescription();

        resolved.Status.ShouldBe(DescriptionLoadStatus.PlatformUnavailable);
        resolved.Profile.ShouldBeNull();
        resolved.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Verifies the public typed result retains provider failure diagnostics in order.</summary>
    [Fact]
    public void ResolveDescription_WhenProviderFails_RetainsRedactedDiagnostics()
    {
        DescriptionDiagnostic[] diagnostics =
        [
            new(DescriptionDiagnosticCode.NativeFailure),
            new(DescriptionDiagnosticCode.EnvironmentLimit),
            new(DescriptionDiagnosticCode.CleanupFailure)
        ];
        var provider = new FakeDescriptionProvider
        {
            Result = DescriptionResult.ProviderFailed(diagnostics)
        };
        var loader = new DescriptionLoader(provider, new FakeDescriptionProvider());
        var connection = new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore(),
            DescriptionPlatform.Unix,
            outputFileDescriptor: 1,
            windowsVirtualTerminal: false,
            descriptionLoader: loader,
            terminalName: "dumb");

        var resolved = connection.ResolveDescription();

        resolved.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        resolved.Profile.ShouldBeNull();
        resolved.Diagnostics.Select(static value => value.Code).ShouldBe(
            diagnostics.Select(static value => value.Code));
    }

    /// <summary>Verifies a deterministic environment reader resolves the terminal name instead of
    /// the live process environment (see #98 item 6).</summary>
    [Fact]
    public void ResolveDescription_WhenEnvironmentReaderIsInjected_ResolvesTerminalNameFromIt()
    {
        var provider = new FakeDescriptionProvider();
        var loader = new DescriptionLoader(provider, new FakeDescriptionProvider());
        var requestedKeys = new List<string>();
        var connection = new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore(),
            DescriptionPlatform.Unix,
            outputFileDescriptor: 1,
            windowsVirtualTerminal: false,
            loader,
            key =>
            {
                requestedKeys.Add(key);
                return "fake-term-256color";
            });

        _ = connection.ResolveDescription();

        requestedKeys.ShouldContain(EnvironmentNames.Term);
        provider.Request.ShouldNotBeNull().TerminalName.ShouldBe("fake-term-256color");
    }

    /// <summary>Verifies a missing environment variable produces a MissingOrGeneric result rather
    /// than reaching the description loader with an empty terminal name.</summary>
    [Fact]
    public void ResolveDescription_WhenInjectedEnvironmentReaderReturnsNull_ReportsMissingOrGeneric()
    {
        var provider = new FakeDescriptionProvider();
        var loader = new DescriptionLoader(provider, new FakeDescriptionProvider());
        var connection = new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore(),
            DescriptionPlatform.Unix,
            outputFileDescriptor: 1,
            windowsVirtualTerminal: false,
            loader,
            static _ => null);

        var resolved = connection.ResolveDescription();

        resolved.Status.ShouldBe(DescriptionLoadStatus.MissingOrGeneric);
        provider.Request.ShouldBeNull();
    }

    /// <summary>Verifies a description loader with neither a terminal name nor an environment
    /// reader is rejected at construction rather than deferring to a confusing later failure.</summary>
    [Fact]
    public void Constructor_WhenDescriptionLoaderHasNoNameSource_Throws()
    {
        var loader = new DescriptionLoader(new FakeDescriptionProvider(), new FakeDescriptionProvider());

        var exception = Should.Throw<ArgumentException>(() => new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore(),
            DescriptionPlatform.Unix,
            outputFileDescriptor: 1,
            windowsVirtualTerminal: false,
            descriptionLoader: loader,
            terminalName: null!));

        exception.ParamName.ShouldBe("descriptionLoader");
    }

    /// <summary>Verifies established Windows VT connection facts select the built-in profile.</summary>
    [Fact]
    public void ResolveProfile_WhenWindowsVtFactsExist_ReturnsBuiltInProfile()
    {
        var connection = new ConsoleConnection(
            new FakeTransport(),
            new FakeResizeSource(),
            new TrackingRestore(),
            DescriptionPlatform.Windows,
            outputFileDescriptor: 1,
            windowsVirtualTerminal: true);

        var resolved = connection.ResolveProfile().ShouldNotBeNull();

        resolved.Description.Name.ShouldBe("windows-vt");
        resolved.Description.Suitability.ShouldBe(Suitability.Usable);
    }

    /// <summary>Verifies deterministic description injection validates both loader and terminal name.</summary>
    /// <remarks>A null terminal name with a loader but no environment reader is covered separately
    /// by <see cref="Constructor_WhenDescriptionLoaderHasNoNameSource_Throws"/>, since an injected
    /// environment reader makes that combination valid (see #98 item 6).</remarks>
    [Fact]
    public void Constructor_WhenDescriptionInjectionIsInvalid_Throws()
    {
        var transport = new FakeTransport();
        var resize = new FakeResizeSource();
        var restore = new TrackingRestore();
        var loader = new DescriptionLoader(
            new FakeDescriptionProvider(),
            new FakeDescriptionProvider());

        var missingLoader = Should.Throw<ArgumentNullException>(() => new ConsoleConnection(
            transport,
            resize,
            restore,
            DescriptionPlatform.Unix,
            1,
            windowsVirtualTerminal: false,
            descriptionLoader: null!,
            terminalName: "dumb"));
        var blankName = Should.Throw<ArgumentException>(() => new ConsoleConnection(
            transport,
            resize,
            restore,
            DescriptionPlatform.Unix,
            1,
            windowsVirtualTerminal: false,
            loader,
            terminalName: " "));

        missingLoader.ParamName.ShouldBe("descriptionLoader");
        blankName.ParamName.ShouldBe("terminalName");
    }
}
