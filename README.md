# PerceptualHash.NET

[![CI](https://github.com/aelena/imagehash-net/actions/workflows/ci.yml/badge.svg)](https://github.com/aelena/imagehash-net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PerceptualHash.NET.svg)](https://www.nuget.org/packages/PerceptualHash.NET)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0%20%7C%2011.0-blue)]()
[![License](https://img.shields.io/badge/license-MIT-green)]()

`PerceptualHash.NET` is a cross-platform image hashing library for modern .NET, implemented in the `NetImgHash` namespace and built on `SixLabors.ImageSharp`.

Output is verified against the Python [`imagehash`](https://github.com/JohannesBuchner/imagehash) reference implementation by a golden dataset, so hashes are comparable across the two ecosystems.

## Status

This repository currently ships the MVP surface:

- `AverageHash` (`aHash`)
- `DifferenceHash` (`dHash`)
- `ImageHash` value type with Hamming distance and similarity helpers

`PerceptualHash` (`pHash`) and `WaveletHash` (`wHash`) are reserved in the public API for follow-up releases.

## Target Frameworks

- `net8.0` — in support until November 2026
- `net10.0` — LTS
- `net11.0`

`net9.0` is not targeted: it went out of support in May 2026.

The frameworks are fixed in `Directory.Build.props` rather than derived from the
installed SDK, so a local build produces the same set of targets as CI and as the
published package. Building all three needs the .NET 8, 10, and 11 SDKs; to build
a subset, pass `-p:TargetFrameworks=net10.0`.

## Install

```bash
dotnet add package PerceptualHash.NET
```

## Usage

```csharp
using NetImgHash;

using var stream1 = File.OpenRead("image-original.jpg");
using var stream2 = File.OpenRead("image-edited.jpg");

var hash1 = ImageHasher.Compute(stream1, HashAlgorithm.DifferenceHash);
var hash2 = ImageHasher.Compute(stream2, HashAlgorithm.DifferenceHash);

var distance = hash1.HammingDistance(hash2);   // 0 = identical, 64 = every bit differs
var similarity = hash1.Similarity(hash2);      // 1.0 = identical, 0.0 = every bit differs

// Hashes round-trip through lowercase hex, so they can be stored and compared later.
var stored = hash1.ToString();                 // e.g. "a1b2c3d4e5f60718"
var restored = ImageHash.Parse(stored);
```

A `Hamming` distance of 0–5 on a 64-bit hash usually means the same image; above
about 10 usually means a different one. Tune the threshold against your own data.

`Compute` throws `NotSupportedException` for `HashAlgorithm.PerceptualHash` and
`HashAlgorithm.WaveletHash`, which are reserved but not yet implemented.

## Algorithm Specifications

### Average Hash

1. Convert the image to grayscale.
2. Resize to `8x8`.
3. Compute the mean luminance value.
4. Set each bit when the pixel is greater than the mean.
5. Return the 64-bit result as lowercase hexadecimal.

### Difference Hash

1. Convert the image to grayscale.
2. Resize to `9x8`.
3. Compare each pixel to its neighbor on the left.
4. Set each bit when the right-hand pixel is brighter.
5. Return the 64-bit result as lowercase hexadecimal.

## Compatibility Notes

- The implementation is tuned to stay close to Python `imagehash` semantics for `aHash` and `dHash`.
- The checked-in golden dataset under `tests/NetImgHash.Tests/TestData` is used to lock hash formatting, bit ordering, and representative image behavior.
- EXIF auto-orientation is not part of the hashing pipeline for this MVP.

## Test Data

The test project includes a golden dataset with representative images:

- standard RGB image
- grayscale image
- high-resolution resize
- thumbnail
- EXIF-orientation JPEG
- transparent PNG
- JPEG quality variants
- cropped version
- rotated version

The expected results live in `tests/NetImgHash.Tests/TestData/expected_hashes.json`.

To regenerate the dataset locally with Python:

1. Create a local virtual environment.
2. Install `pillow` and `imagehash`.
3. Regenerate the images and manifest.
4. Run `dotnet test`.

## Development

```bash
dotnet build PerceptualHash.NET.sln
dotnet test PerceptualHash.NET.sln   # runs once per target framework
```

CI builds and tests on Linux and Windows across all three frameworks. Releases are
tag-driven: pushing a `v*.*.*` tag packs, checks the tag against the package
version, and publishes to NuGet.

## Releasing

Publishing uses **NuGet Trusted Publishing** — nuget.org exchanges a short-lived
GitHub OIDC token for a one-hour API key, so no long-lived secret is stored.

One-time setup on nuget.org (Account → Trusted Publishing):

| Field | Value |
|-------|-------|
| Repository Owner | `aelena` |
| Repository | `imagehash-net` |
| Workflow File | `release.yml` (file name only, no path) |
| Environment | `production` (the workflow declares it; the two must match) |
| Glob Patterns and Packages | `PerceptualHash.NET` |

Create a GitHub environment named `production` in the repository, and add a
secret `NUGET_USER` holding the nuget.org profile name (not an email address).

A policy is bound to **one** repository, so each repository needs its own.

To cut a release: set `<Version>` in `NetImgHash.csproj`, commit, then
`git tag v0.2.0 && git push origin v0.2.0`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

MIT. See `LICENSE`.
