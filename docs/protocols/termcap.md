# Termcap terminal descriptions

## Termcap contract

The Unix provider calls only `setupterm`. When the loaded ncurses build includes
full termcap compatibility, that one call owns `TERMCAP`, `TERMPATH`, aliases,
and `tc` resolution and exposes canonical `tiget*` results. A build without
usable matching inline `TERMCAP` support returns a typed provider failure with a
structured diagnostic. The
[terminal integration contract](../architecture/terminal-integration.md#terminal-integration-contract)
owns host selection, while the
[rendering pipeline](../architecture/rendering-pipeline.md#rendering-pipeline-contract)
owns command emission from the normalized profile.

## Native provider boundary

The provider requirements below apply to the ncurses compatibility path.
SharpVision bounds and validates its inputs and copied results; ncurses owns
termcap source lookup, inheritance, and normalization.

## Sources

- [ncurses 6.6 `terminfo(5)`](https://invisible-island.net/ncurses/man/terminfo.5.html),
  including termcap compatibility, translation, and length limits, accessed
  2026-07-19.
- [ncurses 6.6 `ncurses(3X)`](https://invisible-island.net/ncurses/man/ncurses.3x.html),
  including `TERMCAP`, `TERMPATH`, and fallback-file behavior, accessed
  2026-07-19.
- [ncurses termcap source](https://invisible-island.net/archives/ncurses/current/termcap-20250719.src.gz),
  revision 1.467 dated 2025-07-19, accessed 2026-07-19.

## Termcap lookup

This section records source order owned internally by full-termcap ncurses
builds. SharpVision does not initiate or observe a separate fallback call:

1. `TERMCAP` supplies an inline description for the requested name or the path
   of a termcap file. Inline content applies only when its name matches.
2. If `TERMCAP` supplies neither usable content nor a usable file, `TERMPATH` is
   searched as its declared Unix colon-separated file list.
3. If neither variable supplies a usable source, search `/etc/termcap`,
   `/usr/share/misc/termcap`, and `$HOME/.termcap`, in that order.

Native lookup is governed by the
[provider trust boundary](terminfo.md#native-provider-trust-boundary). Before
invocation, SharpVision bounds its copied terminal name, relevant live
environment values, and inline `TERMCAP`; afterward, it bounds every copied
canonical value. The provider rejects inline content above the fixed historical
1023-byte ceiling and never truncates it. SharpVision does not parse termcap
files to measure raw or resolved entries: ncurses file scanning, `tc` expansion,
and internal resolution remain trusted stable local-host work outside
SharpVision's adversarial memory bound.

ncurses owns `tc` resolution. SharpVision accepts only canonical values that
satisfy the accepted-snapshot and per-value limits. A cycle, unresolved parent,
malformed source, or native-provider failure is a provider failure; SharpVision
does not parse an inheritance graph or retry another terminal type.

## ncurses normalization

The ncurses compatibility provider resolves termcap and exposes canonical
terminfo identifiers through `tigetflag`, `tigetnum`, and `tigetstr`.
SharpVision applies the exact
[terminfo allowlist](terminfo.md#finite-capability-boundary) only after that
normalization; it does not implement a separate two-character projection parser.

The following required raw termcap codes must be available through the shown
canonical identifiers after ncurses normalization:

| Raw termcap | Canonical terminfo |
| ----------- | ------------------ |
| `cm`        | `cup`              |
| `me`        | `sgr0`             |
| `cl`        | `clear`            |
| `ce`        | `el`               |
| `cd`        | `ed`               |

Optional raw codes are accepted only when ncurses normalizes them to an
allowlisted canonical identifier. SharpVision never guesses aliases, shortens
names, or infers support. An absent normalized value remains absent evidence and
never establishes feature support.

The provider runs the same
[full-screen suitability](terminfo.md#full-screen-suitability) validation after
ncurses normalization. Missing optional values degrade safely. Missing required
cursor addressing, rendition reset, or usable clearing rejects before output; a
requested cursor or alternate-screen change still requires its matched fallback.

## Expected behavior

Provider fixtures prove the single `setupterm` path with the native build's
`TERMCAP` file and ordered `TERMPATH`; the required raw-to-canonical mappings;
optional normalization without guessed aliases; inline 1023-byte rejection;
provider-failure handling; absent extended-capability evidence; and no output
before a required capability failure.

> [!NOTE] The installed ncurses library, rather than SharpVision, owns fallback
> among `/etc/termcap`, `/usr/share/misc/termcap`, and `$HOME/.termcap`.
> Repository fixtures cover explicit `TERMCAP` and `TERMPATH` sources; they do
> not replace platform-specific verification of ncurses' default file ordering.
