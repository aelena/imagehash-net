# PerceptualHash.NET

`PerceptualHash.NET` is a cross-platform image hashing library for modern .NET, implemented in the `NetImgHash` namespace and built on `SixLabors.ImageSharp`.

## Status

This repository currently ships the MVP surface:

- `AverageHash` (`aHash`)
- `DifferenceHash` (`dHash`)
- `ImageHash` value type with Hamming distance and similarity helpers

`PerceptualHash` (`pHash`) and `WaveletHash` (`wHash`) are reserved in the public API for follow-up releases.

## Target Frameworks

The library is authored for:

- `net8.0`
- `net9.0`
- `net10.0`

The checked-in project file lights up additional target frameworks when newer SDKs are installed. On a machine with only the .NET 8 SDK, the solution builds and tests the `net8.0` target.

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

var distance = hash1.HammingDistance(hash2);
var similarity = hash1.Similarity(hash2);
```

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
dotnet test PerceptualHash.NET.sln
```

## License

MIT. See `LICENSE`.
