// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Multiplexing;
using SharpVision.Terminal.Tests.Support;


/// <summary>Proves authoritative backend priority and mixed per-placement fallback.</summary>
public sealed class GraphicsBackendSelectorTests
{
    /// <summary>Verifies the shared default prepared-byte budget matches its documented value.
    /// KittyGraphicsBackend and NonRetainedGraphicsBackend (which configures sixel and iTerm2
    /// output directly) both default their own maxPreparedBytes parameter to this same
    /// constant instead of independently repeating the literal.</summary>
    [Fact]
    public void DefaultMaxPreparedBytes_WhenRead_Is16Mebibytes() =>
        GraphicsBackendSelector.DefaultMaxPreparedBytes.ShouldBe(16 * 1024 * 1024);

    /// <summary>Verifies authoritative Kitty support dominates both fallback protocols.</summary>
    [Fact]
    public void Create_WhenAllProtocolsAreSupported_SelectsKitty()
    {
        var profile = Profile(
            kitty: new Feature(CapabilitySupport.Supported, Origin.Query),
            sixel: new Feature(CapabilitySupport.Supported, Origin.Query),
            iterm: new Feature(CapabilitySupport.Supported, Origin.Override));

        using var backend = GraphicsBackendSelector.Create(profile.Capabilities);

        _ = backend.ShouldBeOfType<KittyGraphicsBackend>();
    }

    /// <summary>Verifies environment and database evidence cannot authorize iTerm2 3.5 multipart.</summary>
    [Theory]
    [InlineData(CapabilitySupport.Tentative, Origin.Environment)]
    [InlineData(CapabilitySupport.Supported, Origin.Database)]
    public void Create_WhenItermEvidenceIsNotOverrideOrQuery_ReturnsFallback(
        CapabilitySupport state,
        Origin origin)
    {
        var profile = Profile(iterm: new Feature(state, origin));

        var backend = GraphicsBackendSelector.Create(profile.Capabilities);

        backend.ShouldBeNull();
    }

    /// <summary>Verifies query evidence cannot authorize iTerm2 3.5 multipart, unlike Kitty and
    /// sixel: OSC 1337 Capabilities Query's F reply code is ambiguous between FILE and
    /// FOCUS_REPORTING support, so it can only ever demote iTerm2 to Unsupported, never promote it.</summary>
    [Fact]
    public void Create_WhenItermEvidenceIsQuery_ReturnsFallback()
    {
        var profile = Profile(iterm: new Feature(CapabilitySupport.Supported, Origin.Query));

        var backend = GraphicsBackendSelector.Create(profile.Capabilities);

        backend.ShouldBeNull();
    }

    /// <summary>Verifies environment and database origins cannot authorize Kitty or sixel output.</summary>
    [Theory]
    [InlineData(Origin.Environment)]
    [InlineData(Origin.Database)]
    public void Create_WhenKittyOrSixelOriginIsNotAuthoritative_ReturnsFallback(Origin origin)
    {
        var supported = new Feature(CapabilitySupport.Supported, origin);

        GraphicsBackendSelector.Create(Profile(kitty: supported).Capabilities).ShouldBeNull();
        GraphicsBackendSelector.Create(Profile(sixel: supported).Capabilities).ShouldBeNull();
    }

    /// <summary>
    /// Verifies weak evidence never authorizes graphics even for a terminal whose identity
    /// implements the protocol. Selection reads capability evidence only, so identity cannot
    /// promote tentative environment evidence into authoritative support.
    /// </summary>
    [Fact]
    public void Create_WhenEvidenceIsBelowAuthoritative_ReturnsFallback()
    {
        var kitty = Profile(kitty: new Feature(CapabilitySupport.Tentative, Origin.Environment));
        var iterm = Profile(iterm: new Feature(CapabilitySupport.Tentative, Origin.Query));

        GraphicsBackendSelector.Create(kitty.Capabilities).ShouldBeNull();
        GraphicsBackendSelector.Create(iterm.Capabilities).ShouldBeNull();
    }

