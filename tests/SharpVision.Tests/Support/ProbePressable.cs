namespace SharpVision.Tests.Support;

using SharpVision.Controls;
using SharpVision.Input;

/// <summary>Records completed activations from the shared press behavior.</summary>
internal sealed class ProbePressable: Pressable
{
    /// <summary>Initializes an empty pressable probe.</summary>
    internal ProbePressable() : base(capacity: 0)
    {
    }

    /// <summary>Gets completed activation causes in commit order.</summary>
    internal List<ActivationCause> Activations { get; } = [];

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) => Activations.Add(cause);
}
