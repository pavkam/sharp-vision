# SharpVision.SyntaxHighlighting third-party notices

## KDE syntax-highlighting definitions

The following 159 syntax-definition XML files are copied byte-for-byte from
[`KDE/syntax-highlighting`](https://github.com/KDE/syntax-highlighting) commit
`60cfa684b64cccde19bf12c74db52129709ed863`, `data/syntax/` directory. Each file
is embedded as its own resource so loading one language does not read a
catalog-wide archive.

Every file below is included only because its own `<language license="…">`
attribute, or an in-file `SPDX-License-Identifier` header, states one of the
five unambiguous permissive licenses grouped below.
`extern/kde-syntax-highlighting/README.md` documents the full audit methodology,
including the 250 files this package does not redistribute because they carry no
stated license, an empty one, an ambiguous bare `"BSD"` value, or a copyleft
license. The excluded set notably includes several very widely used definitions
upstream ships under GPL/LGPL or with no stated license, such as C, C#, Python,
PHP, Lua, MATLAB, Objective-C, Pascal, and JSON — see
`SyntaxDefinitionCatalog.FromFile`/ `FromDirectory` to load one of those (or any
other KDE-format definition) from an application's own files instead.

The complete license text for each SPDX identifier below is packaged as
`licenses/<identifier>.txt`; the four `Public-Domain` files carry no separate
license file since their original authors declared them public domain outright.
Every embedded XML file also keeps whatever copyright or SPDX comment header it
shipped with upstream, unchanged.

## First-party definitions

`SharpVision.SyntaxHighlighting` also embeds one syntax definition that is
**not** third-party: `csharp.xml` (C#), written from scratch by SharpVision
contributors against the C# grammar directly, because upstream's own C#
definition is one of the 250 excluded files above (no stated license) and cannot
be redistributed. It is ordinary SharpVision source code licensed under this
repository's own root `LICENSE`, the same as every other file in this project -
it does not belong in, and is intentionally omitted from, the third-party tables
below.

### MIT (149)

| Language                           | File                            | Author                                                                                                      |
| ---------------------------------- | ------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| ABNF                               | `abnf.xml`                      | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Adblock Plus                       | `adblock.xml`                   | `Volker Krause (vkrause@kde.org)`                                                                           |
| AHDL                               | `ahdl.xml`                      | `Dominik Haumann (dhaumann@kde.org)`                                                                        |
| Alerts                             | `alert.xml`                     | `Dominik Haumann (dhaumann@kde.org)`                                                                        |
| ANSI C89                           | `ansic89.xml`                   | `Dominik Haumann (dhaumann@kde.org)`                                                                        |
| ANTLR                              | `antlr.xml`                     | `Andrzej Borucki (borucki.andrzej@gmail.com)`                                                               |
| AppArmor Security Profile          | `apparmor.xml`                  | `Nibaldo González (nibgonz@gmail.com)`                                                                      |
| ARM Assembler                      | `asm-arm.xml`                   | `Leo Marušić`                                                                                               |
| AsciiDoc                           | `asciidoc.xml`                  | `Andreas Gratzer`                                                                                           |
| BrightScript                       | `brightscript.xml`              | `Daniel Levin (dendy.ua@gmail.com)`                                                                         |
| Cabal                              | `cabal.xml`                     | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Cap'n Proto                        | `capnproto.xml`                 | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| CashScript                         | `cashscript.xml`                | `James Zuccon`                                                                                              |
| ChangeLog                          | `changelog.xml`                 | `Dominik Haumann (dhaumann@kde.org)`                                                                        |
| CLIST                              | `clist.xml`                     | —                                                                                                           |
| COBOL                              | `cobol.xml`                     | `Jonathan Poelen (jonathan.poelen@gmail.com);github.com/MihailJP`                                           |
| CoffeeScript                       | `coffee.xml`                    | `Max Shawabkeh (max99x@gmail.com)`                                                                          |
| Comments                           | `comments.xml`                  | `Alex Turbov (i.zaufi@gmail.com)`                                                                           |
| Common Intermediate Language (CIL) | `cil.xml`                       | `Volker Krause (vkrause@kde.org)`                                                                           |
| Common Lisp                        | `commonlisp.xml`                | `Dominik Haumann (dhaumann@kde.org)`                                                                        |
| CSV                                | `csv.xml`                       | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| CSV (pipe)                         | `csv-pipe.xml`                  | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| CSV (semicolon)                    | `csv-semicolon.xml`             | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| CSV (whitespace)                   | `csv-whitespace.xml`            | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Cucumber Gherkin feature           | `gherkin.xml`                   | `Samu Voutilainen (kde.gherkin-syntax@smar.fi)`                                                             |
| D2                                 | `d2.xml`                        | —                                                                                                           |
| Dart                               | `dart.xml`                      | `Waqar Ahmed (waqar.17a@gmail.com)`                                                                         |
| Devicetree Source (DTS)            | `dts.xml`                       | `Artur Weber`                                                                                               |
| Dockerfile                         | `dockerfile.xml`                | `James Turnbull (james@lovedthanlost.net)`                                                                  |
| Doxyfile                           | `doxyfile.xml`                  | `Ernst Maurer (ernst.maurer@gmail.com)`                                                                     |
| Doxygen                            | `doxygen.xml`                   | `Dominik Haumann (dhaumann@kde.org)`                                                                        |
| Earthfile                          | `earthfile.xml`                 | `Alex Turbov (i.zaufi@gmail.com)`                                                                           |
| Elixir/EEx                         | `elixir-eex.xml`                | `Jade Pfeiffer (jade@pfeiffer.codes)`                                                                       |
| Elixir/HEEx                        | `elixir-heex.xml`               | `Jade Pfeiffer (jade@pfeiffer.codes)`                                                                       |
| Elm                                | `elm.xml`                       | `Bonghyun Kim (bonghyun.d.kim@gmail.com)`                                                                   |
| Elvish                             | `elvish.xml`                    | `Lilith Houtjes (lilith@stroopwafel.dev)`                                                                   |
| Email                              | `email.xml`                     | `Volker Krause (vkrause@kde.org)`                                                                           |
| Expect                             | `expect.xml`                    | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| FlatBuffers                        | `flatbuffers.xml`               | `Harald Fernengel`                                                                                          |
| Fluent                             | `fluent.xml`                    | `Fabian Wunsch (fabian@uriah.heep.sax.de)`                                                                  |
| Fortran (Fixed Format)             | `fortran-fixed.xml`             | `Franchin Matteo (fnch@libero.it)`                                                                          |
| Fortran (Free Format)              | `fortran-free.xml`              | `Franchin Matteo (fnch@libero.it), Janus Weil`                                                              |
| FreeFEM                            | `edp.xml`                       | —                                                                                                           |
| GDL                                | `gdl.xml`                       | `Christoph Cullmann (cullmann@absint.com)`                                                                  |
| Gemtext                            | `gemtext.xml`                   | `Cuche (mike@cuche.cc)`                                                                                     |
| Gleam                              | `gleam.xml`                     | `Louis Guichard (kde@glpda.net)`                                                                            |
| GN                                 | `gn.xml`                        | `BogDan Vatra (bogdan@kde.org)`                                                                             |
| GNU Gettext                        | `gettext.xml`                   | `Dominik Haumann (dhaumann@kde.org)`                                                                        |
| GPRBuild                           | `gpr.xml`                       | `Léo Germond (germond@adacore.com)`                                                                         |
| GraphQL                            | `graphql.xml`                   | `Volker Krause (vkrause@kde.org)`                                                                           |
| GTK Blueprint                      | `gtk-blueprint.xml`             | `Zoey Ahmed`                                                                                                |
| Hare                               | `hare.xml`                      | `Akseli Lahtinen (akselmo@akselmo.dev)`                                                                     |
| Haxe                               | `haxe.xml`                      | `Chad Joan`                                                                                                 |
| Hjson                              | `hjson.xml`                     | `Marco Nelles (marco@maniatek.de)`                                                                          |
| IATA SSIM                          | `ssim.xml`                      | `Volker Krause (vkrause@kde.org)`                                                                           |
| InnoSetup                          | `innosetup.xml`                 | `Michael Hansen`                                                                                            |
| Intel HEX                          | `intelhex.xml`                  | `Miklos Marton (martonmiklosqdev@gmail.com)`                                                                |
| Jam                                | `jam.xml`                       | `Mildred (silkensedai@online.fr)`                                                                           |
| Java Module                        | `java-module.xml`               | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Java Properties                    | `java-properties.xml`           | `Matthias Böhm (MatthiasBoehm87 _at_ gmail.com)`                                                            |
| JavaScript React (JSX)             | `javascript-react.xml`          | `Nibaldo González (nibgonz@gmail.com)`                                                                      |
| JCL                                | `jcl.xml`                       | —                                                                                                           |
| Jinja                              | `jinja.xml`                     | `zoltan.gera@qt.io`                                                                                         |
| JSON5                              | `json5.xml`                     | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Jsonnet                            | `jsonnet.xml`                   | `Ribhav Kaul`                                                                                               |
| Julia                              | `julia.xml`                     | —                                                                                                           |
| Kate Config                        | `kateconfig.xml`                | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Klipper Config                     | `klipper-config.xml`            | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Klipper G-Code                     | `klipper-gcode.xml`             | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| LaTeX Log File                     | `latex-logfile.xml`             | `Thomas Braun (thomas.braun@virtuell-zuhause.de)`                                                           |
| Log File (advanced)                | `logfile-advanced.xml`          | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Log File (advanced) Selector       | `logfile-advanced-selector.xml` | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Log File (simplified)              | `logfile.xml`                   | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Log File (simplified) Selector     | `logfile-selector.xml`          | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Logcat                             | `logcat.xml`                    | `Nibaldo González (nibgonz@gmail.com)`                                                                      |
| MapCSS                             | `mapcss.xml`                    | `Volker Krause (vkrause@kde.org)`                                                                           |
| Mermaid                            | `mermaid.xml`                   | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Metamath                           | `metamath.xml`                  | `Aaron Puchert`                                                                                             |
| MIB                                | `mib.xml`                       | `Jaap Keuter (jaap.keuter@xs4all.nl)`                                                                       |
| MIPS Assembler                     | `mips.xml`                      | `Dominik Haumann (dhaumann@kde.org)`                                                                        |
| Modelines                          | `modelines.xml`                 | `Alex Turbov (i.zaufi@gmail.com)`                                                                           |
| Modula-2                           | `modula-2.xml`                  | `B. Kowarsch (trijezdci@github)`                                                                            |
| Modula-2 (ISO only)                | `modula-2-iso-only.xml`         | `B. Kowarsch (trijezdci@github)`                                                                            |
| Modula-2 (PIM only)                | `modula-2-pim-only.xml`         | `B. Kowarsch (trijezdci@github)`                                                                            |
| Modula-2 (R10 only)                | `modula-2-r10-only.xml`         | `B. Kowarsch (trijezdci@github)`                                                                            |
| Mustache/Handlebars (HTML)         | `mustache.xml`                  | `Nibaldo González (nibgonz@gmail.com), based on the HTML highlighter by Wilbert Berendsen (wilbert@kde.nl)` |
| NFTables                           | `nftables.xml`                  | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| nginx Configuration                | `nginx.xml`                     | `Jyrki Gadinger (nilsding@nilsding.org)`                                                                    |
| Ninja                              | `ninja.xml`                     | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Nix                                | `nix.xml`                       | `Katalin Rebhan &lt;me@dblsaiko.net&gt;`                                                                    |
| Odin                               | `odin.xml`                      | `Akseli Lahtinen (akselmo@akselmo.dev)`                                                                     |
| OORS                               | `oors.xml`                      | `Gernot Gebhard (gebhard@absint.com)`                                                                       |
| OpenSCAD                           | `openscad.xml`                  | `Julian Stirling (julian@julianstirling.co.uk)`                                                             |
| Org Mode                           | `orgmode.xml`                   | `Gary Wang`                                                                                                 |
| Overpass QL                        | `overpassql.xml`                | `Volker Krause (vkrause@kde.org)`                                                                           |
| PIO Assembler                      | `pioasm.xml`                    | `Dale Cook (cook.dale.e@gmail.com)`                                                                         |
| Pony                               | `pony.xml`                      | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| PowerShell                         | `powershell.xml`                | `Motoki Kashihara (motoki8791@gmail.com); Michael Lombardi (Michael.T.Lombardi@outlook.com)`                |
| QDoc Configuration                 | `qdocconf.xml`                  | `Volker Krause (vkrause@kde.org)`                                                                           |
| QFace                              | `qface.xml`                     | `Dominik Holland (dominik.holland@qt.io), Zoltan Gera (zoltan.gera@qt.io)`                                  |
| QMake                              | `qmake.xml`                     | `Milian Wolff (mail@milianw.de), Kevin Funk (kevin.funk@kdab.com)`                                          |
| QML                                | `qml.xml`                       | `Milian Wolff (mail@milianw.de)`                                                                            |
| R documentation                    | `rdoc.xml`                      | `Aaron Puchert`                                                                                             |
| Racket                             | `racket.xml`                    | `slbtty(shenlebantongying@gmail.com)`                                                                       |
| Raku                               | `raku.xml`                      | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| RenPy                              | `renpy.xml`                     | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| RETRO                              | `retro.xml`                     | —                                                                                                           |
| Robot                              | `robot.xml`                     | `Akseli Lahtinen (akselmo@akselmo.dev)`                                                                     |
| Rust                               | `rust.xml`                      | `The Rust Project Developers`                                                                               |
| SAS                                | `sas.xml`                       | `Michael Walshe (michael.j.t.walshe@gmail.com)`                                                             |
| SASS                               | `sass.xml`                      | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Scheme                             | `scheme.xml`                    | `Dominik Haumann (dhaumann@kde.org)`                                                                        |
| SELinux CIL Policy                 | `selinux-cil.xml`               | `Nibaldo González (nibgonz@gmail.com)`                                                                      |
| SELinux File Contexts              | `selinux-fc.xml`                | `Nibaldo González (nibgonz@gmail.com)`                                                                      |
| SELinux Policy                     | `selinux.xml`                   | `Nibaldo González (nibgonz@gmail.com)`                                                                      |
| Sieve                              | `sieve.xml`                     | `Volker Krause (vkrause@kde.org)`                                                                           |
| Slint                              | `slint.xml`                     | `SixtyFPS GmbH (info@slint.dev)`                                                                            |
| Smali                              | `smali.xml`                     | —                                                                                                           |
| SML                                | `sml.xml`                       | `Christoph Cullmann (cullmann@kde.org)`                                                                     |
| Snakemake                          | `snakemake.xml`                 | `Thomas Bigot`                                                                                              |
| Snort/Suricata                     | `snort_suricata.xml`            | —                                                                                                           |
| Solidity                           | `solidity.xml`                  | `Robert Kaiser (kairo@kairo.at)`                                                                            |
| SPARQL                             | `sparql.xml`                    | `Damian Oswald (damian.oswald@protonmail.com)`                                                              |
| SPDX-Comments                      | `spdx-comments.xml`             | `Alex Turbov (i.zaufi@gmail.com)`                                                                           |
| Stan                               | `stan.xml`                      | —                                                                                                           |
| STEP                               | `step.xml`                      | `Volker Krause (vkrause@kde.org)`                                                                           |
| SubRip Subtitles                   | `subrip-subtitles.xml`          | `Nibaldo González (nibgonz@gmail.com)`                                                                      |
| Swift                              | `swift.xml`                     | `I. Elland (igor@elland.me)`                                                                                |
| systemd unit                       | `systemd-unit.xml`              | `Andreas Gratzer`                                                                                           |
| Terraform                          | `terraform.xml`                 | `Thuck (denisdoria@gmail.com)`                                                                              |
| TextProto                          | `textproto.xml`                 | `Alexander Potashev (aspotashev@gmail.com)`                                                                 |
| Tiger                              | `tiger.xml`                     | `Pablo Oliveira`                                                                                            |
| TLA+                               | `tlaplus.xml`                   | `Younes (dev@younes.io)`                                                                                    |
| Todo.txt                           | `todo.xml`                      | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| toit                               | `toit.xml`                      | `Florian Loitsch (florian@toit.io)`                                                                         |
| TSV                                | `tsv.xml`                       | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| Twig/Twig                          | `twig.xml`                      | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| TypeScript                         | `typescript.xml`                | `Nibaldo González (nibgonz@gmail.com)`                                                                      |
| TypeScript React (TSX)             | `typescript-react.xml`          | `Nibaldo González (nibgonz@gmail.com)`                                                                      |
| Typst                              | `typst.xml`                     | `Katalin Rebhan &lt;me@dblsaiko.net&gt;`                                                                    |
| V                                  | `v.xml`                         | `Lars Pontoppidan (dev.larpon@gmail.com)`                                                                   |
| Viper                              | `viper.xml`                     | `nishanthkarthik`                                                                                           |
| Wayland Trace                      | `wayland-trace.xml`             | `Andreas Cord-Landwehr (cordlandwehr@kde.org)`                                                              |
| Web Video Text Tracks              | `webvtt.xml`                    | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| XHTML                              | `xhtml.xml`                     | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| XKeyboardConfig                    | `xkb.xml`                       | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |
| YARA                               | `yara.xml`                      | —                                                                                                           |
| Zig                                | `zig.xml`                       | `Waqar Ahmed (waqar.17a@gmail.com)`                                                                         |
| Zsh                                | `zsh.xml`                       | `Jonathan Poelen (jonathan.poelen@gmail.com)`                                                               |

### BSD-3-Clause (4)

| Language             | File             | Author                                |
| -------------------- | ---------------- | ------------------------------------- |
| ACPI DSL             | `acpi-dsl.xml`   | `Fabian Vogt (fabian@ritter-vogt.de)` |
| ACPI Source Language | `acpi-asl.xml`   | `Fabian Vogt (fabian@ritter-vogt.de)` |
| GNU M4               | `m4.xml`         | `Jaak Ristioja`                       |
| PureScript           | `purescript.xml` | `Gleb Popov (6yearold@gmail.com)`     |

### CC0-1.0 (1)

| Language     | File            | Author         |
| ------------ | --------------- | -------------- |
| CartoCSS MSS | `carto-css.xml` | `Lukas Sommer` |

### Zlib (1)

| Language   | File             | Author        |
| ---------- | ---------------- | ------------- |
| CubeScript | `cubescript.xml` | `Kevin Meyer` |

### Public-Domain (4)

| Language                       | File          | Author                             |
| ------------------------------ | ------------- | ---------------------------------- |
| fstab                          | `fstab.xml`   | `Diego Iastrubni (elcuco@kde.org)` |
| PostScript Printer Description | `ppd.xml`     | `Lukas Sommer`                     |
| RPM Spec                       | `rpmspec.xml` | —                                  |
| vCard, vCalendar, iCalendar    | `vcard.xml`   | `Lukas Sommer`                     |
