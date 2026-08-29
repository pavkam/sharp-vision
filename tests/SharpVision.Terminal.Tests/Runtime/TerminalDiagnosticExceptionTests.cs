// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Verifies diagnostic exceptions identify exactly one redacted promotion family.</summary>
public sealed class TerminalDiagnosticExceptionTests
{
    /// <summary>Verifies none, combined families, and unknown bits are rejected.</summary>
    /// <param name="promotion">The invalid family selection.</param>
    [Theory]
    [InlineData(DiagnosticPromotion.None)]
    [InlineData(DiagnosticPromotion.MalformedInput | DiagnosticPromotion.Fallback)]
    [InlineData((DiagnosticPromotion) 32)]
    public void Constructor_WhenPromotionIsNotOneDefinedFamily_ThrowsArgumentOutOfRangeException(
        DiagnosticPromotion promotion)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new TerminalDiagnosticException(promotion));

        exception.ParamName.ShouldBe(nameof(promotion));
    }
}
