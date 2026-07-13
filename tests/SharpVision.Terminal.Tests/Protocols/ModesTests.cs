namespace SharpVision.Terminal.Tests.Protocols;

using System.Buffers;

using SharpVision.Terminal.Protocols;

using Shouldly;

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
        var writer = new Writer(destination);

        Modes.CursorVisible(writer, true);
        Modes.CursorVisible(writer, false);
        Modes.AlternateScreen(writer, true);
        Modes.BracketedPaste(writer, true);
        Modes.FocusReporting(writer, true);
        Modes.SynchronizedOutput(writer, true);
        Modes.ClipboardPasteEvents(writer, true);
        Modes.ClipboardPasteEvents(writer, false);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes(
                "\u001b[?25h\u001b[?25l\u001b[?1049h\u001b[?2004h" +
                "\u001b[?1004h\u001b[?2026h\u001b[?5522h\u001b[?5522l"));
    }

    /// <summary>
    /// Verifies raw private mode validation.
    /// </summary>
    [Fact]
    public void SetPrivate_WhenModeIsNotPositive_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Modes.SetPrivate(new Writer(destination), 0, enabled: true));

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
        var writer = new Writer(destination);

        Modes.Mouse(writer, tracking, MouseCoordinates.Sgr, enabled: true);
        Modes.Mouse(writer, tracking, MouseCoordinates.Sgr, enabled: false);

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

        Modes.Mouse(new Writer(destination), MouseTracking.Press, coordinates, enabled: true);

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
        var writer = new Writer(destination);

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Modes.Mouse(writer, 0, MouseCoordinates.Sgr, enabled: true));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Modes.Mouse(writer, MouseTracking.Press, (MouseCoordinates) int.MaxValue, enabled: true));

        destination.WrittenCount.ShouldBe(0);
    }
}
