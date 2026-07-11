# Control and integration testing

## Control and integration testing

Every concrete control tests property validation before mutation,
measure/arrange/render invalidation, ownership, dispatcher affinity, focus,
pointer capture, keyboard/pointer parity, disabled/hidden state, visual-state
composition, zero/tiny bounds, resize, events, and final semantic cells.

Layout tests use recording controls to assert measure constraints, desired size,
arranged slots, call order, cache invalidation, non-reentrancy, rounding, and
clipping. Routed-input tests record route, phase, source, handled state, local
coordinates, mutation during dispatch, default behavior, and cleanup.

## End-to-end path

Representative tests start with raw terminal key/mouse/paste/resize bytes and
exercise decoder, dispatcher, hit testing/focus, control behavior, invalidation,
layout, cell drawing, frame diff, encoder, and captured output bytes. Assertions
cover intermediate typed boundaries only when they are public contracts; final
bytes and virtual screen are mandatory.

## Controls with state machines

Buttons, toggles, radio groups, text editing, selection, menus, popups, windows,
scrollbars, and scroll views enumerate valid/invalid transitions and event
order. Fake clocks drive hover/open delays, timers, idle, and repeated input
without wall-clock sleeps.
