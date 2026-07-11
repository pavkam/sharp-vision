using System.Buffers;

using SharpVision.Terminal.Protocols;

using Shouldly;

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
            System.Text.Encoding.ASCII.GetBytes(
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
}
