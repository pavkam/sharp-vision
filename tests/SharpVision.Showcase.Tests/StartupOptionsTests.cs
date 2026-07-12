using System.Text;

using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;

using Shouldly;

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the executable showcase explicitly requests its interactive terminal modes.</summary>
public sealed class StartupOptionsTests
{
    /// <summary>Verifies application startup emits the typed SGR any-event mouse enable sequence.</summary>
    [Fact]
    public async Task Create_WhenShowcaseStarts_EnablesSgrAnyEventMouseAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using var gallery = new Gallery();
        var options = StartupOptions.Create(new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-256color",
        });
        await using var application = new Application(gallery.Root, terminal, terminal, options);

        options.Tracking.ShouldBe(MouseTracking.Any);
        options.Coordinates.ShouldBe(MouseCoordinates.Sgr);
        options.Capabilities.CellMouse.IsSupported.ShouldBeTrue();

        await application.StartAsync(TestContext.Current.CancellationToken);

        var output = Encoding.ASCII.GetString([.. terminal.Writes.SelectMany(static value => value)]);
        output.ShouldContain("\u001b[?1003h\u001b[?1006h");

        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
