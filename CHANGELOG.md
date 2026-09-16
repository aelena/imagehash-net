# Changelog

All notable changes to this project are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

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
