// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>Verifies public terminal-description result ownership and default diagnostic behavior.</summary>
public sealed class DescriptionResultTests
{
    /// <summary>Verifies result diagnostics are copied and exposed through an immutable view.</summary>
    [Fact]
    public void ProviderFailed_WhenDiagnosticSourceChanges_RetainsImmutableSnapshot()
    {
        DescriptionDiagnostic[] source =
        [
            new(DescriptionDiagnosticCode.NativeFailure),
            new(DescriptionDiagnosticCode.CleanupFailure)
        ];
        var result = DescriptionResult.ProviderFailed(source);

        source[0] = default;
        var list = (IList<DescriptionDiagnostic>) result.Diagnostics;

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Profile.ShouldBeNull();
        result.Diagnostics.Select(static value => value.Code).ShouldBe(
            [DescriptionDiagnosticCode.NativeFailure, DescriptionDiagnosticCode.CleanupFailure]);
        _ = Should.Throw<NotSupportedException>(() =>
        {
            list[0] = new DescriptionDiagnostic(DescriptionDiagnosticCode.EnvironmentLimit);
        });
    }

    /// <summary>Verifies the public diagnostic default is a valid non-sensitive value.</summary>
    [Fact]
    public void DescriptionDiagnostic_WhenDefault_HasStablePublicValues()
    {
        var diagnostic = default(DescriptionDiagnostic);

        diagnostic.Code.ShouldBe(DescriptionDiagnosticCode.WrongType);
        diagnostic.Capability.ShouldBeNull();
    }

    /// <summary>Verifies internal result factories reject null owned values with parameter identity.</summary>
    [Fact]
    public void Factory_WhenOwnedValueIsNull_Throws()
    {
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);

        Should.Throw<ArgumentNullException>(() =>
            DescriptionResult.Loaded(null!, Array.Empty<DescriptionDiagnostic>()))
            .ParamName.ShouldBe("profile");
        Should.Throw<ArgumentNullException>(() =>
            DescriptionResult.Loaded(profile, null!))
            .ParamName.ShouldBe("diagnostics");
        Should.Throw<ArgumentNullException>(() =>
            DescriptionResult.ProviderFailed(null!))
            .ParamName.ShouldBe("diagnostics");
    }
}
