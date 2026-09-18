# Changelog

All notable changes to this project are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.1]

### Added

- **A pixel budget checked before decoding.** `ImageHashOptions.MaxPixels` (default
  50,000,000) is compared with the dimensions in the image header, and an image
  above it fails with the new `ImageTooLargeException` carrying `Width`, `Height`
  and `MaxPixels`. `ImageHasher.Compute` gains overloads taking `ImageHashOptions`.
  Decoders trust the header, so before this a syntactically valid 69-byte PNG
  claiming 20000×20000 allocated 2.2 GB, ran for 18 seconds, and returned a hash.
  It now fails in about 10 milliseconds with no pixel buffer allocated.
- Seven images in the golden dataset, all verified against `imagehash`: random
  noise at 37×23, 640×8 and 9×300 (the last two exercise the horizontal-only and
  vertical-only resize paths), 2×2 and 3×5 images that are enlarged rather than
  reduced, a palette PNG and a CMYK JPEG. **10 images → 17; 30 golden cases → 51.**
- 86 tests: argument validation on every `ImageHasher` entry point, non-seekable
  streams, crafted decompression-bomb headers against the default and custom
  budgets, first-frame-only decoding of an animated GIF, and the parsing and
  equality surface of `ImageHash`. **74 tests → 160; line coverage 96.6% → 100%,
  branch coverage 90.2% → 99.2%.** The remaining partial branch is a divide-by-zero
  guard in the resampler that Lanczos weights cannot trigger.

### Changed

- **Only the first frame of a multi-frame image is decoded.** A 120-frame GIF at
  400×400 peaked at 110 MB to produce one hash; the hash is of the first frame
  either way, which is also what Pillow gives `imagehash`.
- **`ImageHash.Parse` and `TryParse` require the full-width string:** exactly
  ⌈bits/4⌉ hexadecimal digits after an optional `0x`. `ToString` and Python
  `imagehash` always pad, so a shorter string is a stored hash that lost its leading
  zeros or was truncated. `"ff"` no longer parses as a 64-bit hash; `TryParse`
  returns `false` and `Parse` names the expected digit count.
- `ImageHash.Parse` with a bit length outside 1 to 64 throws
  `ArgumentOutOfRangeException` naming `bitLength`, instead of a `FormatException`
  blaming the string.
- Non-seekable streams are buffered explicitly so the header can be read twice.

### Documented

- 16-bit PNGs do not match the reference: Pillow and ImageSharp reduce 16-bit
  samples to 8 bits differently. CMYK JPEGs, which were suspected, do match.
- Truncated images decode to black and hash, where Pillow raises. Noted under a new
  "Untrusted Input" section in the README, with the rest of the threat model.

## [0.4.0]

### Added

- **`HashAlgorithm.PerceptualHash` (pHash) is implemented**, bit-compatible with
  Python `imagehash.phash` at its defaults: 32×32 grayscale, un-normalised DCT-II
  along each axis, top-left 8×8 block including the DC term, strict comparison
  against the `numpy.median` of the block. Coefficients are rounded to six decimals
  first so mathematically equal terms compare equal, as they do out of scipy's
  FFT-based transform; without that a flat image hashes to noise.
- `tools/regenerate_golden.py` rebuilds `expected_hashes.json` from the checked-in
  images with the reference implementation and records the `imagehash`, Pillow and
  NumPy versions in the manifest. The values are no longer edited by hand.
- 14 tests: pHash for all ten golden images, and four synthetic inputs (flat,
  checkerboard, two lit pixels, a lit corner block) whose reference values have a
  clear margin at the median. **60 tests → 74; 180 executions → 222.**

### Changed

- **Hash values can differ from 0.3.0 on borderline images.** The grayscale and
  resize stages now reproduce Pillow's arithmetic exactly (`Internal/PillowResampler.cs`:
  22-bit fixed-point Lanczos-3 coefficients, horizontal then vertical pass, 8-bit
  rounding in between; luma as `(19595 R + 38470 G + 7471 B + 32768) >> 16`).
  ImageSharp's resampler, used before, differs from Pillow's by ±1 on roughly one
  pixel in ten, which made bit-exact pHash impossible and had already produced two
  golden values that the reference does not: the EXIF JPEG's aHash
  (`00000b19…` → `00000919…`) and the cropped PNG's dHash (`6677…` → `6777…`).
  Both now match `imagehash`. If you store hashes, expect at most a bit or two of
  drift on images whose pixels sat on a threshold; re-hash if exact equality
  matters to you.
- ImageSharp is now used for decoding only. `SixLabors.ImageSharp.Processing` is
  no longer referenced.
- README: replaces the terse algorithm list with an account of the exact pipeline
  (Pillow luma before a Lanczos-3 resize, integer mean, strict comparisons, MSB-first
  packing), states plainly that no DCT-based pHash is implemented yet despite the
  package name, and adds a proposed roadmap with the reasoning for pHash, wHash and
  an embedding-based companion package.

