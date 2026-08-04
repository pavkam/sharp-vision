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
        var log = TestContext.Current.TestOutputHelper!;

        // 200 attempts, because the hang-up only races into view occasionally.
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var pty = Support.UnixPseudoterminal.Open();
            await pty.CloseMasterAsync();

            try
            {
                var read = await pty.Slave.ReadAsync(new byte[1], TestContext.Current.CancellationToken);

                if (read != 0)
                {
                    log.WriteLine($"PROBE attempt {attempt}: read returned {read}");
                }
            }
            catch (Exception error)
            {
                log.WriteLine(
                    $"PROBE attempt {attempt}: {error.GetType().FullName} HResult={error.HResult} " +
                    $"message='{error.Message}' inner={error.InnerException?.GetType().FullName ?? "none"}");
            }

            await pty.DisposeAsync();
        }

        log.WriteLine("PROBE complete");
    }
}
