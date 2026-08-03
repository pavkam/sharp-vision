// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies public Theme construction preserves valid immutable metadata.</summary>
public sealed class ThemeTests
{
    /// <summary>Verifies an undefined color-scheme value is rejected before publication.</summary>
    [Fact]
    public void Constructor_WhenColorSchemeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Theme(colorScheme: (ColorScheme) int.MaxValue));
    }

    /// <summary>Verifies required identity and provenance metadata cannot be null or blank.</summary>
    [Fact]
    public void Constructor_WhenMetadataIsInvalid_ThrowsBeforePublishingTheme()
    {
        _ = Should.Throw<ArgumentNullException>(() => new Theme(name: null!));
        _ = Should.Throw<ArgumentNullException>(() => new Theme(slug: null!));
        _ = Should.Throw<ArgumentNullException>(() => new Theme(author: null!));
        _ = Should.Throw<ArgumentNullException>(() => new Theme(license: null!));
        _ = Should.Throw<ArgumentNullException>(() => new Theme(source: null!));

        _ = Should.Throw<ArgumentException>(() => new Theme(name: " \t"));
        _ = Should.Throw<ArgumentException>(() => new Theme(slug: " \t"));
        _ = Should.Throw<ArgumentException>(() => new Theme(author: " \t"));
        _ = Should.Throw<ArgumentException>(() => new Theme(license: " \t"));
        _ = Should.Throw<ArgumentException>(() => new Theme(source: " \t"));
    }
}
