// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Immutable concrete color values for every <see cref="ColorRole"/>.</summary>
public sealed class ThemePalette: IEquatable<ThemePalette>
{
    private readonly Dictionary<ColorRole, Color> _colors;

    /// <summary>Initializes a complete immutable palette.</summary>
    /// <param name="colors">One concrete color for each defined semantic role.</param>
    /// <exception cref="ArgumentNullException"><paramref name="colors"/> is null.</exception>
    /// <exception cref="ArgumentException">A defined role is missing.</exception>
    public ThemePalette(IReadOnlyDictionary<ColorRole, Color> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);

        var copy = new Dictionary<ColorRole, Color>();

        foreach (var role in Enum.GetValues<ColorRole>())
        {
            if (!colors.TryGetValue(role, out var color))
            {
                throw new ArgumentException($"The palette does not define {role}.", nameof(colors));
            }

            copy.Add(role, color);
        }

        _colors = copy;
    }

    /// <summary>Gets the concrete color for one defined semantic role.</summary>
    /// <param name="role">The defined role.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is unknown.</exception>
    public Color this[ColorRole role]
    {
        get
        {
            return !Enum.IsDefined(role)
                ? throw new ArgumentOutOfRangeException(nameof(role), role, "The color role is unknown.")
                : _colors[role];
        }
    }

    /// <inheritdoc/>
    public bool Equals(ThemePalette? other)
    {
        if (other is null)
        {
            return false;
        }

        foreach (var role in Enum.GetValues<ColorRole>())
        {
            if (this[role] != other[role])
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ThemePalette);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var role in Enum.GetValues<ColorRole>())
        {
            hash.Add(role);
            hash.Add(this[role]);
        }

        return hash.ToHashCode();
    }
}
