// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies optional terminal output requires authoritative capability evidence, not merely a
/// supported state a caller can construct from a guess.
/// </summary>
public sealed class CapabilityOriginGatingTests
{
    /// <summary>Verifies only description, query, and override evidence authorizes output.</summary>
    /// <param name="origin">The evidence origin under test.</param>
    /// <param name="expected">Whether that origin may put optional bytes on the wire.</param>
    [Theory]
    [InlineData(Origin.Default, false)]
    [InlineData(Origin.Environment, false)]
    [InlineData(Origin.Database, true)]
    [InlineData(Origin.Query, true)]
    [InlineData(Origin.Override, true)]
    public void IsAuthoritative_WhenSupported_FollowsEvidenceOrigin(Origin origin, bool expected) =>
        new Feature(CapabilitySupport.Supported, origin).IsAuthoritative.ShouldBe(expected);

    /// <summary>Verifies an unsupported state is never authoritative regardless of origin.</summary>
    /// <param name="state">The non-supported state under test.</param>
    [Theory]
    [InlineData(CapabilitySupport.Unknown)]
    [InlineData(CapabilitySupport.Tentative)]
    [InlineData(CapabilitySupport.Unsupported)]
    public void IsAuthoritative_WhenNotSupported_IsFalse(CapabilitySupport state) =>
        new Feature(state, Origin.Override).IsAuthoritative.ShouldBeFalse();

    /// <summary>
    /// Verifies environment-only evidence emits no synchronized-output wrapping, no overline, no
    /// typed underline, and no underline color, even though every feature claims support.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenEvidenceIsEnvironmentOnly_OmitsOptionalSequencesAsync()
    {
        var emitted = await RenderDecoratedAsync(Origin.Environment);

        emitted.ShouldNotContain("\u001b[?2026h");
        emitted.ShouldNotContain("\u001b[?2026l");
        emitted.ShouldNotContain("\u001b[53m");
        emitted.ShouldNotContain("\u001b[4:3m");
        emitted.ShouldNotContain("\u001b[58;2;");
    }

    /// <summary>
    /// Verifies the same content under override evidence still emits every optional sequence, so
    /// the gate rejects the origin rather than disabling the feature.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenEvidenceIsAuthoritative_EmitsOptionalSequencesAsync()
    {
        var emitted = await RenderDecoratedAsync(Origin.Override);

        emitted.ShouldContain("\u001b[?2026h");
        emitted.ShouldContain("\u001b[?2026l");
        emitted.ShouldContain("\u001b[53m");
        emitted.ShouldContain("\u001b[4:3m");
        emitted.ShouldContain("\u001b[58;2;");
    }

    private static async Task<string> RenderDecoratedAsync(Origin origin)
    {
        var supported = new Feature(CapabilitySupport.Supported, origin);
        var capabilities = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.TrueColor,
            SynchronizedOutput = supported,
            Overline = supported,
            StyledUnderlines = supported,
            UnderlineColor = supported
        };
        await using MemoryStream output = new();
        await using StreamTransport transport = new(Stream.Null, output, leaveOpen: true);
        using var renderer = new Renderer();
        using var frame = new Frame(new Size(4, 1));
        _ = frame.Canvas.Draw(
            "x",
            new Point(0, 0),
            new CellStyle(
                attributes: TerminalAttributes.Overline,
                underline: Underline.Curly,
                underlineColor: Color.Rgb(1, 2, 3)));

        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalProfile.CreateAnsi(capabilities),
            TestContext.Current.CancellationToken);

        return Encoding.ASCII.GetString(output.ToArray());
    }
}
