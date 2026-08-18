# Unicode data provenance

The `17.0.0/` snapshot contains pinned Unicode Character Database and
conformance test inputs downloaded from the official
[Unicode 17.0.0 data directory](https://www.unicode.org/Public/17.0.0/ucd/).
Each source URL and SHA-256 is declared in `scripts/generate-unicode-data.mjs`;
the generator rejects any byte mismatch before updating product tables.

Run `npm run check:unicode` to verify generated terminal Unicode data against
this snapshot, or `npm run refresh:unicode` to download and validate the pinned
sources again.

The files are distributed under the adjacent [Unicode License V3](LICENSE.txt),
and their original `ReadMe.txt` remains in the versioned snapshot.
