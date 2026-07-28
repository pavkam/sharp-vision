// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies source-labelled theme parsing diagnostics.</summary>
public sealed class ThemesDiagnosticsTests
{
    /// <summary>Verifies malformed JSON is wrapped as <see cref="InvalidDataException"/> naming the source.</summary>
    [Fact]
    public void Deserialize_WhenMalformedJson_Throws()
    {
        var error = Should.Throw<InvalidDataException>(() => Themes.Parse("{ not json", "broken"));

        error.Message.ShouldContain("broken");
    }
}
