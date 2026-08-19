// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

using Capabilities;

using Discovery.Adapters;

/// <summary>Resolves canonical terminal identity from source-specific redacted evidence only.</summary>
internal static class TerminalBackendResolver
{
    extension(TerminalProfile profile)
    {
        /// <summary>Resolves the most specific canonical backend without inspecting capability values or process state.</summary>
        /// <param name="environment">The non-null caller-supplied environment snapshot.</param>
        /// <returns>A canonical backend and immutable ordered redacted evidence snapshot.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="profile"/> or <paramref name="environment"/> is <see langword="null"/>.
        /// </exception>
        public BackendResolution Resolve(IReadOnlyDictionary<string, string?> environment)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(environment);

            var adapters = new IBackendEvidenceAdapter[]
            {
                new DescriptionBackendEvidenceAdapter(profile.Description),
                new EnvironmentBackendEvidenceAdapter(environment),
            };
            var evidence = new List<BackendEvidence>(adapters.Length);
            var selected = new BackendEvidence(TerminalBackendKind.Vt, BackendEvidenceOrigin.Description);

            foreach (var adapter in adapters)
            {
                if (!adapter.TryAdapt(out var item))
                {
                    continue;
                }

                evidence.Add(item);
                if (IsPreferred(item, selected))
                {
                    selected = item;
                }
            }

            return new BackendResolution(GetBackend(selected.Kind), evidence);
        }
    }

    [Pure]
    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static bool IsPreferred(BackendEvidence candidate, BackendEvidence current) =>
        Specificity(candidate.Kind) > Specificity(current.Kind) ||
        (Specificity(candidate.Kind) == Specificity(current.Kind) &&
         candidate.Origin == BackendEvidenceOrigin.Environment &&
         current.Origin == BackendEvidenceOrigin.Description);

    [Pure]
    private static int Specificity(TerminalBackendKind kind) => kind switch
    {
        TerminalBackendKind.Kitty => 3,
        TerminalBackendKind.Iterm2 => 2,
        TerminalBackendKind.Xterm => 1,
        TerminalBackendKind.Vt => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The terminal backend kind is unknown.")
    };

    [Pure]
    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static VtBackend GetBackend(TerminalBackendKind kind) => kind switch
    {
        TerminalBackendKind.Kitty => KittyBackend.Instance,
        TerminalBackendKind.Iterm2 => ItermBackend.Instance,
        TerminalBackendKind.Xterm => XtermBackend.Instance,
        TerminalBackendKind.Vt => VtBackend.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The terminal backend kind is unknown.")
    };
}
