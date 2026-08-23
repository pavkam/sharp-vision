# External resources

This directory is the only repository location for checked-in third-party data
and resource payloads. Product projects may embed files from here under stable
logical resource names, but vendored bytes do not live beside product source.

- [FIGlet catalog](figlet/README.md#figlet-font-catalog-provenance) contains the
  audited compressed font collection, its manifest, and redistribution notice.
- [Unicode data](unicode/README.md#unicode-data-provenance) contains the pinned
  Unicode 17.0.0 inputs used to generate terminal grapheme and width tables.
- [KDE syntax-highlighting definitions](kde-syntax-highlighting/README.md#kde-syntax-highlighting-definition-provenance)
  contains the audited, permissively licensed subset of Kate syntax-definition
  XML files.

Every immediate package directory contains provenance and its applicable license
or notice.
