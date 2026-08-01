using Frelon.Core;
using Xunit;

namespace Frelon.Mail.Tests;

/// <summary>
/// Tests de <see cref="BasicUrlIocExtractor"/>.
/// </summary>
public class BasicUrlIocExtractorTests
{
    private static BasicUrlIocExtractor CreateExtractor() => new();

    [Fact]
    public void ExtractIocs_ListeVideProduitListeVide()
    {
        var extractor = CreateExtractor();

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs([], DateTimeOffset.UtcNow);

        Assert.Empty(iocs);
    }

    [Fact]
    public void ExtractIocs_UneUrlProduitUnIocUrl()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/login",
                NormalizedValue = "https://evil.example.com/login",
                Host            = "evil.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        Assert.Contains(iocs, i => i.Type == IocType.Url);
    }

    [Fact]
    public void ExtractIocs_UneUrlAvecHostProduitUnIocDomain()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/login",
                NormalizedValue = "https://evil.example.com/login",
                Host            = "evil.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        Assert.Contains(iocs, i => i.Type == IocType.Domain);
    }

    [Fact]
    public void ExtractIocs_UneUrlAvecIpBruteProduitUnIocIpEtPasUnDomaine()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue = "http://203.0.113.10/login",
                NormalizedValue = "http://203.0.113.10/login",
                Host = "203.0.113.10"
            }
        };

        var iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        var ip = Assert.Single(iocs, ioc => ioc.Type == IocType.IpAddress);
        Assert.Equal("203.0.113.10", ip.Value);
        Assert.DoesNotContain(iocs, ioc => ioc.Type == IocType.Domain);
    }

    [Fact]
    public void ExtractIocs_ValeurIocUrlCorrespondANormalizedValue()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/login",
                NormalizedValue = "https://evil.example.com/login",
                Host            = "evil.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        Ioc urlIoc = Assert.Single(iocs, i => i.Type == IocType.Url);
        Assert.Equal("https://evil.example.com/login", urlIoc.Value);
    }

    [Fact]
    public void ExtractIocs_ValeurIocUrlUtiliseRawValueSiNormalizedValueInexploitable()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/login",
                NormalizedValue = null,
                Host            = "evil.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        Ioc urlIoc = Assert.Single(iocs, i => i.Type == IocType.Url);
        Assert.Equal("https://evil.example.com/login", urlIoc.Value);
    }

    [Fact]
    public void ExtractIocs_FirstSeenCorrespondExactementAObservedAt()
    {
        var extractor = CreateExtractor();
        var observedAt = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/login",
                NormalizedValue = "https://evil.example.com/login",
                Host            = "evil.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, observedAt);

        Assert.All(iocs, i => Assert.Equal(observedAt, i.FirstSeen));
    }

    [Fact]
    public void ExtractIocs_ConfianceParDefautVautZeroPointCinq()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/login",
                NormalizedValue = "https://evil.example.com/login",
                Host            = "evil.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        Assert.All(iocs, i => Assert.Equal(0.5, i.Confidence));
    }

    [Fact]
    public void ExtractIocs_PlusieursUrlsDuMemeDomaineNeProduisentQuUnSeulIocDomain()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/login",
                NormalizedValue = "https://evil.example.com/login",
                Host            = "evil.example.com",
            },
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/reset",
                NormalizedValue = "https://evil.example.com/reset",
                Host            = "evil.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        Assert.Single(iocs, i => i.Type == IocType.Domain);
        Assert.Equal(2, iocs.Count(i => i.Type == IocType.Url));
    }

    [Fact]
    public void ExtractIocs_DeuxUrlsDifferentesProduisentDeuxIocUrl()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/login",
                NormalizedValue = "https://evil.example.com/login",
                Host            = "evil.example.com",
            },
            new UrlIndicator
            {
                RawValue        = "https://other.example.com/reset",
                NormalizedValue = "https://other.example.com/reset",
                Host            = "other.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        Assert.Equal(2, iocs.Count(i => i.Type == IocType.Url));
    }

    [Fact]
    public void ExtractIocs_DeduplicationDomainEstInsensibleALaCasse()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://Evil.Example.Com/login",
                NormalizedValue = "https://Evil.Example.Com/login",
                Host            = "Evil.Example.Com",
            },
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/reset",
                NormalizedValue = "https://evil.example.com/reset",
                Host            = "evil.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        Assert.Single(iocs, i => i.Type == IocType.Domain);
        Assert.Equal(2, iocs.Count(i => i.Type == IocType.Url));
    }

    [Fact]
    public void ExtractIocs_NeFaitAucunAppelReseau()
    {
        var extractor = CreateExtractor();
        var urls = new[]
        {
            new UrlIndicator
            {
                RawValue        = "https://evil.example.com/login",
                NormalizedValue = "https://evil.example.com/login",
                Host            = "evil.example.com",
            },
        };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(urls, DateTimeOffset.UtcNow);

        Assert.Single(iocs, i => i.Type == IocType.Domain);
    }

    [Fact]
    public void ExtractIocs_UrlsDifferantParLaCasseDuPathRestentDistinctes()
    {
        var extractor = CreateExtractor();

        var urls = new[]
        {
        new UrlIndicator
        {
            RawValue = "https://evil.example.com/Login",
            NormalizedValue = "https://evil.example.com/Login",
            Host = "evil.example.com",
        },
        new UrlIndicator
        {
            RawValue = "https://evil.example.com/login",
            NormalizedValue = "https://evil.example.com/login",
            Host = "evil.example.com",
        },
    };

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(
            urls,
            DateTimeOffset.UtcNow);

        Assert.Equal(2, iocs.Count(i => i.Type == IocType.Url));
        Assert.Single(iocs, i => i.Type == IocType.Domain);
    }
}
