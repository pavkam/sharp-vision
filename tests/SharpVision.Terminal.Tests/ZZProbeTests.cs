// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests;

/// <summary>Temporary probe for the Linux pseudoterminal hang-up errno (#295).</summary>
public sealed class ZZProbeTests
{
    /// <summary>Records what a read observes after the pseudoterminal master goes away.</summary>
    [Fact]
    public async Task ProbeAsync()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "unix");
        var outcomes = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var attempt = 0; attempt < 400; attempt++)
        {
            var pty = Support.UnixPseudoterminal.Open();
            await pty.CloseMasterAsync();
            string key;

            try
            {
                var read = await pty.Slave.ReadAsync(new byte[1], TestContext.Current.CancellationToken);
                key = $"read={read}";
            }
            catch (Exception error)
            {
                key = $"{error.GetType().FullName} HResult={error.HResult} message='{error.Message}'";
            }

            outcomes[key] = outcomes.TryGetValue(key, out var count) ? count + 1 : 1;
            await pty.DisposeAsync();
        }

        // Deliberate failure: xunit only surfaces output for failing tests, and this probe exists
        // solely to publish the distribution into the CI log.
        throw new InvalidOperationException(
            "PROBE RESULTS :: " + string.Join(" | ", outcomes.Select(pair => $"{pair.Key} x{pair.Value}")));
    }
}
