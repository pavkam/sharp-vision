// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Compatibility.Tests;

/// <summary>
/// Guards the complete public surfaces of the published SharpVision assemblies against
/// unintentional change.
/// </summary>
/// <remarks>
/// This is not a per-version frozen baseline: the single snapshot per assembly under
/// <c>Snapshots/</c> always reflects the current, approved shape of the public API. A failing
/// diff here is the maintainer's own signal for whether the change is a documented breaking
/// change that warrants bumping that assembly's own version property in
/// <c>Directory.Build.props</c> - not an automated release gate keyed to a shared version.
/// SharpVision.Terminal, SharpVision, SharpVision.Document, and SharpVision.FigletFonts each version and publish
/// independently, so a signature change in one never requires bumping the others. Accepting the
/// new snapshot is a deliberate, reviewed decision, not a mechanical bookkeeping step.
///
/// Attributes are excluded from the compared text: they are metadata annotations rather than
/// binary-breaking surface, so adding, removing, or editing one here never by itself indicates a
/// meaningful API change worth flagging.
/// </remarks>
public sealed class PublicApiCompatibilityTests
{
    /// <summary>
    /// Verifies the terminal library against the approved public API baseline.
    /// </summary>
    [Fact]
    public Task SharpVisionTerminal_WhenComparedWithApprovedBaseline_MatchesApprovedPublicApi()
    {
        var assembly = typeof(Terminal.Geometry.Point).Assembly;

        return VerifyPublicApiAsync(assembly, "SharpVision.Terminal");
    }

    /// <summary>
    /// Verifies the UI library against the approved public API baseline.
    /// </summary>
    [Fact]
    public Task SharpVision_WhenComparedWithApprovedBaseline_MatchesApprovedPublicApi()
    {
        var assembly = typeof(Controls.ControlBase).Assembly;

        return VerifyPublicApiAsync(assembly, "SharpVision");
    }

    /// <summary>
    /// Verifies the optional rich-document library against the approved public API baseline.
    /// </summary>
    [Fact]
    public Task SharpVisionDocument_WhenComparedWithApprovedBaseline_MatchesApprovedPublicApi()
    {
        var assembly = typeof(Controls.Documents.Document).Assembly;

        return VerifyPublicApiAsync(assembly, "SharpVision.Document");
    }

    /// <summary>
    /// Verifies the optional FIGlet font library against the approved public API baseline.
    /// </summary>
    [Fact]
    public Task SharpVisionFigletFonts_WhenComparedWithApprovedBaseline_MatchesApprovedPublicApi()
    {
        var assembly = typeof(Fonts.FigletCatalog).Assembly;

        return VerifyPublicApiAsync(assembly, "SharpVision.FigletFonts");
    }

    /// <summary>
    /// Verifies the optional syntax-highlighting library against the approved public API baseline.
    /// </summary>
    [Fact]
    public Task SharpVisionSyntaxHighlighting_WhenComparedWithApprovedBaseline_MatchesApprovedPublicApi()
    {
        var assembly = typeof(SyntaxHighlighting.SyntaxDefinitionCatalog).Assembly;

        return VerifyPublicApiAsync(assembly, "SharpVision.SyntaxHighlighting");
    }

    private static Task VerifyPublicApiAsync(Assembly assembly, string assemblyName)
    {
        var publicApi = assembly.GeneratePublicApi();

        assembly.GetName().Name.ShouldBe(assemblyName);
        publicApi.ShouldNotBeNullOrWhiteSpace();

        var withoutAttributes = RemoveAttributeLines(publicApi);

        var settings = new VerifySettings();
        settings.UseDirectory("Snapshots");
        settings.UseFileName(assemblyName);

        return Verify(withoutAttributes, settings);
    }

    /// <summary>
    /// Removes every standalone attribute-application line from generated public API text.
    /// </summary>
    /// <param name="publicApi">The complete generated public API text.</param>
    /// <returns>The same text with every attribute-only line removed.</returns>
    /// <remarks>
    /// <c>PublicApiGenerator</c> always renders one complete attribute application per line,
    /// immediately preceding the type or member it decorates, never sharing a line with another
    /// attribute or with the declaration itself. Matching on the trimmed line's own enclosing
    /// brackets is therefore exact regardless of the attribute's namespace or constructor
    /// arguments, including ones that themselves contain nested brackets or quoted text.
    /// </remarks>
    private static string RemoveAttributeLines(string publicApi)
    {
        var lines = publicApi.Split(["\r\n", "\n"], StringSplitOptions.None);
        var kept = lines.Where(line => !IsAttributeLine(line));

        return string.Join('\n', kept);
    }

    private static bool IsAttributeLine(string line)
    {
        var trimmed = line.Trim();

        return trimmed.StartsWith('[') && trimmed.EndsWith(']');
    }
}
