# Architecture specifications

## Architecture map

- [Project structure](project-structure.md#project-structure-contract) defines
  the one-way dependency graph.
- [Runtime event loop](runtime-event-loop.md#runtime-event-loop-contract)
  defines dispatcher ordering and lifecycle.
- [Rendering pipeline](rendering-pipeline.md#rendering-pipeline-contract)
  defines cell drawing, damage, and output.
- [Capabilities](capabilities.md#capability-contract) defines detection,
  overrides, and safe fallback.
- [Memory ownership](memory-ownership.md#memory-ownership-contract) defines
  spans, pooled storage, and asynchronous lifetime.
- [Error handling](error-handling.md#error-handling-contract) defines programmer
  errors, diagnostics, and restoration.
- [Showcase](showcase.md#showcase-contract) defines the interactive product
  gallery and executable API proof.
