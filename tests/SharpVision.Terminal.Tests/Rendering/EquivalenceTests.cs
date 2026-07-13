// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

using CapabilitySupport = Terminal.Capabilities.Support;
using Encoder = Terminal.Rendering.Encoder;
using TerminalCapabilities = Terminal.Capabilities.Capabilities;

/// <summary>
/// Verifies incremental output reaches the same semantic terminal state as full output.
/// </summary>
public sealed class EquivalenceTests
{
    /// <summary>
    /// Verifies targeted multi-frame transitions against an independent terminal model.
    /// </summary>
    /// <param name="frontText">The committed front-frame text.</param>
    /// <param name="backText">The target back-frame text.</param>
    [Theory]
    [InlineData("abcd", "abXd")]
    [InlineData("界x", "abx")]
    [InlineData("abx", "界x")]
    [InlineData("e\u0301x", "éx")]
    public void Encode_WhenFrameTransitions_AgreesWithFullRender(
        string frontText,
        string backText)
    {
        using Frame front = Create(frontText);
        using Frame back = Create(backText);
        VirtualScreen incremental = new(back.Size);
        incremental.Apply(Encode(null, front));
        incremental.Apply(Encode(front, back));
        VirtualScreen full = new(back.Size);
        full.Apply(Encode(null, back));

        incremental.ShouldMatch(back);
        full.ShouldMatch(back);
        incremental.ShouldMatch(full);
    }

    /// <summary>
    /// Verifies style, hyperlink, and cursor transitions are semantically equivalent.
    /// </summary>
    [Fact]
    public void Encode_WhenStyleAndCursorChange_AgreesWithFullRender()
    {
        using Frame front = new(new Size(2, 1));
        using Frame back = new(new Size(2, 1));
        _ = front.Canvas.Draw("ab".AsSpan(), new Point(0, 0));
        _ = back.Canvas.Draw(
            "ab".AsSpan(),
            new Point(0, 0),
            new CellStyle(attributes: Attributes.Bold, hyperlink: "https://example.test"));
        back.SetCursor(new Point(1, 0), visible: true);
        VirtualScreen incremental = new(back.Size);
        incremental.Apply(Encode(null, front));
        incremental.Apply(Encode(front, back));
        VirtualScreen full = new(back.Size);
        full.Apply(Encode(null, back));

        incremental.ShouldMatch(back);
        incremental.ShouldMatch(full);
    }

    /// <summary>
    /// Verifies modern decorations survive supported output and independent parsing.
    /// </summary>
    [Fact]
    public void Encode_WhenModernDecorationsAreSupported_AgreesWithFullRender()
    {
        using Frame frame = new(new Size(2, 1));
        CellStyle style = new(
            attributes: Attributes.RapidBlink | Attributes.Overline,
            underline: Underline.Curly,
            underlineColor: Color.Rgb(12, 34, 56));
        _ = frame.Canvas.Draw("ab".AsSpan(), new Point(0, 0), style);
        VirtualScreen screen = new(frame.Size);

        screen.Apply(Encode(null, frame, ModernDecorationCapabilities));

        screen.ShouldMatch(frame);
    }

    private static TerminalCapabilities ModernDecorationCapabilities =>
        TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.TrueColor,
            StyledUnderlines = new Feature(CapabilitySupport.Supported, Origin.Override),
            UnderlineColor = new Feature(CapabilitySupport.Supported, Origin.Override),
            Overline = new Feature(CapabilitySupport.Supported, Origin.Override),
        };

    private static byte[] Encode(
        Frame? front,
        Frame back,
        TerminalCapabilities? capabilities = null)
    {
        ArrayBufferWriter<byte> destination = new();
        _ = Encoder.Encode(
            front,
            back,
            destination,
            capabilities ?? TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
        return destination.WrittenSpan.ToArray();
    }

    private static Frame Create(string value)
    {
        Frame frame = new(new Size(3, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }
}
