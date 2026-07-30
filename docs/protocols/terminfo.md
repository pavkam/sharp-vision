# Terminfo terminal descriptions

## Terminfo contract

SharpVision loads one requested Unix terminfo description through the ncurses 6
low-level API, copies the finite allowlist under one process-wide lock, compiles
retained output programs, and publishes an owned `TerminalProfile`. Console
hosting selects that profile before output, the session consumes its matched
lifecycle pairs, and rendering consumes exact compiled output programs.
Description key strings are compiled into typed parser signatures or a bounded
longest-match trie and drive the active input decoder. The
[coverage matrix](coverage-matrix.md#coverage) is the current support claim.

## Native description boundary

The provider, host, renderer, and terminal-service requirements below describe
the implemented boundary; the coverage matrix remains the support authority.

## Sources

- [ncurses 6.6 `terminfo(5)`](https://invisible-island.net/ncurses/man/terminfo.5.html),
  including its database search and capability tables, accessed 2026-07-19.
- [ncurses 6.6 `ncurses(3X)`](https://invisible-island.net/ncurses/man/ncurses.3x.html),
  including the environment-variable search rules, accessed 2026-07-19.
- [ncurses 6.6 `curs_terminfo(3X)`](https://invisible-island.net/ncurses/man/curs_terminfo.3x.html),
  including parameter expansion and variable lifetime, accessed 2026-07-19.
- [ncurses terminal-description source revision 1.1261](https://invisible-island.net/ncurses/terminfo.src.html),
  dated 2026-07-19 and accessed 2026-07-20. Its `xterm+sl` entries establish
  `TS` as a title prefix paired with the `fsl` status-line terminator.
- [ncurses 6.6 `user_caps(5)`](https://invisible-island.net/ncurses/man/user_caps.5.html),
  including `RGB` and `U8` type semantics, accessed 2026-07-19.
- [ECMA-48, 5th edition](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
  and
  [xterm control sequences](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html),
  accessed 2026-07-19, define the standard and extension sequences against which
  database strings and optional queries are validated.

## Lookup and fallback

The provider calls ncurses `setupterm` exactly once for the requested terminal
name. ncurses owns this configured search order and returns at most one resolved
entry:

1. `TERMINFO`, when it names a database or an ncurses `hex:`/`b64:` compiled
   description for the requested name.
2. `$HOME/.terminfo`, when this user directory is enabled by the provider's
   ncurses configuration and is safe for the process to read.
3. `TERMINFO_DIRS`, in declared order. On Unix, entries are colon-separated; an
   empty entry means the configured system location.
4. The terminal-description directory configured into that ncurses provider,
   including its configured `/usr/share/terminfo` location when present.

When the loaded ncurses build includes full termcap compatibility, `setupterm`
also owns its documented [termcap search](termcap.md#termcap-lookup).
SharpVision does not call `tgetent`, retry lookup, or decide whether a result
came from terminfo or termcap. An accepted, malformed, oversized, or unsuitable
result is final for that requested name.

`TERM` and all other environment names may select the requested database name,
narrow a semantic feature, or provide tentative diagnostic evidence. They MUST
NOT replace a command string from an accepted description or synthesize one from
a terminal name.

## Limits and provider boundary

`SharpVision.Terminal.Protocols.Limits` owns these implemented provider limits.
Default is the ordinary accepted limit; hard ceiling is the largest caller-set
value. Values are counts, raw bytes, or UTF-8 bytes as stated. A caller value
above a hard ceiling is rejected before lookup. A provider value above its
applicable limit rejects that description before any command is compiled or
emitted.

| Limit               | Default | Hard ceiling | Unit                                                                                 |
| ------------------- | ------: | -----------: | ------------------------------------------------------------------------------------ |
| Terminal name       |     128 |        1,024 | UTF-8 bytes                                                                          |
| Path-list entries   |      64 |          256 | entries                                                                              |
| Each path           |   4,096 |       32,768 | UTF-8 bytes                                                                          |
| Capability string   |  64 KiB |        1 MiB | raw bytes per string                                                                 |
| Accepted snapshot   |   1 MiB |       16 MiB | UTF-8 bytes for relevant live environment evidence and copied accepted facts/strings |
| Key bindings        |     256 |        1,024 | bindings                                                                             |
| Compiled operations |   2,048 |       16,384 | parameter-program operations                                                         |
| Interpreter stack   |      64 |          256 | values while expanding one program                                                   |
| Expansion output    |  64 KiB |        1 MiB | raw bytes for one expanded command                                                   |
| String parameter    |  64 KiB |        1 MiB | raw bytes per parameter                                                              |
| RGB component bits  |      16 |           63 | bits in one parsed `RGB` string component                                            |

## Implemented parameter-program boundary

`Compiler` accepts raw program bytes and compiles `%p1`–`%p9`, `%i`,
`%{number}`, `%'c'`, `%P`/`%g` variables, raw-byte string length `%l`,
arithmetic, bitwise and logical operators, comparisons, nested and chained
`%?`/`%t`/`%e`/`%;` conditionals, `%%`, and `printf`-style `%d`, `%o`, `%x`,
`%X`, `%c`, and `%s` conversions. Numeric execution is a bounded tiparm-style
signed Int32 model: `sbyte`, `byte`, `short`, `ushort`, and `int` inputs are
accepted, while `uint`, `long`, and `ulong` are rejected. Arithmetic and `%i`
wrap on signed Int32 overflow. Width, precision, `#`, `0`, `-`, `+`, and space
flags are supported; `:` disambiguates flags that otherwise name an operator,
and `.` immediately followed by a conversion means precision zero. The compiler
rejects malformed stack use, unsupported legacy termcap forms, output padding,
and every configured byte, operation, stack, width, or precision overflow.

Program literals and `%s` values are raw terminal bytes, not UTF-8 text. The
interpreter validates parameter kinds and bounds, evaluates into reusable owned
storage, and mutates its `IBufferWriter<byte>` only after successful expansion.
Dynamic variables `a`–`z` reset for each expansion. Static variables `A`–`Z`
persist within one render-thread-affine `Interpreter`; persisted string values
are copied into an immutable snapshot on every static string assignment, and
static changes from a failed expansion are discarded. Those string snapshots
allocate by design; a warmed numeric expansion performs no managed allocation.
The interpreter never invokes native `tparm` or `tputs`.

One `Runtime.Session` owns one interpreter. Before terminal I/O it expands each
requested lifecycle pair (`smcup`/`rmcup`, `civis`/`cnorm`, or `smkx`/`rmkx`) as
one transaction. Both programs must be present, require zero parameters, produce
non-empty output within `MaxProgramOutputBytes`, and either both be compiled or
both be the built-in ANSI intrinsic form. Uppercase static variables flow from
the first program to the second and commit only when both expansions succeed. A
rejected pair emits no bytes and cannot leak staged static state into a later
pair.

## Native provider trust boundary

ncurses database lookup, environment paths, and `TERMCAP`/`TERMPATH` files are
trusted stable local host configuration. They are outside SharpVision's
adversarial byte-stream parser and resource guarantee. Under the same
process-wide lock used for `setupterm`, SharpVision reads and bounds the
relevant live `TERMINFO`, `HOME`, `TERMINFO_DIRS`, `TERMCAP`, and `TERMPATH`
values. The process environment must remain stable for the native call;
SharpVision neither replaces nor temporarily mutates it. Process-isolated probes
set child environment values for deterministic tests. After ncurses returns,
SharpVision copies, compiles, and retains only values within the remaining
limits. It does not claim to bound ncurses file scanning or internal
allocations, and it does not cancel `setupterm`.

The provider owns ncurses loading of compiled terminfo and returns an already
resolved entry. SharpVision does not parse `use` references or build a custom
inheritance graph. A provider failure is a rejected description with a
diagnostic; it is not an invitation to try a different terminal type. Terminal
replies and every retained capability value remain untrusted and bounded.

Deployments that require end-to-end bounded lookup MUST provide an explicit,
already-owned `TerminalProfile` and disable native discovery.

### Implemented ncurses 6 API boundary

The Unix provider dynamically discovers current `libncursesw`, `libncurses`, or
split `libtinfo` names and uses `setupterm`, `tigetflag`, `tigetnum`,
`tigetstr`, `set_curterm`, and `del_curterm`. It passes a non-null `errret` to
`setupterm`. Its return and non-null `errret` are interpreted together: ordinary
success is `OK` with `errret=1`; `ERR` with `errret=1` identifies hardcopy and
publishes only synthetic unsuitable metadata without reading `tiget*`; `ERR`
with `errret=0` cannot distinguish a missing entry from a generic description
and returns typed missing-or-generic evidence; `ERR` with `errret=-1` is a
database/provider failure. Other combinations are provider failures.

The provider interprets `tigetflag` `-1`, `tigetnum` `-2`, and `tigetstr`
`(char *)-1` as wrong type. `tigetnum` `-1` and a null `tigetstr` pointer are
absent or cancelled values. A non-null pointer to an empty string remains a
present empty value and cannot satisfy a required program. Every accepted string
is scanned through its configured byte bound and copied before `del_curterm`.

When exported, `use_extended_names` is enabled only inside the same serialized
lease and its prior process-global state is restored. The provider snapshots the
preceding `cur_term`, re-reads the active pointer even after a setup exception,
restores it with `set_curterm`, validates the replaced pointer, and calls
`del_curterm` only after successful restoration. If restoration fails, it
deliberately abandons the new terminal and library handle rather than leave a
dangling process-global `cur_term`. Cleanup diagnostics never replace the
primary load result. No pointer, `tparm`, `tiparm`, or `tputs` call crosses the
provider boundary.

## Full-screen suitability

The conforming reader validates an accepted description before the session emits
application output. It MUST reject `generic_type` (`gn`) and `hard_copy` (`hc`),
and it MUST reject an entry missing any of:

- absolute row-and-column cursor addressing (`cup`);
- complete rendition reset (`sgr0`); and
- usable clearing: `clear`, or both `el` and `ed`.

Usable means the command is allowlisted, decodes, compiles within
`Protocols.Limits`, has its typed parameter arity, and proves non-empty output.
Compiler metadata proves unconditional numeric programs; conditional or
potentially failing programs receive one isolated representative expansion whose
static-variable state is rolled back. Missing required commands reject the
session before its first frame.

Cursor visibility and alternate screen are requested features. The session emits
a cursor-hide command only with a matched `civis`/`cnorm` restoration pair;
otherwise it leaves cursor visibility alone. It emits alternate-screen entry
only with a matched `smcup`/`rmcup` pair. A one-sided, malformed,
parameter-consuming, empty, or over-limit pair is omitted, leaving the main
screen or current cursor state unchanged. Missing allowlisted commands outside
these required and requested pairs degrade by omission or an established safe
default.

Cursor state programs use the stricter static contract. Compiler metadata MUST
prove non-empty output for every execution of exact zero-parameter `civis`,
`cnorm`, and `Se`, and exact one-parameter `Ss`; a representative probe cannot
admit a conditional, variable-dependent, or otherwise fallible cursor program.
Both members of each visibility or shape pair must satisfy that contract. Once
admitted, a requested cursor transition is part of frame correctness: any
unexpected live expansion failure aborts the staged frame before transport and
leaves the prior semantic frame committed for retry.

Renderer and service support uses the same executable-contract boundary. Known
cursor, erasure, rendition, color/default, underline, shape, bell, and title
programs must have their exact typed arity and proven non-empty representative
output, except for the statically proven cursor-state contracts above. `Ms`
specifically requires exactly two string parameters and a non-empty
representative expansion before database evidence may publish OSC 52 support.
Classification is cached as finite bits inside the immutable program set. Each
first-use classification creates local interpreter and output scratch, rolls
back static variables, and retains no thread- or process-scoped probe objects.

Every live compiled expansion is also staged. Zero output, invalid evaluation,
division by zero, and other interpreter failures return failure without copying
bytes or committing static variables. Optional renderer programs degrade by
omission and projected state advances only for expansions that actually emitted
bytes. A failed required program aborts the staged frame batch before transport,
so that frame is byte-quiet.

## Finite capability boundary

The reader accepts only the identifiers in this table. Names are case-sensitive
ncurses identifiers; an inclusive range expands only the shown numeric suffixes.
An absent allowlisted value is absent evidence, not a support claim. Every
identifier outside this table is ignored, including printer control, labels,
micro-motion, and padded hardware behavior.

| Family                                | Exact accepted identifiers                                                                                                                                                                                                                                                                                  |
| ------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Booleans                              | `am`, `bce`, `ccc`, `gn`, `hc`, `km`, `mir`, `msgr`, `npc`, `xenl`, `xon`, `AX`, `Tc`, `XF`, `XT`                                                                                                                                                                                                           |
| Numerics                              | `cols`, `lines`, `colors`, `pairs`, `ncv`, `U8`                                                                                                                                                                                                                                                             |
| Typed color descriptor                | `RGB`                                                                                                                                                                                                                                                                                                       |
| Core, cursor, erasure, and margins    | `bel`, `clear`, `E3`, `cup`, `home`, `cud1`, `cuu1`, `cub1`, `cuf1`, `cud`, `cuu`, `cub`, `cuf`, `hpa`, `vpa`, `csr`, `ind`, `ri`, `indn`, `rin`, `ed`, `el`, `el1`, `ech`, `ich`, `ich1`, `dch`, `dch1`, `il`, `il1`, `dl`, `dl1`, `sc`, `rc`, `civis`, `cnorm`, `cvvis`, `smcup`, `rmcup`, `smkx`, `rmkx` |
| Rendition and color                   | `sgr0`, `sgr`, `bold`, `dim`, `sitm`, `ritm`, `smul`, `rmul`, `rev`, `smso`, `rmso`, `blink`, `invis`, `smxx`, `rmxx`, `setaf`, `setab`, `setrgbf`, `setrgbb`, `setdf`, `setdb`, `op`, `oc`, `initc`, `Smulx`, `Setulc`, `setal`, `Smol`, `Rmol`                                                            |
| Legacy input                          | `kbs`, `kcbt`, `kent`, `kcuu1`, `kcud1`, `kcub1`, `kcuf1`, `khome`, `kend`, `kich1`, `kdch1`, `kpp`, `knp`, `kbeg`, `ka1`, `ka3`, `kb2`, `kc1`, `kc3`, `kf1`–`kf63`                                                                                                                                         |
| Modified xterm input                  | `kUP`, `kDN`, `kLFT`, `kRIT`, `kHOM`, `kEND`, `kIC`, `kDC`, `kNXT`, `kPRV`, each optionally suffixed only `3`–`8`                                                                                                                                                                                           |
| Mouse, focus, and paste               | `kmous`, `XM`, `xm`, `fe`, `fd`, `kxIN`, `kxOUT`, `BE`, `BD`, `PS`, `PE`                                                                                                                                                                                                                                    |
| Clipboard, cursor, title, and reports | `Ms`, `Ss`, `Se`, `Cs`, `Cr`, `TS`, `fsl`, `RV`, `rv`, `XR`, `xr`                                                                                                                                                                                                                                           |

Database strings control legacy command encoding. A bounded query may refine an
optional semantic feature but MUST NOT rewrite a compiled database program.
Padding markers are not commands: a reader removes or rejects them according to
its no-padding policy and MUST NOT delay a frame or control hardware timing.

Canonical values must have their listed types. Boolean identifiers accept only
Boolean values. Numeric identifiers, including `U8`, accept only numeric values.
`RGB` accepts exactly these ncurses forms and is metadata used with validated
`setaf`/`setab`, never a parameter program itself:

- Boolean `true` derives the total color-index width from `colors`, then assigns
  red `(width + 2) / 3`, green `(width + 1) / 3`, and blue `width / 3` with
  integer division. Thus `colors#256` is `3/3/2`, while `colors#16777216` is
  `8/8/8`.
- A positive numeric value gives equal bits per red, green, and blue component;
  for example, `RGB#1` means `1/1/1`.
- A string is exactly three slash-separated decimal bit widths,
  `red/green/blue`, such as `8/8/8`.

Each parsed string component is bounded by `RGB component bits` and must be
nonzero. For numeric and string forms, `colors` must equal the color count
described by the component widths: `2^(3 * bits)` for the numeric form and
`2^(red + green + blue)` for the string form. Zero, malformed text, overflow, an
extra/missing field, or precision inconsistent with the described `colors` value
is ignored with a structured diagnostic. A wrong-type canonical value is also
ignored with a structured diagnostic and cannot satisfy suitability or establish
feature support.

## Typed surface and precedence

Immutable `TerminalProfile` owns `Description`, semantic `Capabilities`, opaque
compiled-program values, and the key map as immutable snapshots. The ncurses
provider loads database values and the compiler validates retained programs. The
runtime consumes matched lifecycle programs, selects described application
cursor-key mode, and passes the immutable key map into its protocol router. The
renderer uses exact `cup`, erasure, rendition, color/default, `Ss`/`Se`, and
reset programs through one transaction-scoped interpreter. `Setulc` receives the
ncurses packed RGB integer after semantic color projection. Built-in ANSI
programs use explicit intrinsic markers mapped to typed encoders.

Every retained `kcuu1`, `kcud1`, `kcub1`, `kcuf1`, Home, End, Insert, Delete,
Page Up, Page Down, BackTab, Enter, keypad-position, `kf1`–`kf63`, and
allowlisted modified-key string has one typed logical identity. `kbeg` and `kb2`
map to Begin; `ka1`/`ka3`/`kc1`/`kc3` map to Home/Page Up/End/Page Down. CSI,
SS3, Escape, C0, and DEL strings compile to structural `KeySignature` values.
The map rejects exact-byte conflicts and equivalent-signature conflicts,
including seven-bit and eight-bit CSI or SS3 spellings, before profile
publication. A profile containing an eight-bit spelling authorizes only that
standalone CSI or SS3 introducer in ground state. It does not enable parser-wide
C1 controls, and a pending UTF-8 scalar consumes its continuation byte first.
Identical duplicates coalesce.

Structural compilation uses the active parser's `MaxParameterBytes` and
`MaxIntermediateBytes`. Exact-limit signatures are admitted; an over-limit,
empty, incomplete, or parser-unreachable optional key is omitted with
`DescriptionDiagnosticCode.InvalidKey` while the remaining description and key
bindings stay usable. `InvalidKey` is appended after every previously shipped
public diagnostic value.

A finite trie is built only for accepted byte strings that are not one parser
signature. Its storage is bounded by the description key count and per-string
limits; matching retains no more than the longest candidate. It chooses the
longest complete binding, rematches the suffix of a shorter match from the trie
root, replays bytes that fail at the root exactly once, and treats invalid UTF-8
as terminal bytes rather than text. A new match cannot begin while a UTF-8
scalar is pending, but an already retained match prefix continues to own its
remaining bytes. Every matched byte advances absolute input accounting exactly
once, so later diagnostic offsets include adjacent and completion-time keys.
Parser-control-prefixed strings that are not one complete structural signature
are rejected instead of entering that trie. Decoder disposal clears pending and
rematch bytes, releases the matcher binding/trie arrays and replay workspace,
and remains idempotent; no described-key byte storage survives that ownership
boundary.

Input precedence is fixed: registered typed replies, paste framing, mouse and
focus reports, and Kitty keyboard events consume their grammar before a
described key lookup. A complete described signature then wins over the optional
generic legacy grammar. That generic grammar is enabled only by the explicit
built-in ANSI compatibility profile; arbitrary database profiles do not inherit
xterm meanings for strings they did not describe. A lone Escape retains the
finite input Escape deadline.

Database color fidelity is effective only as a complete directional contract.
Indexed color requires both `setaf` and `setab` plus a complete default-color
path (`op`, `setdf` with `setdb`, or the required `sgr0`). True color
additionally requires both `setrgbf` and `setrgbb`; otherwise it lowers to the
highest complete tier, down to monochrome. Basic and indexed underline colors
are first resolved through the deterministic palette, then packed as RGB for
`Setulc`.

A described title is supported only by a complete, non-empty, parameterless
`TS`/`fsl` pair. The runtime expands both programs before publishing any bytes,
then emits `TS`, the validated UTF-8 title payload, and `fsl` as one ordered
out-of-band write. A one-sided, parameter-consuming, intrinsic/compiled mixed,
or failed pair is byte-quiet. Title payloads containing C0 or DEL control bytes
are rejected before pair expansion or queueing. `TS` alone is not interpreted as
OSC 2.

A described bell is supported only when `bel` accepts zero parameters and proves
non-empty output. Unsupported bell and title service calls remain byte-quiet and
do not attempt live expansion.

`Description.AutomaticMargins`, `EatNewlineGlitch` (`xenl`), and
`BackColorErase` retain the database Boolean semantics. Final-column output is
followed by absolute positioning before further bytes. `el` is selected for a
trailing blank only when `bce` is true and the projected blank style is safe;
otherwise exact spaces preserve terminal-model equivalence.

Full redraw resets through exact `sgr0`, then uses exact `clear` when retained.
The normative usable `el`/`ed` alternative instead homes through exact
`cup(0, 0)` and expands exact `ed`; the renderer's outer transaction rolls back
all description state and publishes no bytes if any required expansion fails.

A description loaded from a database records `Description.Origin` as
`DescriptionOrigin.Database`; a semantic feature records supported
`Origin.Database` evidence only when its exact non-empty backing program is
retained. An explicit replacement `TerminalProfile` may replace command strings
and programs as one immutable value. `Settings` overrides semantic features only
and MUST NOT expose raw database command strings.

Exact key-byte ownership, conflicting-sequence rejection, conservative-only
database projection, later-evidence precedence, transplanted-claim
normalization, and semantic-only constructor suitability are owned by the
[capability contract](../architecture/capabilities.md#terminal-description-profile).

The profile-construction order is: built-in safety defaults, accepted database
description, environment narrowing and hints, bounded query results, then
explicit semantic settings or an explicit replacement `TerminalProfile`. The
detailed cross-layer ownership is in the
[capability contract](../architecture/capabilities.md#terminal-description-profile).

## Expected behavior

Fixture databases and runtime tests prove the lookup order, every
`Protocols.Limits` boundary, provider-failure distinction, `gn`/`hc` rejection,
required-command rejection before output, matched cursor/alternate-screen
fallback, allowlist filtering, parameter-program bounds, and final lifecycle
bytes coming from a database program rather than an environment terminal name.
