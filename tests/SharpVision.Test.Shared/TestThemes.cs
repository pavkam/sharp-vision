// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Test.Shared;

/// <summary>Provides focused immutable themes for behavior tests that intentionally omit visual chrome.</summary>
public static class TestThemes
{
    /// <summary>Gets a theme whose input role has no intrinsic border.</summary>
    public static Theme BorderlessInput { get; } = ThemeCatalog.Parse(ThemeJson.Create(inputSides: "\"none\""));

    /// <summary>Gets a theme whose container role has no intrinsic border.</summary>
    public static Theme BorderlessContainer { get; } = ThemeCatalog.Parse(ThemeJson.Create(containerSides: "\"none\""));
}
