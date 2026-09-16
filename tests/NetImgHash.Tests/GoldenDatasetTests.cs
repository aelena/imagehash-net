using System.Text.Json;

namespace NetImgHash.Tests;

public sealed class GoldenDatasetTests
{
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TheoryData<string, string, string, string> HashExpectations
    {
        get
        {
            var data = new TheoryData<string, string, string, string>();
            var manifestPath = Path.Combine(AppContext.BaseDirectory, "TestData", "expected_hashes.json");
            var manifest = JsonSerializer.Deserialize<ExpectedHashManifest>(
                File.ReadAllText(manifestPath), ManifestOptions)
                ?? throw new InvalidOperationException("Failed to deserialize expected hash manifest.");

            foreach (var entry in manifest.Entries)
            {
                data.Add(entry.File, nameof(HashAlgorithm.AverageHash), entry.AverageHash, entry.Notes ?? string.Empty);
                data.Add(entry.File, nameof(HashAlgorithm.DifferenceHash), entry.DifferenceHash, entry.Notes ?? string.Empty);
                data.Add(entry.File, nameof(HashAlgorithm.PerceptualHash), entry.PerceptualHash, entry.Notes ?? string.Empty);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(HashExpectations))]
    public void GeneratedHashes_MatchGoldenDataset(string fileName, string algorithmName, string expected, string _)
    {
        var imagePath = Path.Combine(AppContext.BaseDirectory, "TestData", "Images", fileName);
        var algorithm = Enum.Parse<HashAlgorithm>(algorithmName);

        var actual = ImageHasher.Compute(imagePath, algorithm);

        Assert.Equal(expected, actual.ToString());
    }

    private sealed record ExpectedHashManifest(IReadOnlyList<ExpectedHashEntry> Entries);

    private sealed record ExpectedHashEntry(
        string File,
        string AverageHash,
        string DifferenceHash,
        string PerceptualHash,
        string? Notes);
}
