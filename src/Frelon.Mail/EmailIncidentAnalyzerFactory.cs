using Frelon.Core;

namespace Frelon.Mail;

/// <summary>Construit le pipeline d'analyse locale de référence.</summary>
public static class EmailIncidentAnalyzerFactory
{
    /// <summary>Crée un pipeline complet avec les analyseurs locaux intégrés.</summary>
    public static IEmailIncidentAnalyzer CreateDefault()
        => new BasicEmailIncidentAnalyzer(
            new EmailEvidenceParser(),
            new BasicEmailHeaderAnalyzer(),
            new BasicEmailUrlExtractor(),
            new BasicUrlIocExtractor(),
            new BasicEmailAttachmentAnalyzer(),
            new BasicAttachmentIocExtractor(),
            new BasicIncidentRiskScorer(),
            new CautiousIncidentClassifier());
}
