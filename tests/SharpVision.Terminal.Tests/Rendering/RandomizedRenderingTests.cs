// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

using CapabilitySupport = Terminal.Capabilities.Support;
using Encoder = Terminal.Rendering.Encoder;
using TerminalCapabilities = Terminal.Capabilities.Capabilities;

/// <summary>
/// Verifies fixed-seed incremental/full frame equivalence across random states.
/// </summary>
public sealed class RandomizedRenderingTests
{
    private const int _seed = 0xD1FF;

    /// <summary>
    /// Verifies random semantic frame pairs converge to the same terminal model.
    /// </summary>
    [Fact]
    public void Encode_WhenFramesAreRandomized_MatchesFullRender()
    {
        Random random = new(_seed);

        for (int testCase = 0; testCase < 128; testCase++)
        {
            using Frame front = Create(random);
            using Frame back = Create(random);

            try
            {
                VirtualScreen incremental = new(back.Size);
                incremental.Apply(Encode(null, front));
                incremental.Apply(Encode(front, back));
                VirtualScreen full = new(back.Size);
                full.Apply(Encode(null, back));
                incremental.ShouldMatch(back);
                incremental.ShouldMatch(full);
            }
            catch (ShouldAssertException exception)
            {
                throw new InvalidOperationException(
                    $"Rendering seed {_seed}, case {testCase}.",
                    exception);
            }
        }
    }

    private static Frame Create(Random random)
    {
        Frame frame = new(new Size(10, 4));
        string[] values = ["a", "Z", "界", "語", "e\u0301", "👩‍💻", " "];
        string?[] links = [null, "https://one.test", "https://two.test"];

        for (int index = 0; index < 24; index++)
        {
            Point point = new(random.Next(frame.Size.Width), random.Next(frame.Size.Height));
            Attributes attributes = random.Next(8) switch
            {
                0 => Attributes.None,
                1 => Attributes.Bold,
                2 => Attributes.Italic,
                3 => Attributes.Underline | Attributes.Reverse,
                4 => Attributes.Blink,
                5 => Attributes.RapidBlink,
                6 => Attributes.Overline,
                _ => Attributes.RapidBlink | Attributes.Overline,
            };
            Underline underline = random.Next(6) == 0
                ? (Underline) random.Next((int) Underline.Straight, (int) Underline.Dashed + 1)
                : Underline.None;

            if (underline != Underline.None)
            {
                attributes &= ~Attributes.Underline;
            }

            Color foreground = random.Next(3) == 0
                ? Color.Indexed(random.Next(16))
                : Color.Default;
            Color underlineColor = underline != Underline.None && random.Next(2) == 0
                ? Color.Rgb(random.Next(256), random.Next(256), random.Next(256))
                : Color.Default;
            CellStyle style = new(
                foreground,
                attributes: attributes,
                hyperlink: links[random.Next(links.Length)],
                underline: underline,
                underlineColor: underlineColor);
            _ = frame.Canvas.Draw(
                values[random.Next(values.Length)].AsSpan(),
                point,
                style,
                (Edge) random.Next(3));
        }

        frame.SetCursor(
            new Point(random.Next(frame.Size.Width), random.Next(frame.Size.Height)),
            random.Next(2) == 0);
        return frame;
    }

    private static byte[] Encode(Frame? front, Frame back)
    {
        ArrayBufferWriter<byte> destination = new();
        _ = Encoder.Encode(
            front,
            back,
            destination,
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.TrueColor,
                StyledUnderlines = new Feature(CapabilitySupport.Supported, Origin.Override),
                UnderlineColor = new Feature(CapabilitySupport.Supported, Origin.Override),
                Overline = new Feature(CapabilitySupport.Supported, Origin.Override),
            });
        return destination.WrittenSpan.ToArray();
    }
}
