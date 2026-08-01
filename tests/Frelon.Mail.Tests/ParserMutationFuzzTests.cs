using System.Text;
using MsgKitEmail = MsgKit.Email;
using MsgKitSender = MsgKit.Sender;
using Xunit;

namespace Frelon.Mail.Tests;

public sealed class ParserMutationFuzzTests
{
    private const int DefaultCaseCount = 128;
    private const int MaximumCaseCount = 50_000;
    private const ulong DefaultCampaignSeed = 1_179_796_812;
    private static readonly byte[] OutlookCompoundFileSignature =
    [
        0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1,
    ];

    [Fact]
    [Trait("Category", "Fuzz")]
    public async Task CampagneDeMutations_ProduitSeulementUnResultatOuUnRefusMaitrise()
    {
        var seeds = await LoadSeedsAsync(TestContext.Current.CancellationToken);
        var campaignSeed = ReadCampaignSeed();
        var requestedCase = ReadOptionalCaseNumber();
        var caseNumbers = requestedCase is null
            ? Enumerable.Range(0, ReadCaseCount())
            : [requestedCase.Value];

        foreach (var caseNumber in caseNumbers)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var mutation = DeterministicEmailMutator.Create(
                seeds,
                campaignSeed,
                caseNumber);
            using var caseTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            caseTimeout.CancelAfter(TimeSpan.FromSeconds(3));
            await using var source = new MemoryStream(
                mutation.Content,
                writable: false);
            var parser = new EmailEvidenceParser();

            try
            {
                _ = await parser.ParseAsync(source, caseTimeout.Token);
            }
            catch (EmailAnalysisLimitException)
            {
                // Refus défensif attendu.
            }
            catch (InvalidDataException)
            {
                // Entrée corrompue rejetée selon le contrat public.
            }
            catch (OperationCanceledException)
                when (!TestContext.Current.CancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(ReproductionMessage(
                    campaignSeed,
                    caseNumber,
                    mutation,
                    "délai de 3 secondes dépassé"));
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    ReproductionMessage(
                        campaignSeed,
                        caseNumber,
                        mutation,
                        $"exception inattendue {exception.GetType().Name}"),
                    exception);
            }
        }
    }

    [Fact]
    public async Task Mutateur_RejoueUnCasIndependammentDeLOrdreDExecution()
    {
        var seeds = await LoadSeedsAsync(TestContext.Current.CancellationToken);

        var first = DeterministicEmailMutator.Create(
            seeds,
            DefaultCampaignSeed,
            caseNumber: 42);
        _ = DeterministicEmailMutator.Create(
            seeds,
            DefaultCampaignSeed,
            caseNumber: 7);
        var replay = DeterministicEmailMutator.Create(
            seeds,
            DefaultCampaignSeed,
            caseNumber: 42);

        Assert.Equal(first.SourceName, replay.SourceName);
        Assert.Equal(first.Description, replay.Description);
        Assert.Equal(first.Content, replay.Content);
    }

    private static async Task<IReadOnlyList<CorpusSeed>> LoadSeedsAsync(
        CancellationToken cancellationToken)
    {
        var corpusDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Corpus",
            "External");
        var paths = Directory
            .GetFiles(corpusDirectory, "*.eml", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal);
        var seeds = new List<CorpusSeed>();
        foreach (var path in paths)
        {
            seeds.Add(new CorpusSeed(
                Path.GetFileName(path),
                await File.ReadAllBytesAsync(path, cancellationToken),
                IsOutlookMessage: false));
        }

        seeds.Add(new CorpusSeed(
            "synthetic-outlook.msg",
            CreateSyntheticOutlookMessage(),
            IsOutlookMessage: true));
        return seeds;
    }

    private static byte[] CreateSyntheticOutlookMessage()
    {
        using var email = new MsgKitEmail(
            new MsgKitSender("sender@example.test", "Synthetic sender"),
            "Synthetic fuzz seed");
        email.Recipients.AddTo("analyst@example.test", "Synthetic analyst");
        email.BodyText = "Local defensive parser seed.";
        email.InternetMessageId = "<fuzz-seed@example.test>";
        email.TransportMessageHeadersText =
            "From: Synthetic sender <sender@example.test>\r\n" +
            "To: Synthetic analyst <analyst@example.test>\r\n" +
            "Subject: Synthetic fuzz seed\r\n" +
            "Message-ID: <fuzz-seed@example.test>\r\n";

        using var stream = new MemoryStream();
        email.Save(stream);
        return stream.ToArray();
    }

    private static int ReadCaseCount()
        => ReadBoundedInteger(
            "FRELON_FUZZ_CASES",
            DefaultCaseCount,
            minimum: 1,
            maximum: MaximumCaseCount);

    private static int? ReadOptionalCaseNumber()
    {
        var value = Environment.GetEnvironmentVariable("FRELON_FUZZ_CASE");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, out var caseNumber) || caseNumber < 0)
        {
            throw new InvalidOperationException(
                "FRELON_FUZZ_CASE doit être un entier positif ou nul.");
        }

        return caseNumber;
    }

    private static ulong ReadCampaignSeed()
    {
        var value = Environment.GetEnvironmentVariable("FRELON_FUZZ_SEED");
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultCampaignSeed;
        }

        if (!ulong.TryParse(value, out var seed))
        {
            throw new InvalidOperationException(
                "FRELON_FUZZ_SEED doit être un entier non signé.");
        }

        return seed;
    }

    private static int ReadBoundedInteger(
        string variableName,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out var parsed)
            || parsed < minimum
            || parsed > maximum)
        {
            throw new InvalidOperationException(
                $"{variableName} doit être compris entre {minimum} et {maximum}.");
        }

        return parsed;
    }

    private static string ReproductionMessage(
        ulong campaignSeed,
        int caseNumber,
        MutationCase mutation,
        string failure)
        =>
            $"Fuzz case {caseNumber} ({mutation.SourceName}, {mutation.Description}) : " +
            $"{failure}. Rejouer avec FRELON_FUZZ_SEED={campaignSeed} " +
            $"et FRELON_FUZZ_CASE={caseNumber}.";

    private sealed record CorpusSeed(
        string Name,
        byte[] Content,
        bool IsOutlookMessage);

    private sealed record MutationCase(
        string SourceName,
        string Description,
        byte[] Content);

    private static class DeterministicEmailMutator
    {
        private const int MaximumMutationBytes = 512 * 1024;
        private static readonly byte[][] InjectionTokens =
        [
            Encoding.ASCII.GetBytes("\r\nContent-Type: multipart/mixed; boundary=\""),
            Encoding.ASCII.GetBytes("\r\nContent-Transfer-Encoding: base64\r\n\r\n===="),
            Encoding.ASCII.GetBytes("\r\nReceived: from (((invalid)))\r\n"),
            Encoding.ASCII.GetBytes("\r\n\r\n--frelon-boundary--\r\n"),
            [0x00, 0x00, 0xFF, 0xFF, 0x00, 0xFF],
            OutlookCompoundFileSignature,
        ];

        public static MutationCase Create(
            IReadOnlyList<CorpusSeed> seeds,
            ulong campaignSeed,
            int caseNumber)
        {
            if (caseNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(caseNumber));
            }

            var random = new StableRandom(
                campaignSeed
                ^ (0x9E3779B97F4A7C15UL * checked((ulong)caseNumber + 1)));
            var seed = seeds[random.NextInt(seeds.Count)];
            var operation = random.NextInt(7);
            var content = seed.Content.ToArray();
            string description;

            switch (operation)
            {
                case 0:
                    description = FlipBits(content, ref random);
                    break;
                case 1:
                    description = FillRange(content, ref random);
                    break;
                case 2:
                    (content, description) = Truncate(content, ref random);
                    break;
                case 3:
                    (content, description) = DeleteRange(content, ref random);
                    break;
                case 4:
                    (content, description) = DuplicateRange(content, ref random);
                    break;
                case 5:
                    (content, description) = InsertToken(content, ref random);
                    break;
                default:
                    description = OverwriteWithToken(content, ref random);
                    break;
            }

            if (seed.IsOutlookMessage
                && caseNumber % 2 == 0
                && content.Length >= OutlookCompoundFileSignature.Length)
            {
                OutlookCompoundFileSignature.CopyTo(content, 0);
                description += "+signature-ole-conservée";
            }

            return new MutationCase(seed.Name, description, content);
        }

        private static string FlipBits(byte[] content, ref StableRandom random)
        {
            var changes = 1 + random.NextInt(16);
            for (var index = 0; index < changes; index++)
            {
                var position = random.NextInt(content.Length);
                content[position] ^= (byte)(1 << random.NextInt(8));
            }

            return $"bits-inversés:{changes}";
        }

        private static string FillRange(byte[] content, ref StableRandom random)
        {
            var start = random.NextInt(content.Length);
            var length = 1 + random.NextInt(Math.Min(256, content.Length - start));
            var value = random.NextInt(3) switch
            {
                0 => byte.MinValue,
                1 => byte.MaxValue,
                _ => (byte)random.NextInt(256),
            };
            content.AsSpan(start, length).Fill(value);
            return $"plage-remplie:{start}:{length}:{value}";
        }

        private static (byte[] Content, string Description) Truncate(
            byte[] content,
            ref StableRandom random)
        {
            var length = 1 + random.NextInt(content.Length);
            return (content[..length], $"troncature:{length}");
        }

        private static (byte[] Content, string Description) DeleteRange(
            byte[] content,
            ref StableRandom random)
        {
            if (content.Length == 1)
            {
                return (content, "suppression:ignorée");
            }

            var start = random.NextInt(content.Length);
            var maximumLength = Math.Min(4096, content.Length - start);
            var length = 1 + random.NextInt(maximumLength);
            if (length == content.Length)
            {
                length--;
            }

            var result = new byte[content.Length - length];
            content.AsSpan(0, start).CopyTo(result);
            content.AsSpan(start + length).CopyTo(result.AsSpan(start));
            return (result, $"suppression:{start}:{length}");
        }

        private static (byte[] Content, string Description) DuplicateRange(
            byte[] content,
            ref StableRandom random)
        {
            var start = random.NextInt(content.Length);
            var maximumLength = Math.Min(
                Math.Min(4096, content.Length - start),
                MaximumMutationBytes - content.Length);
            if (maximumLength <= 0)
            {
                return (content, "duplication:ignorée");
            }

            var length = 1 + random.NextInt(maximumLength);
            var insertion = random.NextInt(content.Length + 1);
            var result = new byte[content.Length + length];
            content.AsSpan(0, insertion).CopyTo(result);
            content.AsSpan(start, length).CopyTo(result.AsSpan(insertion));
            content.AsSpan(insertion).CopyTo(result.AsSpan(insertion + length));
            return (result, $"duplication:{start}:{length}:{insertion}");
        }

        private static (byte[] Content, string Description) InsertToken(
            byte[] content,
            ref StableRandom random)
        {
            var tokenIndex = random.NextInt(InjectionTokens.Length);
            var token = InjectionTokens[tokenIndex];
            if (content.Length + token.Length > MaximumMutationBytes)
            {
                return (content, "insertion:ignorée");
            }

            var insertion = random.NextInt(content.Length + 1);
            var result = new byte[content.Length + token.Length];
            content.AsSpan(0, insertion).CopyTo(result);
            token.CopyTo(result, insertion);
            content.AsSpan(insertion).CopyTo(result.AsSpan(insertion + token.Length));
            return (result, $"insertion-jeton:{tokenIndex}:{insertion}");
        }

        private static string OverwriteWithToken(
            byte[] content,
            ref StableRandom random)
        {
            var tokenIndex = random.NextInt(InjectionTokens.Length);
            var token = InjectionTokens[tokenIndex];
            var start = random.NextInt(content.Length);
            var length = Math.Min(token.Length, content.Length - start);
            token.AsSpan(0, length).CopyTo(content.AsSpan(start));
            return $"écrasement-jeton:{tokenIndex}:{start}:{length}";
        }
    }

    private struct StableRandom(ulong seed)
    {
        private ulong _state = seed;

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            }

            return (int)(NextUInt64() % checked((ulong)exclusiveMaximum));
        }

        private ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var value = _state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