    /// <summary>Verifies a Screen route disables graphics selection before backend construction.</summary>
    [Fact]
    public void Create_WhenRouteContainsScreen_ReturnsFallback()
    {
        var profile = Profile(iterm: new Feature(CapabilitySupport.Supported, Origin.Override));
        var route = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Screen],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics));

        var backend = GraphicsBackendSelector.Create(profile.Capabilities, route);

        backend.ShouldBeNull();
    }

    /// <summary>Verifies Screen disables even otherwise authoritative Kitty output.</summary>
    [Fact]
    public void Create_WhenKittyIsSupportedButRouteContainsScreen_ReturnsFallback()
    {
        var profile = Profile(kitty: new Feature(CapabilitySupport.Supported, Origin.Query));
        var route = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Screen],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics));

        GraphicsBackendSelector.Create(profile.Capabilities, route).ShouldBeNull();
    }

    /// <summary>Verifies missing operation authorization or pane visibility disables tmux output.</summary>
    [Fact]
    public void Create_WhenTmuxRouteIsNotAuthorizedOrVisible_ReturnsFallback()
    {
        var profile = Profile(iterm: new Feature(CapabilitySupport.Supported, Origin.Override));
        var outer = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        var unauthorized = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            outer,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries));
        var hidden = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            outer,
            PassthroughMode.Visible,
            paneVisible: false,
            MultiplexingOperation.Graphics));

        GraphicsBackendSelector.Create(profile.Capabilities, unauthorized).ShouldBeNull();
        GraphicsBackendSelector.Create(profile.Capabilities, hidden).ShouldBeNull();
    }

    /// <summary>Verifies a route too small for one worst-case Kitty chunk falls through to an
    /// otherwise-authoritative fallback protocol instead of constructing a KittyGraphicsBackend
    /// that would later throw out of Prepare when the undersized budget is discovered.</summary>
    [Fact]
    public void Create_WhenRouteIsTooSmallForOneKittyChunk_FallsBackToNonRetainedBackend()
    {
        var profile = Profile(
            kitty: new Feature(CapabilitySupport.Supported, Origin.Query),
            sixel: new Feature(CapabilitySupport.Supported, Origin.Query));
        var route = new MultiplexerRoute(new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics,
            maxEnvelopeBytes: 10));

        using var backend = GraphicsBackendSelector.Create(profile.Capabilities, route);

        _ = backend.ShouldBeOfType<NonRetainedGraphicsBackend>();
    }

    /// <summary>Verifies mixed RGBA and PNG placements retain original paint order in one stream.</summary>
    [Fact]
    public void Prepare_WhenSixelAndItermAreAuthoritative_PreservesMixedFrameOrder()
    {
        var profile = Profile(
            sixel: new Feature(CapabilitySupport.Supported, Origin.Query),
            iterm: new Feature(CapabilitySupport.Supported, Origin.Override));
        using var backend = GraphicsBackendSelector.Create(profile.Capabilities)!;
        using var frame = new Frame(new Size(3, 1));
        var rgba = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]);
        var png = Png();
        frame.Canvas.DrawImage(rgba, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
        frame.Canvas.DrawImage(png, new Rect(1, 0, 1, 1), PlacementMode.Contain);
        frame.Canvas.DrawImage(rgba, new Rect(2, 0, 1, 1), PlacementMode.Stretch);

        var result = backend.Prepare(
            null,
            frame,
            full: true,
            new GraphicsContext(profile, new CellMetrics(1, 6)));
        var bytes = WritePlacements(backend);
        var firstSixel = bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8);
        var iterm = bytes.AsSpan().IndexOf("\u001b]1337;MultipartFile="u8);
        var secondRelative = bytes.AsSpan((firstSixel + 1)..).IndexOf("\u001bP0;1;0q"u8);

        result.Placements.ShouldBe(3);
        firstSixel.ShouldBeGreaterThanOrEqualTo(0);
        iterm.ShouldBeGreaterThan(firstSixel);
        secondRelative.ShouldBeGreaterThanOrEqualTo(0);
        var secondSixel = secondRelative + firstSixel + 1;
        secondSixel.ShouldBeGreaterThan(iterm);
    }

    /// <summary>Verifies damage under a lower sixel replays its later overlapping iTerm image.</summary>
    [Fact]
    public void Prepare_WhenMixedPlacementsOverlap_RepaintsLaterProtocolInPaintOrder()
    {
        var profile = Profile(
            sixel: new Feature(CapabilitySupport.Supported, Origin.Query),
            iterm: new Feature(CapabilitySupport.Supported, Origin.Override));
        using var backend = GraphicsBackendSelector.Create(profile.Capabilities)!;
        var rgba = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]);
        var png = Png();
        using var first = new Frame(new Size(3, 1));
        _ = first.Canvas.Draw("abc", default);
        first.Canvas.DrawImage(rgba, new Rect(0, 0, 2, 1), PlacementMode.Stretch);
        first.Canvas.DrawImage(png, new Rect(1, 0, 2, 1), PlacementMode.Contain);
        using var changed = new Frame(new Size(3, 1));
        _ = changed.Canvas.Draw("xbc", default);
        changed.Canvas.DrawImage(rgba, new Rect(0, 0, 2, 1), PlacementMode.Stretch);
        changed.Canvas.DrawImage(png, new Rect(1, 0, 2, 1), PlacementMode.Contain);
        var context = new GraphicsContext(profile, new CellMetrics(1, 6));
        _ = backend.Prepare(null, first, full: true, context);
        backend.Commit();

        var result = backend.Prepare(first, changed, full: false, context);
        var bytes = WritePlacements(backend);
        var sixel = bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8);
        var iterm = bytes.AsSpan().IndexOf("\u001b]1337;MultipartFile="u8);

        result.Placements.ShouldBe(2);
        sixel.ShouldBeGreaterThanOrEqualTo(0);
        iterm.ShouldBeGreaterThan(sixel);
    }

    /// <summary>Verifies missing sixel metrics does not strand a viable PNG placement.</summary>
    [Fact]
    public void Prepare_WhenMetricsAreMissing_StillEmitsItermPngOnly()
    {
        var profile = Profile(
            sixel: new Feature(CapabilitySupport.Supported, Origin.Override),
            iterm: new Feature(CapabilitySupport.Supported, Origin.Override));
        using var backend = GraphicsBackendSelector.Create(profile.Capabilities)!;
        using var frame = new Frame(new Size(2, 1));
        frame.Canvas.DrawImage(
            GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch);
        frame.Canvas.DrawImage(Png(), new Rect(1, 0, 1, 1), PlacementMode.Contain);

        var result = backend.Prepare(
            null,
            frame,
            full: true,
            new GraphicsContext(profile, metrics: null));
        var bytes = WritePlacements(backend);

        // Both placements route through iTerm2 (sixel needs metrics to size pixels;
        // iTerm2 does not, and now also accepts the RGBA source by PNG-encoding it on demand).
        result.Placements.ShouldBe(2);
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("\u001b]1337;MultipartFile="u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies revoked non-retained evidence repairs cells without further graphics bytes.</summary>
    [Fact]
    public void Prepare_WhenSixelAndItermAreRevoked_RequestsFullCellRepairWithoutGraphics()
    {
        var supported = Profile(
            sixel: new Feature(CapabilitySupport.Supported, Origin.Query),
            iterm: new Feature(CapabilitySupport.Supported, Origin.Override));
        using var backend = GraphicsBackendSelector.Create(supported.Capabilities)!;
        using var front = new Frame(new Size(2, 1));
        front.Canvas.DrawImage(
            GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch);
        front.Canvas.DrawImage(Png(), new Rect(1, 0, 1, 1), PlacementMode.Contain);
        _ = backend.Prepare(
            null,
            front,
            full: true,
            new GraphicsContext(supported, new CellMetrics(1, 6)));
        backend.Commit();
        using var back = front.Clone();
        var revoked = Profile(
            sixel: new Feature(CapabilitySupport.Unsupported, Origin.Override),
            iterm: new Feature(CapabilitySupport.Unsupported, Origin.Override));

        var result = backend.Prepare(
            front,
            back,
            full: true,
            new GraphicsContext(revoked, new CellMetrics(1, 6)));
        var bytes = WritePlacements(backend);

        result.Changed.ShouldBeTrue();
        result.FullCellRedraw.ShouldBeTrue();
        result.Placements.ShouldBe(0);
        bytes.ShouldBeEmpty();
    }

    private static TerminalProfile Profile(
        Feature? kitty = null,
        Feature? sixel = null,
        Feature? iterm = null) => TerminalProfile.CreateAnsi(
        TerminalCapabilities.Conservative with
        {
            KittyGraphics = kitty ?? Feature.Unknown,
            Sixel = sixel ?? Feature.Unknown,
            ItermImages = iterm ?? Feature.Unknown
        });


    private static GraphicsImage Png() => GraphicsImage.FromPng(PngTestData.CreateContainer());

    private static byte[] WritePlacements(IGraphicsBackend backend)
    {
        var output = new ArrayBufferWriter<byte>();
        backend.WritePlacements(output);
        return output.WrittenSpan.ToArray();
    }
}
