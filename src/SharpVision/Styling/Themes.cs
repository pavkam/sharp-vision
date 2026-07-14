// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Exposes frozen standard themes built from the public theme API.</summary>
public static class Themes
{
    /// <summary>Gets the frozen light standard theme.</summary>
    public static Theme White { get; } = ThemeCatalog.Default.Load("default-light");

    /// <summary>Gets the frozen dark standard theme.</summary>
    public static Theme Dark { get; } = ThemeCatalog.Default.Load("default-dark");
}
