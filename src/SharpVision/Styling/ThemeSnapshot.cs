using SharpVision.Controls;

namespace SharpVision.Styling;

/// <summary>Immutable theme contents used during value resolution.</summary>
internal sealed class ThemeSnapshot
{
    internal static ThemeSnapshot Empty { get; } = new(0, []);

    private readonly Dictionary<Type, IControlStyle> _styles;

    internal ThemeSnapshot(int version, Dictionary<Type, IControlStyle> styles)
    {
        Version = version;
        _styles = styles;
    }

    internal int Version { get; }

    internal IReadOnlyList<IControlStyle> GetStyleChain(Type controlType)
    {
        var chain = new List<IControlStyle>();

        for (var current = controlType; current is not null; current = current.BaseType)
        {
            if (!typeof(Control).IsAssignableFrom(current))
            {
                break;
            }

            if (_styles.TryGetValue(current, out var style))
            {
                chain.Add(style);
            }
        }

        chain.Reverse();
        return chain;
    }
}
