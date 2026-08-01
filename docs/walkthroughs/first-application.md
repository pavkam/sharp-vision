# Build your first application

This walkthrough creates a .NET 10 console application with one retained screen,
some text, and a button. SharpVision owns the terminal session and restores the
terminal modes when the application exits.

## Create the project

```bash
git clone https://github.com/pavkam/sharp-vision.git sharp-vision
dotnet new console --framework net10.0 --name HelloSharpVision
cd HelloSharpVision
dotnet add reference ../sharp-vision/src/SharpVision/SharpVision.csproj
```

> [!IMPORTANT] The published UI package currently cannot resolve its unpublished
> `SharpVision.Terminal` dependency. Until the terminal package exists, the
> project reference above is the supported path.

## Add the screen

Replace `Program.cs` with:

```csharp
using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision;
using SharpVision.Styling;

var status = await ConsoleApplication.RunAsync(new HelloScreen());
return status == ConsoleRunStatus.Failed ? 1 : 0;

internal sealed class HelloScreen : Screen
{
    public HelloScreen()
    {
        var message = new Text("Ready.");
        var exit = new Button { Content = new Text("Exit") };
        exit.Click += (_, _) => Application?.Closed();

        InitializeContent(new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 1,
            Children = { new Text("Hello, SharpVision"), message, exit },
        });
    }
}
```

`Screen` is a retained
[`CompositeControl`](../controls/composite-control.md#overview): its constructor
creates the permanent visual tree and calls `InitializeContent` exactly once.
`Stack.Spacing` inserts one terminal cell between visible children, and the two
alignment properties center the stack inside the application viewport. The
[`Button`](../controls/input/button.md#overview) owns one `Content` child and
publishes `Click` after a completed activation.

> [!NOTE] `Application.Closed()` is currently the public request for an orderly
> shutdown; a dedicated, intention-named `Shutdown()` API is tracked by issue
> #228.

## Run it

```bash
dotnet run
```

`ConsoleApplication.RunAsync` detects redirection, opens the platform console,
starts negotiation and input, commits the first frame, and restores the terminal
modes during shutdown. If the platform description is missing or unsuitable, it
returns `ConsoleRunStatus.UnsupportedTerminal` before emitting any terminal
bytes. Its defaults and fluent alternatives are listed in the
[hosting entry points](../concepts/hosting.md#entry-points).

## Add host options

Pass a builder callback when the application needs an explicit policy:

```csharp
var status = await ConsoleApplication.RunAsync(
    new HelloScreen(),
    static builder => builder
        .UseTheme(Themes.Dark)
        .UseAlternateScreen()
        .UseMouse());
```

Do not construct `ConsoleHost`, terminal `Options`, or escape sequences by hand
for an interactive application. The builder coordinates platform mode leases,
session cleanup, capabilities, and the
[runtime event loop](../architecture/runtime-event-loop.md#overview).

Next,
[compose a responsive layout](layout-and-controls.md#compose-layout-and-controls).
