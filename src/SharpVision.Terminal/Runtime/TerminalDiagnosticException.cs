// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Reports one configured diagnostic promotion at a completed recovery boundary.</summary>
[PublicAPI]
public sealed class TerminalDiagnosticException: Exception
{
    /// <summary>Initializes one promotion without retaining untrusted terminal payload data.</summary>
    /// <param name="promotion">The single diagnostic family being promoted.</param>
    /// <param name="innerException">The optional cleanup failure that produced the diagnostic.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="promotion"/> is none, combines families, or contains an unknown bit.
    /// </exception>
    public TerminalDiagnosticException(
        DiagnosticPromotion promotion,
        Exception? innerException = null) : base(MessageFor(promotion), innerException) =>
        Promotion = promotion;

    /// <summary>Gets the single diagnostic family promoted by this exception.</summary>
    public DiagnosticPromotion Promotion { get; }

    private static string MessageFor(DiagnosticPromotion promotion)
    {
        ArgumentOutOfRangeException.ThrowIfUndefinedFlags(
            promotion,
            DiagnosticPromotion.All,
            nameof(promotion),
            "The diagnostic promotion contains unknown bits.");

        return promotion == DiagnosticPromotion.None ||
               (((uint) promotion & ((uint) promotion - 1)) != 0)
            ? throw new ArgumentOutOfRangeException(
                nameof(promotion),
                promotion,
                "A diagnostic exception must identify exactly one family.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"The {promotion} terminal diagnostic was promoted at a safe boundary.");
    }
}
