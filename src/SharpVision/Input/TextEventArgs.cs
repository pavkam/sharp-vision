using TerminalText = SharpVision.Terminal.Input.Text;

namespace SharpVision.Input;

/// <summary>Provides one immutable Unicode text scalar.</summary>
public sealed class TextEventArgs: RoutedEventArgs
{
    /// <summary>Initializes routed Unicode text input.</summary>
    /// <param name="text">The decoded text scalar.</param>
    public TextEventArgs(TerminalText text) => Text = text;

    /// <summary>Gets the decoded text scalar.</summary>
    public TerminalText Text { get; }
}
