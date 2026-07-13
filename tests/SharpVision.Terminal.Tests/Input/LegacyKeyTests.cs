// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;


using InputAction = Terminal.Input.Action;

/// <summary>
/// Verifies legacy VT key mappings, modifiers, fragmentation, and recovery.
/// </summary>
public sealed class LegacyKeyTests
{
    /// <summary>
    /// Verifies C0 and DEL keyboard bytes map to named keys.
    /// </summary>
    [Theory]
    [InlineData("\r", Code.Enter)]
    [InlineData("\n", Code.Enter)]
    [InlineData("\t", Code.Tab)]
    [InlineData("\b", Code.Backspace)]
    [InlineData("\u007f", Code.Backspace)]
    public void Decode_WhenByteIsNamedControl_EmitsNamedStroke(string input, Code code)
    {
        RecordingInputSink sink = Decode(Encoding.UTF8.GetBytes(input));

        sink.Strokes.ShouldBe(
        [
            new Stroke(code, null, 0, Modifiers.None, InputAction.Press),
        ]);
    }

    /// <summary>
    /// Verifies representative CSI, tilde, Shift-Tab, and SS3 keys at every split.
    /// </summary>
    [Theory]
    [InlineData("\u001b[A", Code.Up, Modifiers.None, 0)]
    [InlineData("\u001b[1;2A", Code.Up, Modifiers.Shift, 0)]
    [InlineData("\u001b[1;3D", Code.Left, Modifiers.Alt, 0)]
    [InlineData("\u001b[1;5C", Code.Right, Modifiers.Control, 0)]
    [InlineData("\u001b[1;6H", Code.Home, Modifiers.Shift | Modifiers.Control, 0)]
    [InlineData("\u001b[F", Code.End, Modifiers.None, 0)]
    [InlineData("\u001b[2~", Code.Insert, Modifiers.None, 2)]
    [InlineData("\u001b[3~", Code.Delete, Modifiers.None, 3)]
    [InlineData("\u001b[5~", Code.PageUp, Modifiers.None, 5)]
    [InlineData("\u001b[6~", Code.PageDown, Modifiers.None, 6)]
    [InlineData("\u001b[15;2~", Code.F5, Modifiers.Shift, 15)]
    [InlineData("\u001b[Z", Code.Tab, Modifiers.Shift, 0)]
    [InlineData("\u001bOP", Code.F1, Modifiers.None, 0)]
    [InlineData("\u001bOQ", Code.F2, Modifiers.None, 0)]
    [InlineData("\u001bOR", Code.F3, Modifiers.None, 0)]
    [InlineData("\u001bOS", Code.F4, Modifiers.None, 0)]
    public void Decode_WhenLegacyKeyIsFragmented_MapsAtEverySplit(
        string input,
        Code code,
        Modifiers modifiers,
        int nativeCode)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);

        for (int split = 0; split <= bytes.Length; split++)
        {
            RecordingInputSink sink = new();
            using Decoder decoder = new(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Strokes.ShouldBe(
            [
                new Stroke(code, null, nativeCode, modifiers, InputAction.Press),
            ], $"split {split}");
            sink.Text.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>
    /// Verifies the complete VT function-key range maps to stable logical codes.
    /// </summary>
    [Theory]
    [InlineData(11, Code.F1)]
    [InlineData(12, Code.F2)]
    [InlineData(13, Code.F3)]
    [InlineData(14, Code.F4)]
    [InlineData(15, Code.F5)]
    [InlineData(17, Code.F6)]
    [InlineData(18, Code.F7)]
    [InlineData(19, Code.F8)]
    [InlineData(20, Code.F9)]
    [InlineData(21, Code.F10)]
    [InlineData(23, Code.F11)]
    [InlineData(24, Code.F12)]
    public void Decode_WhenTildeFunctionKeyIsKnown_MapsLogicalCode(int native, Code code)
    {
        RecordingInputSink sink = Decode(Encoding.UTF8.GetBytes($"\u001b[{native}~"));

        sink.Strokes.ShouldBe(
        [
            new Stroke(code, null, native, Modifiers.None, InputAction.Press),
        ]);
    }

    /// <summary>
    /// Verifies unknown valid CSI keys remain typed and adjacent input survives.
    /// </summary>
    [Fact]
    public void Decode_WhenCsiKeyIsUnknown_EmitsUnknownAndRecovers()
    {
        RecordingInputSink sink = Decode("\u001b[99~x"u8.ToArray());

        sink.Strokes[0].ShouldBe(
            new Stroke(Code.Unknown, null, 99, Modifiers.None, InputAction.Press));
        sink.Strokes[1].Code.ShouldBe(Code.Character);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>
    /// Verifies malformed key parameters report once and preserve the next key.
    /// </summary>
    [Fact]
    public void Decode_WhenCsiParametersAreMalformed_ReportsAndRecovers()
    {
        RecordingInputSink sink = Decode("\u001b[1:x\u001b[B"u8.ToArray());

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Strokes.ShouldContain(static item => item.Code == Code.Down);
    }

    /// <summary>
    /// Verifies adjacent escape sequences produce distinct ordered strokes.
    /// </summary>
    [Fact]
    public void Decode_WhenKeysAreAdjacent_PreservesOrder()
    {
        RecordingInputSink sink = Decode("\u001b[A\u001b[B\u001b[C\u001b[D"u8.ToArray());

        sink.Strokes.Select(static item => item.Code)
            .ShouldBe([Code.Up, Code.Down, Code.Right, Code.Left]);
    }

    /// <summary>
    /// Verifies an interrupted SS3 prefix reports once and cannot consume later text.
    /// </summary>
    [Fact]
    public void Decode_WhenSs3IsInterrupted_ReportsAndRecovers()
    {
        RecordingInputSink sink = Decode("\u001bO\u001b[Ax"u8.ToArray());

        sink.Diagnostics.Count.ShouldBe(1);
        sink.Strokes.Select(static item => item.Code)
            .ShouldBe([Code.Up, Code.Character]);
        sink.Text.Single().Value.ShouldBe(new Rune('x'));
    }

    private static RecordingInputSink Decode(byte[] input)
    {
        RecordingInputSink sink = new();

        using (Decoder decoder = new(sink))
        {
            decoder.Decode(input);
            decoder.Complete();
        }

        return sink;
    }
}
