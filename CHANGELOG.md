# Changelog

All notable changes to this project are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
