# iTerm2 proprietary protocols

## iTerm2 contract

Primary source:
[iTerm2 proprietary escape codes](https://iterm2.com/documentation-escape-codes.html),
accessed 2026-07-11. iTerm2 extends OSC color queries and defines OSC 1337
features including images, files, clipboard, profile/colors, annotations, and
shell integration.

These sequences may behave differently or be filtered inside tmux and GNU
screen. iTerm2 accepts BEL or ST for several OSC commands; SharpVision emits ST
and treats incoming payloads as bounded untrusted data.

## First milestone contract

Document and detect the family, decode relevant replies diagnostically, and
provide a bounded raw extension boundary. OSC 8 hyperlinks use the generic
[OSC contract](osc.md#first-milestone-contract). OSC 1337 image/file transfer,
profile mutation, and clipboard streaming are unsupported; Kitty/OSC 52 typed
clipboard behavior remains preferred.

## Security and tests

The library never writes or opens files based on terminal replies. Tests cover
detection hints, BEL/ST framing, payload limits, multiplexer filtering, safe
fallback, and parser recovery.
