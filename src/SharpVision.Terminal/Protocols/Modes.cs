using System.Buffers.Text;
using System.Diagnostics;

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Encodes DEC private modes required by lifecycle, input, and output handling.
/// </summary>
/// <example>
/// <code>
/// Modes.AlternateScreen(writer, enabled: true);
/// Modes.BracketedPaste(writer, enabled: true);
/// </code>
/// </example>
public static class Modes
{
    /// <summary>Sets or resets a positive DEC private mode.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="mode">The positive private mode number.</param>
    /// <param name="enabled"><see langword="true"/> for DECSET; otherwise DECRST.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not positive.</exception>
    public static void SetPrivate(Writer writer, int mode, bool enabled)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mode);

        Span<byte> parameters = stackalloc byte[11];
        parameters[0] = (byte) '?';
        var formatted = Utf8Formatter.TryFormat(mode, parameters[1..], out var written);
        Debug.Assert(formatted, "Ten bytes must hold a positive Int32.");
        writer.Csi(parameters[..(written + 1)], [], enabled ? (byte) 'h' : (byte) 'l');
    }

    /// <summary>Shows or hides the text cursor using mode 25.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="visible">Whether the cursor is visible.</param>
    public static void CursorVisible(Writer writer, bool visible) => SetPrivate(writer, 25, visible);

    /// <summary>Enters or leaves the alternate screen using mode 1049.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="enabled">Whether the alternate screen is active.</param>
    public static void AlternateScreen(Writer writer, bool enabled) => SetPrivate(writer, 1049, enabled);

    /// <summary>Enables or disables bracketed paste using mode 2004.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="enabled">Whether bracketed paste is active.</param>
    public static void BracketedPaste(Writer writer, bool enabled) => SetPrivate(writer, 2004, enabled);

    /// <summary>Enables or disables terminal focus reporting using mode 1004.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="enabled">Whether focus reporting is active.</param>
    public static void FocusReporting(Writer writer, bool enabled) => SetPrivate(writer, 1004, enabled);

    /// <summary>Begins or ends synchronized output using mode 2026.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="enabled">Whether synchronized output is active.</param>
    public static void SynchronizedOutput(Writer writer, bool enabled) => SetPrivate(writer, 2026, enabled);

    /// <summary>Enables or disables Kitty clipboard paste events using mode 5522.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="enabled">Whether clipboard paste events are active.</param>
    public static void ClipboardPasteEvents(Writer writer, bool enabled) => SetPrivate(writer, 5522, enabled);
}
