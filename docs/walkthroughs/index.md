# Walkthroughs

Walkthroughs assemble public SharpVision APIs into complete tasks. Each one
explains why a property is set, shows runnable C#, and links every deeper rule
to the section that owns it.

1. [Build your first application](first-application.md#build-your-first-application)
   creates a project, a retained screen, a button, and a clean exit path.
2. [Compose layout and controls](layout-and-controls.md#compose-layout-and-controls)
   combines `Dock`, `Stack`, sizing, spacing, and scrolling into a responsive
   screen.
3. [State, input, and events](state-and-events.md#state-input-and-events)
   updates mutable controls from CLR events and routed input.
4. [Background work and the dispatcher](background-work.md#background-work-and-the-dispatcher)
   performs asynchronous work without violating dispatcher affinity.
5. [Use terminal services](terminal-services.md#use-terminal-services) rings the
   bell, sets a title, and uses capability-gated clipboard output.
6. [Build a custom control](custom-controls.md#build-a-custom-control) chooses
   the correct retained authoring role and builds a reusable composite.

For larger working applications, run the
[text editor](../../examples/TextEditor/README.md#sharpvision-text-editor) or
[Snake](../../examples/Snake/README.md#sharpvision-snake). To inspect the active
terminal's detected capabilities and decoded events, run
[Terminal Debugger](../../examples/TerminalDebugger/README.md#sharpvision-terminal-debugger).
The [showcase contract](../architecture/showcase.md#overview) explains how the
interactive gallery doubles as executable API documentation.

## Reference after the walkthroughs

- Use the [control catalog](../controls/index.md#control-catalog) for component
  properties, defaults, input, layout, examples, and expected behavior.
- Use the [concept map](../concepts/index.md#concept-map) for behavior shared by
  several controls.
- Use [feature support](../features/index.md#feature-support) before relying on
  a terminal extension or platform feature.
- Use the [architecture map](../architecture/index.md#architecture-map) when
  changing framework internals.
