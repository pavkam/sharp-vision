// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies typed DEC private mode encoding.
/// </summary>
public sealed class ModesTests
{
    /// <summary>
    /// Verifies lifecycle and input modes required by the runtime.
    /// </summary>
    [Fact]
    public void Mode_WhenToggled_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        ProtocolModes.CursorVisible(writer, true);
        ProtocolModes.CursorVisible(writer, false);
        ProtocolModes.AlternateScreen(writer, true);
        ProtocolModes.BracketedPaste(writer, true);
        ProtocolModes.FocusReporting(writer, true);
        ProtocolModes.SynchronizedOutput(writer, true);
        ProtocolModes.ClipboardPasteEvents(writer, true);
        ProtocolModes.ClipboardPasteEvents(writer, false);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes(
                "\u001b[?25h\u001b[?25l\u001b[?1049h\u001b[?2004h" +
                "\u001b[?1004h\u001b[?2026h\u001b[?5522h\u001b[?5522l"));
    }

    /// <summary>Verifies each typed helper encodes exactly the DecPrivateMode constant carrying
    /// its name, so the encode side and the discovery query side (which also reads
    /// DecPrivateMode) cannot silently retype a different number for the same mode (see #93).</summary>
    [Fact]
    public void Mode_WhenToggled_EncodesTheNamedDecPrivateModeConstant()
    {
        AssertEncodesMode(DecPrivateMode.CursorVisible, static (writer, enabled) => ProtocolModes.CursorVisible(writer, enabled));
        AssertEncodesMode(DecPrivateMode.AlternateScreen, static (writer, enabled) => ProtocolModes.AlternateScreen(writer, enabled));
        AssertEncodesMode(DecPrivateMode.BracketedPaste, static (writer, enabled) => ProtocolModes.BracketedPaste(writer, enabled));
        AssertEncodesMode(DecPrivateMode.FocusReporting, static (writer, enabled) => ProtocolModes.FocusReporting(writer, enabled));
        AssertEncodesMode(DecPrivateMode.SynchronizedOutput, static (writer, enabled) => ProtocolModes.SynchronizedOutput(writer, enabled));
        AssertEncodesMode(DecPrivateMode.ClipboardPasteEvents, static (writer, enabled) => ProtocolModes.ClipboardPasteEvents(writer, enabled));

        static void AssertEncodesMode(int mode, Action<ProtocolWriter, bool> encode)
        {
            var destination = new ArrayBufferWriter<byte>();
            encode(new ProtocolWriter(destination), true);

            var expected = new ArrayBufferWriter<byte>();
            ProtocolModes.SetPrivate(new ProtocolWriter(expected), mode, true);

            destination.WrittenSpan.ToArray().ShouldBe(expected.WrittenSpan.ToArray());
        }
    }

    /// <summary>Verifies the shared constants match their documented DEC mode numbers, so the
    /// discovery query side and this encode side cannot silently drift.</summary>
    [Fact]
    public void DecPrivateMode_WhenRead_MatchesDocumentedModeNumbers()
    {
        DecPrivateMode.CursorVisible.ShouldBe(25);
        DecPrivateMode.FocusReporting.ShouldBe(1004);
        DecPrivateMode.CellMouse.ShouldBe(1006);
        DecPrivateMode.PixelMouse.ShouldBe(1016);
        DecPrivateMode.AlternateScreen.ShouldBe(1049);
        DecPrivateMode.BracketedPaste.ShouldBe(2004);
        DecPrivateMode.SynchronizedOutput.ShouldBe(2026);
        DecPrivateMode.ClipboardPasteEvents.ShouldBe(5522);
    }

    /// <summary>
    /// Verifies raw private mode validation.
    /// </summary>
    [Fact]
    public void SetPrivate_WhenModeIsNotPositive_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            ProtocolModes.SetPrivate(new ProtocolWriter(destination), 0, enabled: true));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies every xterm tracking and coordinate mode uses exact private modes.
    /// </summary>
    [Theory]
    [InlineData(MouseTracking.X10, 9)]
    [InlineData(MouseTracking.Press, 1000)]
    [InlineData(MouseTracking.Drag, 1002)]
    [InlineData(MouseTracking.Any, 1003)]
    public void Mouse_WhenTrackingVaries_WritesExactMode(MouseTracking tracking, int mode)
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        ProtocolModes.Mouse(writer, tracking, MouseCoordinates.Sgr, enabled: true);
        ProtocolModes.Mouse(writer, tracking, MouseCoordinates.Sgr, enabled: false);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes(
                $"\u001b[?{mode}h\u001b[?1006h\u001b[?1006l\u001b[?{mode}l"));
    }

    /// <summary>
    /// Verifies every extended coordinate encoding maps to its documented mode.
    /// </summary>
    [Theory]
    [InlineData(MouseCoordinates.Default, 0)]
    [InlineData(MouseCoordinates.Utf8, 1005)]
    [InlineData(MouseCoordinates.Sgr, 1006)]
    [InlineData(MouseCoordinates.Urxvt, 1015)]
    [InlineData(MouseCoordinates.Pixel, 1016)]
    public void Mouse_WhenCoordinatesVary_WritesExactMode(
        MouseCoordinates coordinates,
        int mode)
    {
        var destination = new ArrayBufferWriter<byte>();

        ProtocolModes.Mouse(new ProtocolWriter(destination), MouseTracking.Press, coordinates, enabled: true);

        var suffix = mode == 0 ? string.Empty : $"\u001b[?{mode}h";
        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes($"\u001b[?1000h{suffix}"));
    }

    /// <summary>
    /// Verifies invalid mouse enum values never partially mutate terminal modes.
    /// </summary>
    [Fact]
    public void Mouse_WhenValueIsInvalid_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            ProtocolModes.Mouse(writer, 0, MouseCoordinates.Sgr, enabled: true));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            ProtocolModes.Mouse(writer, MouseTracking.Press, (MouseCoordinates) int.MaxValue, enabled: true));

        destination.WrittenCount.ShouldBe(0);
    }
}
