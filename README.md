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

## Which Algorithm Is Used

Despite the package name, **this library does not yet implement pHash**, the
DCT-based algorithm most people mean by "perceptual hash". What ships today is
**aHash** and **dHash**, the two simplest members of the same family. Both follow
the Python `imagehash` implementation step for step, which is what makes the golden
dataset possible: a hash computed here is the same 64 bits `imagehash` produces for
the same file.

### The shared pipeline

Every algorithm starts the same way, in `Internal/ImageProcessing.cs`:

1. **Decode** the image with ImageSharp into 8-bit RGBA. Alpha is ignored; a
   transparent pixel contributes only its RGB values, exactly as Pillow's
   `convert("L")` does.
2. **Convert to grayscale** using Pillow's ITU-R 601-2 luma weights
   (`0.299 R + 0.587 G + 0.114 B`, rounded to the nearest integer). Doing this
   *before* resizing, not after, matters: the two orders give different pixels
   after interpolation, and `imagehash` converts first.
3. **Resize** the grayscale image to a tiny fixed grid with a Lanczos-3 filter,
   which is Pillow's `LANCZOS` (formerly `ANTIALIAS`). This throws away all fine
   detail and normalises scale, so a 4000×3000 photo and its 400×300 thumbnail
   arrive at the same few dozen pixels.
4. **Threshold** those pixels into bits. How the threshold is chosen is what
   distinguishes the algorithms.

The result is always 64 bits, packed most-significant-bit first in row-major order
(the top-left decision is bit 63) and printed as 16 lowercase hex digits.

### Average hash (`HashAlgorithm.AverageHash`)

- Resize to **8×8** (64 pixels).
- Compute the **mean** luminance as an integer (sum of the 64 values divided by 64,
  fractional part dropped).
- Set a bit to `1` where the pixel is **strictly greater** than the mean.

aHash encodes which regions of the image are lighter than average. It is the
cheapest hash to compute and is good at finding exact and near-exact duplicates:
re-encodes, resizes, small colour shifts. Its weakness is that a global brightness
or contrast change, or a large uniform region, can flip many bits at once because
every pixel is compared against a single number.

### Difference hash (`HashAlgorithm.DifferenceHash`)

- Resize to **9 wide × 8 high** (72 pixels).
- For each row, compare each pixel with its **right-hand neighbour**: 8 comparisons
  per row, 64 in total.
- Set a bit to `1` where the right pixel is **brighter** than the left.

dHash encodes horizontal gradients rather than absolute brightness, so a uniform
brightness or contrast change leaves it untouched: if the right pixel was brighter
before, it is brighter after. In practice it produces fewer false matches than aHash
on photographic content at the same cost, which is why it is the better default for
"is this the same picture, lightly edited?".

### What the golden dataset shows

Hamming distance from the base image, out of 64 bits, for the variants in
`tests/NetImgHash.Tests/TestData`:

| Variant | aHash | dHash |
|---------|------:|------:|
| Grayscale copy, 4K-style resize, thumbnail, transparent PNG, EXIF-tagged JPEG (orientation not applied) | 0 | 0 |
| JPEG re-encoded at quality 90 and at quality 50 | 1 | 0 |
| Slight crop and resize | 15 | 15 |
| 90° rotation | 32 | 32 |

The first two rows are what these hashes are for: scale, format, colour and mild
compression changes leave them intact or one bit off. The last two rows are what
they are not for. A crop shifts every pixel of the grid, and a rotation scrambles
it; 32 bits out of 64 is the distance between two unrelated images.

### What pHash would add, and why it is not here yet

The classic pHash algorithm goes further:

1. Resize to a larger grid, typically **32×32**, and convert to grayscale.
2. Apply a **2-D Discrete Cosine Transform**, turning the pixels into frequency
   coefficients (the same transform JPEG uses).
3. Keep only the **low-frequency 8×8 block** in the top-left corner. These
   coefficients describe the coarse structure of the image; the rest is detail and
   noise.
4. Threshold those 64 coefficients against their **median** (`imagehash` uses the
   median; some descriptions say mean) to produce 64 bits.

Working in the frequency domain makes pHash noticeably more tolerant of JPEG
re-compression, blur, gamma and contrast changes than aHash or dHash, because those
operations mostly perturb high frequencies that pHash has already discarded. It costs
a 32×32 DCT per image, which is still trivial.

It is reserved on the `HashAlgorithm` enum and `Compute` throws
`NotSupportedException` for it, rather than silently returning some other hash. The
roadmap below is the plan for filling it in.

## Roadmap: Proposed Additional Algorithms

