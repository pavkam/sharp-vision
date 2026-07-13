// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Terminal.Protocols;

/// <summary>Immutable theme contents used during value resolution.</summary>
internal sealed class ThemeSnapshot
{
    internal static ThemeSnapshot Empty { get; } = new(0, [], []);

    private readonly Dictionary<Type, IControlStyle> _styles;
    private readonly Dictionary<ColorRole, Color> _colors;
    private readonly Dictionary<Type, IReadOnlyList<IControlStyle>> _styleChains = [];

    internal ThemeSnapshot(
        int version,
        Dictionary<Type, IControlStyle> styles,
        Dictionary<ColorRole, Color> colors)
    {
        Version = version;
        _styles = styles;
        _colors = colors;
    }

    internal int Version { get; }

    internal bool TryGetColor(ColorRole role, out Color color) => _colors.TryGetValue(role, out color);

    internal IReadOnlyList<IControlStyle> GetStyleChain(Type controlType)
    {
        if (_styleChains.TryGetValue(controlType, out IReadOnlyList<IControlStyle>? cached))
        {
            return cached;
        }

        List<IControlStyle> chain = [];

        foreach (Type type in ControlHierarchy.BaseToDerived(controlType))
        {
            if (_styles.TryGetValue(type, out IControlStyle? style))
            {
                chain.Add(style);
            }
        }

        _styleChains[controlType] = chain;
        return chain;
    }
}
