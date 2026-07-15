// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;




using InputAction = Terminal.Input.Action;

/// <summary>
/// Verifies Kitty CSI-u key identity, modifiers, events, text, and recovery.
/// </summary>
public sealed class KittyKeyboardTests
{
    /// <summary>
    /// Verifies alternate keys, repeat, and associated text follow official grammar.
    /// </summary>
    [Fact]
    public void Decode_WhenFullKittyEventArrives_PreservesEveryField()
    {
        var sink = Decode("\u001b[97:65:99;6:2;65:98u"u8.ToArray());

        sink.Strokes.ShouldBe(
        [
            new Stroke(
                Code.Character,
                new Rune('a'),
                97,
                Modifiers.Shift | Modifiers.Control,
                InputAction.Repeat,
                new Rune('A'),
                new Rune('c')),
        ]);
        sink.Text.Select(static value => value.Value)
            .ShouldBe([new Rune('A'), new Rune('b')]);
    }

    /// <summary>
    /// Verifies press/repeat/release and every modifier bit.
    /// </summary>
    [Theory]
    [InlineData("\u001b[97;256:1u", InputAction.Press)]
    [InlineData("\u001b[97;256:2u", InputAction.Repeat)]
    [InlineData("\u001b[97;256:3u", InputAction.Release)]
    public void Decode_WhenKittyActionVaries_MapsModifiersAndAction(
        string input,
        InputAction action)
    {
        var stroke = Decode(Encoding.UTF8.GetBytes(input)).Strokes.Single();

        stroke.Action.ShouldBe(action);
        stroke.Modifiers.ShouldBe(
            Modifiers.Shift |
            Modifiers.Alt |
            Modifiers.Control |
            Modifiers.Super |
            Modifiers.Hyper |
            Modifiers.Meta |
            Modifiers.CapsLock |
            Modifiers.NumLock);
    }

    /// <summary>
    /// Verifies canonical control and known functional key codes map logically.
    /// </summary>
    [Theory]
    [InlineData(27, Code.Escape)]
    [InlineData(13, Code.Enter)]
    [InlineData(9, Code.Tab)]
    [InlineData(127, Code.Backspace)]
    [InlineData(57358, Code.CapsLock)]
    [InlineData(57376, Code.F13)]
    public void Decode_WhenKittyCodeIsKnown_MapsLogicalCode(int native, Code code)
    {
        var sink = Decode(Encoding.ASCII.GetBytes($"\u001b[{native}u"));

        sink.Strokes.Single().ShouldBe(
            new Stroke(code, null, native, Modifiers.None, InputAction.Press));
    }

    /// <summary>
    /// Verifies unknown functional codes remain typed with their native number.
    /// </summary>
    [Fact]
    public void Decode_WhenKittyCodeIsUnknown_PreservesNativeCode()
    {
        var stroke = Decode("\u001b[63743u"u8.ToArray()).Strokes.Single();

        stroke.Code.ShouldBe(Code.Unknown);
        stroke.NativeCode.ShouldBe(63743);
    }

    /// <summary>
    /// Verifies pure-text events emit one unknown stroke then ordered text values.
    /// </summary>
    [Fact]
    public void Decode_WhenKittyEventIsPureText_EmitsStrokeThenText()
    {
        var sink = Decode("\u001b[0;;229:946u"u8.ToArray());

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Unknown, null, 0, Modifiers.None, InputAction.Press),
        ]);
        sink.Text.Select(static value => value.Value)
            .ShouldBe([new Rune('å'), new Rune('β')]);
    }

    /// <summary>
    /// Verifies representative Kitty events decode identically at every split.
    /// </summary>
    [Fact]
    public void Decode_WhenKittyEventIsFragmented_MapsAtEverySplit()
    {
        var bytes = "\u001b[97:65:99;6:2;65:98u"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.Count.ShouldBe(1, $"split {split}");
            sink.Text.Count.ShouldBe(2, $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies malformed scalars, fields, and controls report once then recover.
    /// </summary>
    [Theory]
    [InlineData("\u001b[97;0u")]
    [InlineData("\u001b[97;1:4u")]
    [InlineData("\u001b[1114112u")]
    [InlineData("\u001b[97;;31u")]
    [InlineData("\u001b[97;1;65;66u")]
    public void Decode_WhenKittyEventIsMalformed_ReportsAndRecovers(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input + "x");
        var sink = Decode(bytes);

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    private static RecordingInputSink Decode(byte[] bytes)
    {
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink))
        {
            decoder.Decode(bytes);
            decoder.Complete();
        }

        return sink;
    }
}
