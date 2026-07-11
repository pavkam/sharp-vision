# GNU screen compatibility

## GNU screen contract

Primary source:
[GNU screen control sequences](https://www.gnu.org/software/screen/manual/html_node/Control-Sequences.html),
accessed 2026-07-11. Screen recognizes a VT/ANSI subset and can pass a DCS
payload to the host terminal without interpretation.

Screen may filter, reinterpret, or limit modern mouse, OSC, color, and graphics
features. A `TERM` value associated with screen selects a conservative profile;
outer-terminal behavior requires explicit override or verified passthrough.

## First milestone contract

Support the documented VT/ANSI subset, bounded DCS passthrough for approved
queries, and safe omission of unsupported modern extensions. Do not emit OSC 83
screen commands or other session-control operations.

## Tests

Test filtered and passed sequences, split DCS/ST, nested multiplexer limits,
conservative capability selection, explicit overrides, and a real screen
pseudoterminal smoke path where available.
