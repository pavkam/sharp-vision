using SharpVision.Text;

namespace SharpVision.Input;

/// <summary>Reports one committed directional TextInput selection transition.</summary>
/// <remarks>Initializes immutable previous and committed selections.</remarks>
/// <param name="previous">The previous valid selection.</param>
/// <param name="selection">The committed valid selection.</param>
public sealed class InputSelectionChangedEventArgs(Selection previous, Selection selection): EventArgs
{

    /// <summary>Gets the previous directional selection.</summary>
    public Selection Previous { get; } = previous;

    /// <summary>Gets the committed directional selection.</summary>
    public Selection Selection { get; } = selection;
}