## [0.3.0]

### Changed

- **Re-versioned release of 0.2.1; the package contents are identical.** Shipping on
  the .NET 10 and .NET 11 preview SDKs is a minor-level change under this project's
  reading of semver, even with `net8.0` retained, so it carries a minor bump rather
  than a patch. Prefer 0.3.0; 0.2.1 remains on nuget.org only because a published
  version cannot be removed.

## [0.2.1]

### Changed

- **Built and verified with the .NET 10.0.401 SDK and the .NET 11.0.100-rc.1 preview
  SDK** on every target the package ships: `net8.0`, `net10.0` and `net11.0`. All 60
  tests pass on each framework, 180 executions in total. The README now documents
  building from a side-by-side SDK install (`~/.dotnet` with `DOTNET_ROOT`) when the
  SDK on `PATH` is an older .NET 8, which cannot build this repository.
- `Microsoft.SourceLink.GitHub` 8.0.0 → 10.0.401. Its transitive
  `Microsoft.Build.Tasks.Git` 8.0.0 carries a moderate advisory
  ([GHSA-23fw-v26w-5fgq](https://github.com/advisories/GHSA-23fw-v26w-5fgq)); with
  `NuGetAudit` at level `low` and warnings as errors, restore refused to run, so the
  repository did not build at all until the pin moved.
- `Microsoft.NET.Test.Sdk` 18.9.0 → 18.10.1.
- `SixLabors.ImageSharp` stays on 3.1.12, and the reason recorded in
  `Directory.Packages.props` is corrected. It previously said 4.x requires `net9.0`;
  4.x does ship a `net8.0` assembly. The real obstacle is that ImageSharp 4 fails the
  build unless a Six Labors licence key is present and is no longer offered under the
  Apache 2.0 side of the Split License, which an MIT library cannot pass on to its
  consumers.

## [0.2.0]

### Fixed

- **`ImageHash` did not enforce its own bit-length invariant.** `Value` could carry
  more bits than `BitLength` declared, and nothing checked it, so
  `ImageHash.Parse("ffffffffffffffff", 8)` returned a hash reporting 8 bits while
  holding 64. Because `HammingDistance` counts set bits across the whole 64-bit
  word, that hash produced a distance of `64` where the maximum possible is `8`,
  and a similarity of `-7` against a documented range of `0.0` to `1.0`. It also
  printed 16 hex digits while claiming to pad to its bit length.

  The constructor now rejects a value that does not fit, and `TryParse` returns
  `false` rather than truncating.

- **`default(ImageHash)` returned `NaN` from `Similarity`.** A struct is always
  default-constructible past its constructor's checks, so `BitLength` was `0` and
  the similarity calculation divided by it. There is now an `IsEmpty` property,
  and the comparison methods throw with a message naming what to call instead.

- `PackageProjectUrl` and `RepositoryUrl` pointed at `github.com/aelena/NetImgHash`,
  which does not exist — the repository is `imagehash-net` — so every link on the
  NuGet listing would have 404'd.

### Changed

- **The test project only ever ran on `net8.0`** while the library multi-targeted,
  so nothing was exercised on the newer runtimes the package was published for.
  The suite now runs on every target framework the package ships.
- **Target frameworks are stated outright** instead of being switched on
  `$(NETCoreSdkVersion)`. Deriving them from whichever SDK happens to be installed
  means the package you build is not the package CI builds, and a contributor on an
  older SDK silently produces fewer targets than the one that reaches NuGet.
  `net9.0` is dropped (out of support since May 2026); `net10.0` and `net11.0` are
  added; `net8.0` stays until its November 2026 end of life.
- Central Package Management with exact pins, `NuGetAudit` across the whole graph,
  and analyzers at `latest-recommended` with warnings as errors.
- Deterministic builds, SourceLink, and a `.snupkg` symbol package, so the
  published assembly can be stepped into and traced back to a commit.
- `ToString` composed an `"x{n}"` format string on every call, allocating on every
  comparison; it now formats into a stack buffer.
- xunit 2.5.3 → 2.9.3, Test SDK 17.8 → 18.9, coverlet 6.0 → 10.0.1, and
  AwesomeAssertions added (MIT; FluentAssertions 8 moved to a paid licence for
  commercial use).

### Removed

- `Internal/Dct.cs`, a throwing stub referenced by nothing. `PerceptualHash` and
  `WaveletHash` remain reserved on the `HashAlgorithm` enum, where they throw
  `NotSupportedException` honestly.

### Added

- GitHub Actions: CI builds and tests on Linux and Windows across all three target
  frameworks; a tag-driven release workflow packs, verifies the tag matches the
  package version, and publishes to NuGet.
- 31 tests covering the bit-length invariant, the uninitialized default, mismatched
  lengths, and hex round-tripping. **29 tests on one framework → 60 on three.**
