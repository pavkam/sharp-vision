using SharpVision.Controls;

namespace SharpVision.Styling;

/// <summary>Provides immutable theme snapshots to attached controls.</summary>
internal sealed class ThemeContext
{
    private readonly ThemeSnapshot _snapshot;

    internal ThemeContext(ThemeSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    internal int Version => _snapshot.Version;

    internal IReadOnlyList<IControlStyle> GetStyleChain(Type controlType) =>
        _snapshot.GetStyleChain(controlType);

    internal static ThemeContext Create(Theme? theme) =>
        new(theme?.CreateSnapshot() ?? ThemeSnapshot.Empty);
}
