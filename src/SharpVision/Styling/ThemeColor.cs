// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>
/// Represents a concrete terminal color or a semantic color role that must be
/// resolved by an active <see cref="Theme"/> before rendering.
/// </summary>
/// <remarks>
/// The default value is the concrete terminal default color. A ThemeColor is a
/// UI value and cannot be passed to terminal cell or encoder APIs directly.
/// </remarks>
public readonly struct ThemeColor: IEquatable<ThemeColor>
{
    private readonly Color _color;
    private readonly ColorRole _role;
    private readonly bool _isRole;

    private ThemeColor(Color color)
    {
        _color = color;
        _role = default;
        _isRole = false;
    }

    private ThemeColor(ColorRole role)
    {
        _color = default;
        _role = role;
        _isRole = true;
    }

    /// <summary>Creates a concrete UI color.</summary>
    /// <param name="color">The concrete terminal color.</param>
    /// <returns>The UI color token.</returns>
    public static ThemeColor From(Color color) => new(color);

    /// <summary>Creates a semantic UI color role.</summary>
    /// <param name="role">The defined role.</param>
    /// <returns>The UI color token.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is unknown.</exception>
    public static ThemeColor From(ColorRole role)
    {
        return !Enum.IsDefined(role)
            ? throw new ArgumentOutOfRangeException(nameof(role), role, "The color role is unknown.")
            : new ThemeColor(role);
    }

    /// <summary>Converts a concrete terminal color to a UI color token.</summary>
    /// <param name="color">The concrete terminal color.</param>
    public static implicit operator ThemeColor(Color color) => From(color);

    /// <summary>Converts a semantic role to a UI color token.</summary>
    /// <param name="role">The defined semantic role.</param>
    public static implicit operator ThemeColor(ColorRole role) => From(role);

    /// <summary>Attempts to read the concrete color representation.</summary>
    /// <param name="color">The concrete color when this token is concrete.</param>
    /// <returns>Whether this token is concrete.</returns>
    public bool TryGetColor(out Color color)
    {
        color = _color;
        return !_isRole;
    }

    /// <summary>Attempts to read the semantic role representation.</summary>
    /// <param name="role">The semantic role when this token is role-backed.</param>
    /// <returns>Whether this token is role-backed.</returns>
    public bool TryGetRole(out ColorRole role)
    {
        role = _role;
        return _isRole;
    }

    /// <inheritdoc/>
    public bool Equals(ThemeColor other) =>
        _isRole == other._isRole &&
        (_isRole ? _role == other._role : _color == other._color);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ThemeColor other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _isRole
        ? HashCode.Combine(true, _role)
        : HashCode.Combine(false, _color);

    /// <summary>Compares two UI color tokens.</summary>
    public static bool operator ==(ThemeColor left, ThemeColor right) => left.Equals(right);

    /// <summary>Compares two UI color tokens.</summary>
    public static bool operator !=(ThemeColor left, ThemeColor right) => !left.Equals(right);
}
