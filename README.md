# PerceptualHash.NET

[![CI](https://github.com/aelena/imagehash-net/actions/workflows/ci.yml/badge.svg)](https://github.com/aelena/imagehash-net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PerceptualHash.NET.svg?logo=nuget)](https://www.nuget.org/packages/PerceptualHash.NET)
[![Downloads](https://img.shields.io/nuget/dt/PerceptualHash.NET.svg?logo=nuget)](https://www.nuget.org/packages/PerceptualHash.NET)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0%20%7C%2011.0-512BD4)]()
[![Tests](https://img.shields.io/badge/tests-180%20passing-brightgreen)]()
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
published package. Building all three needs the .NET 10 SDK or newer (the .NET 11
SDK is still preview); to build a subset, pass `-p:TargetFrameworks=net10.0`.

### Building when the SDK on `PATH` is .NET 8

A .NET 8 SDK cannot build this repository: it does not know `net10.0` or `net11.0`.
Install the newer SDKs side by side without touching `PATH`, then point the build
at that directory. The .NET 8 *runtime* is also needed there so the `net8.0` tests
can execute.

```powershell
# one-time, from https://dot.net/v1/dotnet-install.ps1 (dotnet-install.sh on Linux/macOS)
.\dotnet-install.ps1 -Channel 10.0 -InstallDir ~\.dotnet -NoPath
.\dotnet-install.ps1 -Channel 11.0 -Quality preview -InstallDir ~\.dotnet -NoPath
.\dotnet-install.ps1 -Channel 8.0 -Runtime dotnet -InstallDir ~\.dotnet -NoPath
```

```bash
# every build
PATH=~/.dotnet:$PATH DOTNET_ROOT=~/.dotnet dotnet build PerceptualHash.NET.sln
PATH=~/.dotnet:$PATH DOTNET_ROOT=~/.dotnet dotnet test  PerceptualHash.NET.sln
```

`DOTNET_ROOT` matters as much as `PATH`: without it the test host resolves runtimes
from the machine-wide install and fails to find .NET 10 and 11.

## Install

```bash
dotnet add package PerceptualHash.NET
```

One runtime dependency: `SixLabors.ImageSharp`. Nothing else.

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

## Tests

**60 tests, run against each of the three target frameworks — 180 executions.**

```bash
dotnet test PerceptualHash.NET.sln          # all three frameworks
dotnet test PerceptualHash.NET.sln -f net10.0   # just one
```

| Suite | Tests | What it covers |
|-------|------:|----------------|
| `GoldenDatasetTests` | 20 | Exact hash strings for a checked-in dataset, generated by Python `imagehash`. This is the correctness anchor: it locks bit ordering, hex formatting, and behaviour on greyscale, transparency, EXIF orientation, resizes, crops, rotations and JPEG quality variants. |
| `ImageHashInvariantTests` | 31 | The bit-length invariant, the uninitialized `default`, mismatched lengths, and hex round-tripping. |
| `AlgorithmTests` | 4 | aHash and dHash bit packing against hand-constructed images. |
| `ImageHashTests` | 5 | Distance, similarity, parsing, formatting. |

CI runs the whole suite on **Linux and Windows**. The golden dataset asserts exact
hash strings, so a platform decoding difference would surface there rather than in
production.

## Development

```bash
dotnet build PerceptualHash.NET.sln
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
`git tag v0.3.0 && git push origin v0.3.0`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the full history.

**0.3.0** — Same package as 0.2.1, re-versioned: targeting the .NET 10 and .NET 11
preview SDKs is a minor-level change, so the release carries a minor bump. Prefer
0.3.0 over 0.2.1.

**0.2.1** — Built and verified with the .NET 10.0.401 SDK and the .NET 11.0.100-rc.1
preview SDK on `net8.0`, `net10.0` and `net11.0` (180 test executions passing).
SourceLink moved to 10.0.401 to clear a NuGet audit advisory that had been failing
restore, and the Test SDK to 18.10.1. ImageSharp stays on 3.1.12: version 4 requires
a Six Labors licence key at build time, which an MIT library cannot impose on its
consumers.

## License

**MIT** — see [`LICENSE`](LICENSE). No copyleft anywhere in the dependency graph.

### On ImageSharp

The single runtime dependency, `SixLabors.ImageSharp`, is under the
[Six Labors Split License](https://github.com/SixLabors/ImageSharp/blob/v3.1.12/LICENSE),
which is Apache 2.0 or a commercial licence depending on how you consume it. It
is worth being precise about, because the licence text is explicit and the answer
is favourable:

> Works are licensed to You under the Apache License, Version 2.0 if […] You are
> consuming the Work as a **Transitive Package Dependency**.

If you install `PerceptualHash.NET`, ImageSharp arrives indirectly through it —
that is a transitive dependency by the licence's own definition, so **you receive
ImageSharp under Apache 2.0 regardless of your organisation's size or revenue.**

The commercial-licence threshold (for-profit, over $1M USD annual gross revenue)
applies to a *direct* dependency on ImageSharp. It does not reach consumers of
this package. This library itself qualifies for Apache 2.0 on a separate clause
anyway, being open source.

Not legal advice, but the clause is unambiguous and quoted above so you can check
it yourself.

This is also why the dependency is pinned to the 3.1.x line. ImageSharp 4 moved to
a licence-key model: its build targets fail without a Six Labors key, and it is no
longer offered under the Apache 2.0 side of the Split License. Adopting it would put
that requirement on every consumer of this package, so it stays on 3.1.12 until that
changes.
