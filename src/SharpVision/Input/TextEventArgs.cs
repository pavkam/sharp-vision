using TerminalText = SharpVision.Terminal.Input.Text;

namespace SharpVision.Input;

/// <summary>Provides one immutable Unicode text scalar.</summary>
/// <param name="text">The decoded text scalar.</param>
public sealed class TextEventArgs(TerminalText text): RoutedEventArgs
{
    /// <summary>Gets the decoded text scalar.</summary>
    public TerminalText Text { get; } = text;
}
