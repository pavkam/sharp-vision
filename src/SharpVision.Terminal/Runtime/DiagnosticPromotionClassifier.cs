// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Promotes terminal-runtime diagnostic families at safe boundaries.</summary>
internal static class DiagnosticPromotionClassifier
{
    /// <summary>Throws when one classified occurrence belongs to a configured family.</summary>
    internal static void ThrowIfConfigured(
        DiagnosticPromotion configured,
        DiagnosticPromotion occurrence,
        Exception? innerException = null)
    {
        Debug.Assert(occurrence != DiagnosticPromotion.None, "A classified occurrence names one family.");

        if ((configured & occurrence) != 0)
        {
            throw new TerminalDiagnosticException(occurrence, innerException);
        }
    }
}
