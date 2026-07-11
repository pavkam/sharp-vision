using SharpVision.Text;

namespace SharpVision.Input;

/// <summary>Reports one committed directional TextInput selection transition.</summary>
public sealed class InputSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes immutable previous and committed selections.</summary>
    /// <param name="previous">The previous valid selection.</param>
    /// <param name="selection">The committed valid selection.</param>
    public InputSelectionChangedEventArgs(Selection previous, Selection selection)
    {
        Previous = previous;
        Selection = selection;
    }

    /// <summary>Gets the previous directional selection.</summary>
    public Selection Previous { get; }

    /// <summary>Gets the committed directional selection.</summary>
    public Selection Selection { get; }
}
