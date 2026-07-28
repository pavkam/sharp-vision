# FIGlet font catalog provenance

## Source snapshot

The catalog contains the 400 `.flf` and `.tlf` files at the root of
[`xero/figlet-fonts`](https://github.com/xero/figlet-fonts) commit
`417429ef36ab039cbf192a4424c60aa23fc32de8`.

`fonts.manifest.json` records the original filename, format, byte length,
SHA-256, embedded comment notice, and conservative license classification for
every entry. `figlet-fonts.zip` is generated from those exact bytes with sorted
UTF-8 entry names, fixed DOS timestamps, and deterministic Deflate compression.

## Audit result

The snapshot contains:

- 357 fonts with an author or copyright attribution but no explicit
  redistribution grant in the FIGfont comment;
- 42 fonts without a usable outer notice, including 12 nested ZIP-compressed
  TOIlet fonts; and
- one font declaring freeware terms.

These classifications describe evidence, not legal conclusions. The upstream
collection has no repository-wide license. Consequently, the full archive is
retained as an audited source artifact but is a release blocker until the
project owner establishes redistribution permission or approves an appropriate
distribution policy. Do not silently change classifications or strip notices.

## Reproduction

With the exact source checkout available:

```bash
node scripts/audit-figlet-fonts.mjs \
  --source /path/to/figlet-fonts \
  --commit 417429ef36ab039cbf192a4424c60aa23fc32de8 \
  --output src/SharpVision/Fonts/Resources/fonts.manifest.json

node scripts/package-figlet-fonts.mjs \
  --source /path/to/figlet-fonts \
  --output src/SharpVision/Fonts/Resources/fonts.zip
```

The expected archive SHA-256 is
`7ac92bdafd4937c8a921875272da9b33a22f34118f54a68a1cbe0e77fdba163a`.
