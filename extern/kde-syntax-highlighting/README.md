# KDE syntax-highlighting definition provenance

## Curated source

`SharpVision.SyntaxHighlighting` embeds only the syntax-definition XML files
from [`KDE/syntax-highlighting`](https://github.com/KDE/syntax-highlighting) at
commit `60cfa684b64cccde19bf12c74db52129709ed863` whose own
`<language license="…">` attribute, or an in-file `SPDX-License-Identifier`
header, states an unambiguous permissive license: `MIT`, `BSD-3-Clause`,
`CC0-1.0`, `Zlib`, or an explicit `Public Domain` dedication. Compound SPDX
expressions are rejected rather than reduced to one branch. When both an SPDX
header and the document element's `license` attribute exist, their canonical
classifications must agree; commented example elements do not count as metadata.

At that commit, `data/syntax/` contains 409 definitions. 159 meet that bar and
are embedded; the remaining 250 are excluded because they carry no `license`
attribute at all, an empty one, an ambiguous bare `"BSD"` value with no clause
count, or a copyleft license (GPL, LGPL, and similar). Notably this excludes
several widely used definitions upstream ships under GPL/LGPL or with no stated
license at all, including C, C#, Python, PHP, Lua, MATLAB, Objective-C, Pascal,
and JSON. `SharpVision.SyntaxHighlighting`'s loader still accepts any KDE-format
XML file supplied from an application's own file system at runtime (see
`SyntaxDefinitionCatalog.FromFile`/`FromDirectory`), matching upstream Kate's
own local-override model, so an application remains free to add one of these
excluded definitions on its own without this package redistributing it.

`SharpVision.SyntaxHighlighting` additionally embeds one first-party definition
not sourced from this checkout at all:
`src/SharpVision.SyntaxHighlighting/Resources/Syntax/csharp.xml` (C#), written
from scratch against the C# grammar directly and released under this project's
own MIT license, because upstream's own C# definition is one of the 250 excluded
above (no stated license) and cannot be redistributed. It carries
`sourceRepository` pointing at this project's own repository and an empty
`sourceCommit` in `syntax.manifest.json`, rather than the upstream pin every
other entry carries. `scripts/audit-syntax-definitions.mjs`'s
`firstPartyDefinitions` set names it explicitly, and
`scripts/package-syntax-definitions.mjs`'s `stageCuratedSyntaxDefinitions`
preserves it - rather than deleting it - every time this checkout is refreshed
from a newer upstream pin.

## Reproduction

Check out the pinned commit above, then run:

```bash
node scripts/package-syntax-definitions.mjs \
  --source /path/to/syntax-highlighting \
  --output src/SharpVision.SyntaxHighlighting/Resources/Syntax

node scripts/audit-syntax-definitions.mjs \
  --source src/SharpVision.SyntaxHighlighting/Resources/Syntax \
  --output src/SharpVision.SyntaxHighlighting/Resources/syntax.manifest.json
```

The packaging script rejects a source checkout whose `HEAD` is not the pinned
commit or whose tracked or untracked working tree is not clean, and copies only
files whose stated license passes the classification above. The audit script
rejects duplicate language names, re-derives the manifest from the staged files,
and fails on drift.

## Verification

```bash
node --test scripts/audit-syntax-definitions.test.mjs scripts/package-syntax-definitions.test.mjs

node scripts/audit-syntax-definitions.mjs \
  --source src/SharpVision.SyntaxHighlighting/Resources/Syntax \
  --output src/SharpVision.SyntaxHighlighting/Resources/syntax.manifest.json \
  --check
```
