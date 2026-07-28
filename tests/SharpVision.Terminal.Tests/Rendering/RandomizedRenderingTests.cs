// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

using Encoder = FrameEncoder;

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
        var random = new Random(_seed);

        for (var testCase = 0; testCase < 128; testCase++)
        {
            using var front = Create(random);
            using var back = Create(random);

            try
            {
                var incremental = new VirtualScreen(back.Size);
                incremental.Apply(Encode(null, front));
                incremental.Apply(Encode(front, back));
                var full = new VirtualScreen(back.Size);
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
        var frame = new Frame(new Size(10, 4));
        string[] values = ["a", "Z", "界", "語", "e\u0301", "👩‍💻", " "];
        string?[] links = [null, "https://one.test", "https://two.test"];

        for (var index = 0; index < 24; index++)
        {
            var point = new Point(random.Next(frame.Size.Width), random.Next(frame.Size.Height));
            var attributes = random.Next(8) switch
            {
                0 => Attributes.None,
                1 => Attributes.Bold,
                2 => Attributes.Italic,
                3 => Attributes.Underline | Attributes.Reverse,
                4 => Attributes.Blink,
                5 => Attributes.RapidBlink,
                6 => Attributes.Overline,
                _ => Attributes.RapidBlink | Attributes.Overline
            };
            var underline = random.Next(6) == 0
                ? (Underline) random.Next((int) Underline.Straight, (int) Underline.Dashed + 1)
                : Underline.None;

            if (underline != Underline.None)
            {
                attributes &= ~Attributes.Underline;
            }

            var foreground = random.Next(3) == 0
                ? ReferenceColors.Get(random.Next(16))
                : Color.Default;
            var underlineColor = underline != Underline.None && random.Next(2) == 0
                ? Color.Rgb(random.Next(256), random.Next(256), random.Next(256))
                : Color.Default;
            var style = new CellStyle(
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
        var destination = new ArrayBufferWriter<byte>();
        _ = Encoder.Encode(
            front,
            back,
            destination,
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.TrueColor,
                StyledUnderlines = new Feature(CapabilitySupport.Supported, Origin.Override),
                UnderlineColor = new Feature(CapabilitySupport.Supported, Origin.Override),
                Overline = new Feature(CapabilitySupport.Supported, Origin.Override)
            });
        return destination.WrittenSpan.ToArray();
    }
}
