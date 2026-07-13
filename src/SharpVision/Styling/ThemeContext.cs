// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Terminal.Protocols;

/// <summary>Provides immutable theme snapshots to attached controls.</summary>
internal sealed class ThemeContext
{
    private readonly ThemeSnapshot _snapshot;

    internal ThemeContext(ThemeSnapshot snapshot) => _snapshot = snapshot;

    internal int Version => _snapshot.Version;

    internal IReadOnlyList<IControlStyle> GetStyleChain(Type controlType) =>
        _snapshot.GetStyleChain(controlType);

    internal bool TryGetColor(ColorRole role, out Color color) => _snapshot.TryGetColor(role, out color);

    internal static ThemeContext Create(Theme? theme) =>
        new(theme?.CreateSnapshot() ?? ThemeSnapshot.Empty);
}
