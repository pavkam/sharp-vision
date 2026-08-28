# FIGlet font catalog provenance

## Curated sources

`SharpVision.FigletFonts` contains only these pinned, redistributable sources:

- the 18 `.flf` files in the official
  [`cmatsuoka/figlet`](https://github.com/cmatsuoka/figlet) `fonts/` directory
  at commit `202a0a8110650a943f1125f536b3bb455cf72ee1`, distributed under
  BSD-3-Clause; and
- `Classy.flf` from [`patorjk/figlet.js`](https://github.com/patorjk/figlet.js)
  commit `b95c2f03ccbc7e2a23e9fd030e8378c2d3b9dd0e`, whose embedded notice
  grants use and distribution under MIT.

Every font remains a separate embedded resource. The adjacent schema-2 manifest
records its exact logical resource name, source filename, byte length, SHA-256,
embedded comment notice, SPDX license expression, repository, and commit. The
previous unaudited 400-font archive is not distributed by any project.

## Reproduction

Check out both repositories at the commits above, then run:

```bash
node scripts/package-figlet-fonts.mjs \
  --official-source /path/to/cmatsuoka-figlet \
  --classy-source /path/to/figlet-js \
  --output src/SharpVision.FigletFonts/Resources/Fonts

node scripts/audit-figlet-fonts.mjs \
  --source src/SharpVision.FigletFonts/Resources/Fonts \
  --output src/SharpVision.FigletFonts/Resources/fonts.manifest.json
```

The packaging script rejects source checkouts whose `HEAD` is not the pinned
commit or whose tracked or untracked working tree is not clean. The audit
rejects extra or missing fonts and any manifest drift.

## Verification

```bash
node --test scripts/audit-figlet-fonts.test.mjs scripts/package-figlet-fonts.test.mjs

node scripts/audit-figlet-fonts.mjs \
  --source src/SharpVision.FigletFonts/Resources/Fonts \
  --output src/SharpVision.FigletFonts/Resources/fonts.manifest.json \
  --check
```
