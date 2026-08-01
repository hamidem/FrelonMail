using System.Security.Cryptography;
using Xunit;

namespace Frelon.Mail.Tests;

public sealed class HostileEmailCorpusTests
{
    private static readonly string ExternalCorpusDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "Corpus",
        "External");

    private static readonly CorpusCase[] ExternalCases =
    [
        new(
            "mimekit-empty-multipart.eml",
            "075b14b04c98f9b9d81370ca6a4cc73491ecf18ae374e5a637c7dbb28c115c6f"),
        new(
            "mimekit-missing-subtype.eml",
            "5226395ab60ce2f9dfeed86695d114d3219bd5054ed689b61623b5224a871929"),
        new(
            "mimekit-long-address-list.eml",
            "bfd547668029267401f2dcf6228c20d1193cdbc8b91cf73641146365c8bf8008"),
    ];

    public static TheoryData<string, string> ExternalEmlCases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var testCase in ExternalCases)
            {
                data.Add(testCase.FileName, testCase.Sha256);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ExternalEmlCases))]
    public async Task PipelineComplet_AnalyseChaqueRegressionExterneDansUnTempsBorne(
        string fileName,
        string expectedSha256)
    {
        var path = Path.Combine(ExternalCorpusDirectory, fileName);
        var content = await File.ReadAllBytesAsync(
            path,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            expectedSha256,
            Convert.ToHexStringLower(SHA256.HashData(content)));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var source = new MemoryStream(content, writable: false);
        var analyzer = EmailIncidentAnalyzerFactory.CreateDefault();

        var incident = await analyzer.AnalyzeAsync(
            source,
            fileName,
            timeout.Token);

        Assert.Equal(fileName, incident.Evidence.FileName);
        Assert.Equal(expectedSha256, incident.Evidence.Sha256);
    }

    [Fact]
    public void CorpusExterne_NeContientAucunFichierNonDeclare()
    {
        var expectedFiles = ExternalCases
            .Select(testCase => testCase.FileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualFiles = Directory
            .GetFiles(ExternalCorpusDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedFiles, actualFiles);
    }

    private sealed record CorpusCase(string FileName, string Sha256);
}
