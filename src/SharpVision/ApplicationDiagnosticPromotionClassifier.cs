// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using Terminal.Runtime;

/// <summary>Maps application-visible subsystem diagnostics to configured promotion families.</summary>
internal static class ApplicationDiagnosticPromotionClassifier
{
    /// <summary>Classifies one protocol diagnostic after its parser recovery has completed.</summary>
    internal static DiagnosticPromotion Classify(DiagnosticCode code) => code switch
    {
        DiagnosticCode.Malformed or
        DiagnosticCode.Cancelled or
        DiagnosticCode.Truncated or
        DiagnosticCode.ParameterLimit or
        DiagnosticCode.IntermediateLimit or
        DiagnosticCode.StringLimit or
        DiagnosticCode.InvalidBase64 or
        DiagnosticCode.InvalidMetadata => DiagnosticPromotion.MalformedInput,
        DiagnosticCode.UnexpectedPacket or
        DiagnosticCode.DuplicateResponse or
        DiagnosticCode.LateResponse => DiagnosticPromotion.InconsistentReply,
        DiagnosticCode.QueryLimit or
        DiagnosticCode.Unsupported => DiagnosticPromotion.UnsupportedFeature,
        DiagnosticCode.Fallback => DiagnosticPromotion.Fallback,
        _ => throw new UnreachableException()
    };

    /// <summary>Classifies one description diagnostic after profile resolution has completed.</summary>
    internal static DiagnosticPromotion Classify(DescriptionDiagnosticCode code) => code switch
    {
        DescriptionDiagnosticCode.CleanupFailure => DiagnosticPromotion.CleanupFailure,
        DescriptionDiagnosticCode.AnsiFallback => DiagnosticPromotion.Fallback,
        DescriptionDiagnosticCode.WrongType or
        DescriptionDiagnosticCode.InvalidProgram or
        DescriptionDiagnosticCode.TermcapLimit or
        DescriptionDiagnosticCode.EnvironmentLimit or
        DescriptionDiagnosticCode.DescriptionLimit or
        DescriptionDiagnosticCode.InvalidKey => DiagnosticPromotion.MalformedInput,
        DescriptionDiagnosticCode.ConflictingKey => DiagnosticPromotion.InconsistentReply,
        DescriptionDiagnosticCode.UnsupportedPadding or
        DescriptionDiagnosticCode.MissingRequired or
        DescriptionDiagnosticCode.NativeFailure or
        DescriptionDiagnosticCode.MissingOrGeneric => DiagnosticPromotion.UnsupportedFeature,
        _ => throw new UnreachableException()
    };

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
