using SharpVision.Controls;

namespace SharpVision.Styling;

/// <summary>Immutable theme contents used during value resolution.</summary>
internal sealed class ThemeSnapshot
{
    internal static ThemeSnapshot Empty { get; } = new(0, []);

    private readonly Dictionary<Type, IControlStyle> _styles;
    private readonly Dictionary<Type, IReadOnlyList<IControlStyle>> _styleChains = [];

    internal ThemeSnapshot(int version, Dictionary<Type, IControlStyle> styles)
    {
        Version = version;
        _styles = styles;
    }

    internal int Version { get; }

    internal IReadOnlyList<IControlStyle> GetStyleChain(Type controlType)
    {
        if (_styleChains.TryGetValue(controlType, out var cached))
        {
            return cached;
        }

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
        _styleChains[controlType] = chain;
        return chain;
    }
}
