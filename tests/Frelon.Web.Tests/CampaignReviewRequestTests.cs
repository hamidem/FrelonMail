using System.Text.Json;
using System.Text.Json.Serialization;
using Frelon.Core;

namespace Frelon.Web.Tests;

/// <summary>Vérifie le contrat JSON utilisé pour protéger les revues de campagne périmées.</summary>
public sealed class CampaignReviewRequestTests
{
    [Fact]
    public void Json_RestaureLeSnapshotExactAfficheDansLeCockpit()
    {
        var first = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var second = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var candidate = new CampaignCandidate(
            [first, second],
            new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero),
            [
                new IncidentCorrelationLink(
                    first,
                    second,
                    [new SharedIocMatch(IocType.Hash, new string('a', 64), 100)])
            ]);
        var request = new CampaignReviewRequest(
            candidate,
            CampaignReviewVerdict.Confirmed,
            "Rapprochement vérifié.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));

        var json = JsonSerializer.Serialize(request, options);
        var restored = JsonSerializer.Deserialize<CampaignReviewRequest>(json, options);

        Assert.NotNull(restored);
        Assert.NotNull(restored.CandidateSnapshot);
        Assert.True(candidate.HasSameSnapshotAs(restored.CandidateSnapshot));
        Assert.Equal(CampaignReviewVerdict.Confirmed, restored.Verdict);
        Assert.Equal("Rapprochement vérifié.", restored.Notes);
    }
}
