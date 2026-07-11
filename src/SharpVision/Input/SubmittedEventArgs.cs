namespace SharpVision.Input;

/// <summary>Reports a single-line TextInput submission.</summary>
public sealed class SubmittedEventArgs: EventArgs
{
    /// <summary>Initializes a submission with the committed text.</summary>
    /// <param name="text">The non-null committed text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public SubmittedEventArgs(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets the committed submitted text.</summary>
    public string Text { get; }
}
