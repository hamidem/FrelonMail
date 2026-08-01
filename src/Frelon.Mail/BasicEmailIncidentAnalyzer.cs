using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Implémentation de base de <see cref="IEmailIncidentAnalyzer"/>.
/// Orchestre <see cref="IEmailParser"/>, <see cref="IEmailHeaderAnalyzer"/>,
/// <see cref="IEmailUrlExtractor"/>, <see cref="IUrlIocExtractor"/>
/// <see cref="IEmailAttachmentAnalyzer"/>, <see cref="IAttachmentIocExtractor"/>
/// <see cref="IIncidentRiskScorer"/> et <see cref="IIncidentClassifier"/>
/// pour construire un <see cref="FraudIncident"/> minimal à partir d'un flux .eml.
/// Ne fait aucun appel réseau, n'ouvre aucune URL et n'exécute aucune pièce jointe.
/// </summary>
public sealed class BasicEmailIncidentAnalyzer : IEmailIncidentAnalyzer
{
    private readonly IEmailParser              _parser;
    private readonly IEmailHeaderAnalyzer      _headerAnalyzer;
    private readonly IEmailUrlExtractor        _urlExtractor;
    private readonly IUrlIocExtractor          _urlIocExtractor;
    private readonly IEmailAttachmentAnalyzer _attachmentAnalyzer;
    private readonly IAttachmentIocExtractor   _attachmentIocExtractor;
    private readonly IIncidentRiskScorer       _riskScorer;
    private readonly IIncidentClassifier       _classifier;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="BasicEmailIncidentAnalyzer"/>.
    /// </summary>
    /// <param name="parser">Parseur de fichiers .eml.</param>
    /// <param name="headerAnalyzer">Analyseur des headers d'un email parsé.</param>
    /// <param name="urlExtractor">Extracteur d'URLs depuis le corps de l'email.</param>
    /// <param name="urlIocExtractor">Transformateur d'URLs en indicateurs de compromission.</param>
    /// <param name="attachmentAnalyzer">Analyseur local des pièces jointes.</param>
    /// <param name="attachmentIocExtractor">Transformateur de SHA-256 de pièces jointes en IOC.</param>
    /// <param name="riskScorer">Scorer local des risques à partir de l'incident construit.</param>
    /// <param name="classifier">Producteur local d'une piste de classification explicable.</param>
    public BasicEmailIncidentAnalyzer(
        IEmailParser parser,
        IEmailHeaderAnalyzer headerAnalyzer,
        IEmailUrlExtractor urlExtractor,
        IUrlIocExtractor urlIocExtractor,
        IEmailAttachmentAnalyzer attachmentAnalyzer,
        IAttachmentIocExtractor attachmentIocExtractor,
        IIncidentRiskScorer riskScorer,
        IIncidentClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(headerAnalyzer);
        ArgumentNullException.ThrowIfNull(urlExtractor);
        ArgumentNullException.ThrowIfNull(urlIocExtractor);
        ArgumentNullException.ThrowIfNull(attachmentAnalyzer);
        ArgumentNullException.ThrowIfNull(attachmentIocExtractor);
        ArgumentNullException.ThrowIfNull(riskScorer);
        ArgumentNullException.ThrowIfNull(classifier);

        _parser                 = parser;
        _headerAnalyzer         = headerAnalyzer;
        _urlExtractor           = urlExtractor;
        _urlIocExtractor        = urlIocExtractor;
        _attachmentAnalyzer = attachmentAnalyzer;
        _attachmentIocExtractor = attachmentIocExtractor;
        _riskScorer             = riskScorer;
        _classifier             = classifier;
    }

    /// <inheritdoc/>
    public async Task<FraudIncident> AnalyzeAsync(
        Stream emlStream,
        string? sourceFileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emlStream);

        var parsed = await _parser.ParseAsync(emlStream, cancellationToken).ConfigureAwait(false);

        var identity       = _headerAnalyzer.ExtractIdentity(parsed);
        var authentication = _headerAnalyzer.ExtractAuthentication(parsed);
        var receivedChain  = _headerAnalyzer.ExtractReceivedChain(parsed);
        var urls           = _urlExtractor.ExtractUrls(parsed);
        var attachments    = _attachmentAnalyzer.AnalyzeAttachments(parsed);
        var now            = DateTimeOffset.UtcNow;
        var urlIocs        = _urlIocExtractor.ExtractIocs(urls, now);
        var attachmentIocs = _attachmentIocExtractor.ExtractIocs(attachments, now);

        var iocs = new List<Ioc>(urlIocs.Count + attachmentIocs.Count);
        iocs.AddRange(urlIocs);
        iocs.AddRange(attachmentIocs);

        var incident = new FraudIncident
        {
            IncidentId     = Guid.NewGuid(),
            CreatedAt      = now,
            Evidence       = new EvidenceSource
            {
                FileName = string.IsNullOrWhiteSpace(sourceFileName)
                            ? "unknown.eml"
                            : sourceFileName,
                ImportedAt = now,
                Sha256 = parsed.SourceSha256,
            },
            Identity       = identity,
            Authentication = authentication,
            ReceivedChain  = receivedChain,
            Urls           = urls,
            Attachments    = attachments,
            Iocs           = iocs,
            Classification = FraudClassification.Unknown,
            RiskScore      = new RiskScore
            {
                Value = 0,
                Level = RiskLevel.Unknown,
            },
        };

        var riskScoredIncident = incident with
        {
            RiskScore = _riskScorer.Score(incident),
        };

        return riskScoredIncident with
        {
            ClassificationAssessment = _classifier.Assess(riskScoredIncident),
        };
    }
}