None of these is committed yet. They are listed with the reasoning for wanting each,
so the order can be argued about. The constraint they all work under is the one this
library already has: results must match the Python `imagehash` reference bit for bit
where a reference exists, and the core package keeps ImageSharp as its only runtime
dependency.

| Algorithm | Robust to | Weak against | Cost | Feasibility |
|-----------|-----------|--------------|------|-------------|
| aHash (shipped) | resize, re-encode, small colour shifts | brightness/contrast, uniform regions | lowest | — |
| dHash (shipped) | the above, plus brightness/contrast | rotation, crop, flips | lowest | — |
| pHash | the above, plus JPEG artefacts, blur, gamma | rotation, crop, flips | low (32×32 DCT) | high, no new dependency |
| wHash | similar to pHash, better at multi-scale structure | rotation, crop, flips | low (Haar DWT) | high, no new dependency |
| Embedding hash | crop, framing, viewpoint, same *subject* | exact-duplicate precision; determinism across hardware | high (neural network) | separate package |

### pHash (DCT)

**Why.** It is the algorithm the package is named after, and the one users of
`imagehash` reach for by default. The golden dataset's JPEG variants barely move
aHash and dHash, but those are mild re-encodes of a synthetic image. On photographs
saved at low quality, or after blur, sharpening or gamma correction, pHash holds
its distance where the pixel-domain hashes start to drift.

**How.** A separable 2-D DCT-II over a 32×32 grid is two passes of a 32-point 1-D
DCT, implementable in a few dozen lines with no dependency. Matching `imagehash`
means following its exact choices: `scipy.fftpack.dct` with the default
un-normalised `type=2`, applied along rows then columns; the top-left 8×8 block
*including* the DC term; and the **median** as threshold, not the mean. The golden
dataset would be extended with `imagehash.phash` outputs to lock it down.

### wHash (wavelet)

**Why.** wHash replaces the DCT with a Haar wavelet decomposition. Wavelets localise
in both space and frequency, so wHash tends to represent images with strong regional
structure (a bright object on a dark background, text on a page) more stably than a
global DCT does, and it degrades more gracefully as the image is scaled. Having both
lets a caller pick per corpus.

**How.** The Haar DWT is averages and differences of neighbouring pixels, repeated
per level; no dependency needed. The `imagehash` reference has a few knobs that
would need to be honoured for compatibility: the image is scaled to a power of two
(`image_scale`); by default the top-level LL coefficient is zeroed and the image
reconstructed without it (`remove_max_haar_ll=True`); then a second decomposition
runs to a level derived from the hash size and its low-frequency band is
thresholded against the median. More surface than pHash, so it would follow it.

### Embedding-based ("deep") hashes

**Why.** Every hash above is a *pixel-structure* hash: it answers "is this the same
picture, lightly modified?". None of them can say that two photos show the same
object from a different angle, a different crop, or a different framing. The
cropped and rotated entries in the test dataset are the honest demonstration:
their aHash and dHash are, correctly, far from the original's (15 and 32 bits
out of 64). A neural embedding
(CLIP, DINOv2 and similar) captures *what* is in the image, so a hash derived from it
answers the semantic question instead. For deduplicating a photo library or
matching product images that is often the question actually being asked.

**How, and why it would be a separate package.** Run an ONNX model through
`Microsoft.ML.OnnxRuntime` to get a float vector, then binarise it with
random-hyperplane locality-sensitive hashing so the result is still a fixed-width
bit string comparable by Hamming distance through the existing `ImageHash` type.
Two consequences keep it out of the core:

- The dependency footprint is a native runtime plus a model file of tens to
  hundreds of megabytes, against a core package whose whole appeal is one managed
  dependency and a 12 KB assembly.
- Floating-point results differ slightly across CPUs and GPUs, so a bit near a
  hyperplane can flip between machines. Such a hash is *comparable* but not
  *reproducible* the way the pixel hashes are, and the documentation would have to
  say so plainly.

Both point at a companion package (`PerceptualHash.NET.Embeddings` or similar) that
depends on this one and reuses `ImageHash`, rather than a fourth algorithm on the
same enum.

### Also worth considering

- **Crop-resistant hashing.** `imagehash` ships a segmentation-based approach that
  hashes image regions independently and matches on any shared region. It directly
  addresses the crop case without a neural network, at the price of a variable-size
  hash that does not fit the current 64-bit `ImageHash`.
- **Larger hash sizes.** `imagehash` lets every algorithm run at 16×16 (256 bits)
  for finer discrimination. `ImageHash` is capped at 64 bits today; lifting that is
  a prerequisite for both this and crop-resistant hashing.

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
