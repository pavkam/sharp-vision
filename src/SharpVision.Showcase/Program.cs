using SharpVision.Runtime;
using SharpVision.Showcase;

var status = await Application.RunConsoleAsync(
    new Gallery(),
    new ConsoleRunOptions
    {
        RedirectedMessage = StartupMessage.Get(),
    });
return status == ConsoleRunStatus.Failed ? 1 : 0;
