using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Générateur local et déterministe de documents de signalement multi-destinataires.
/// </summary>
public sealed class BasicTakedownPackWriter : ITakedownPackWriter
{
    private const double MinimumIocConfidence = 0.5;
    private const string MarkdownContentType = "text/markdown; charset=utf-8";
    private const string JsonContentType = "application/json; charset=utf-8";
    private const string NonRenseigne = "Non renseigné";

    private static readonly JsonSerializerOptions ManifestJsonOptions =
        CreateManifestJsonOptions();

    /// <inheritdoc />
    public TakedownPack Write(TakedownPackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var input = Validate(request);
        var artifacts = new List<TakedownPackArtifact>(request.Recipients.Count + 2)
        {
            new(
                "LISEZ-MOI.md",
                MarkdownContentType,
                WriteReadme(request, input)),
            new(
                "manifest.json",
                JsonContentType,
                WriteManifest(request, input)),
        };

        foreach (var recipient in request.Recipients.Order())
        {
            artifacts.Add(new TakedownPackArtifact(
                FileName(recipient),
                MarkdownContentType,
                WriteRecipientDocument(request, input, recipient),
                recipient));
        }

        return new TakedownPack(
            request.PackId,
            request.PreparedAt,
            request.CampaignReview.CandidateFingerprint,
            artifacts);
    }

    private static ValidatedInput Validate(TakedownPackRequest request)
    {
        if (request.CampaignReview.Verdict != CampaignReviewVerdict.Confirmed)
        {
            throw new InvalidOperationException(
                "Le takedown pack exige une campagne confirmée par une décision humaine.");
        }

        if (request.CampaignReview.DecidedAt > request.PreparedAt)
        {
            throw new InvalidOperationException(
                "Le takedown pack ne peut pas être préparé avant la décision de campagne.");
        }

        var candidateIds = request.CampaignReview.CandidateSnapshot.IncidentIds.ToHashSet();
        var incidentById = request.Incidents.ToDictionary(incident => incident.IncidentId);
        if (!candidateIds.SetEquals(incidentById.Keys))
        {
            throw new InvalidOperationException(
                "Les incidents fournis doivent correspondre exactement à la campagne confirmée.");
        }

        var reviewByIncidentId = request.IncidentReviews.ToDictionary(
            review => review.IncidentId);
        if (!candidateIds.SetEquals(reviewByIncidentId.Keys))
        {
            throw new InvalidOperationException(
                "Chaque incident de la campagne doit posséder une décision humaine individuelle.");
        }

        foreach (var incidentId in candidateIds)
        {
            var review = reviewByIncidentId[incidentId];
            if (review.Verdict != ReviewVerdict.ConfirmedFraud ||
                review.Classification is null or
                    FraudClassification.Unknown or
                    FraudClassification.Suspicious)
            {
                throw new InvalidOperationException(
                    $"L'incident '{incidentId:D}' doit être confirmé comme fraude et catégorisé.");
            }

            if (review.DecidedAt > request.PreparedAt)
            {
                throw new InvalidOperationException(
                    $"Le takedown pack ne peut pas précéder la revue de l'incident '{incidentId:D}'.");
            }

            var evidenceHash = incidentById[incidentId].Evidence.Sha256;
            if (!IsSha256(evidenceHash))
            {
                throw new InvalidOperationException(
                    $"L'incident '{incidentId:D}' ne possède pas de SHA-256 de preuve exploitable.");
            }
        }

        var orderedIncidents = incidentById.Values
            .OrderBy(incident => incident.IncidentId)
            .ToArray();
        var qualifiedIocs = BuildQualifiedIocs(orderedIncidents);

        foreach (var recipient in request.Recipients)
        {
            if (!IsApplicable(recipient, orderedIncidents, qualifiedIocs))
            {
                throw new InvalidOperationException(
                    $"Le destinataire '{recipient}' ne dispose d'aucun élément technique adapté.");
            }
        }

        return new ValidatedInput(
            orderedIncidents,
            reviewByIncidentId,
            qualifiedIocs);
    }

    private static string WriteReadme(
        TakedownPackRequest request,
        ValidatedInput input)
    {
        var output = new StringBuilder();
        output.AppendLine("# Takedown pack Frelon");
        output.AppendLine();
        output.AppendLine("> Dossier préparé localement. Frelon ne l'a envoyé à aucun tiers.");
        output.AppendLine();
        output.AppendLine("## Traçabilité");
        output.AppendLine();
        AddField(output, "Identifiant du pack", request.PackId.ToString("D"));
        AddField(output, "Préparé le", ToUtcText(request.PreparedAt));
        AddField(
            output,
            "Empreinte de campagne",
            request.CampaignReview.CandidateFingerprint);
        AddField(
            output,
            "Revue de campagne",
            request.CampaignReview.ReviewId.ToString("D"));
        AddField(
            output,
            "Incidents inclus",
            input.Incidents.Count.ToString(CultureInfo.InvariantCulture));
        AddField(
            output,
            "Destinataires préparés",
            string.Join(", ", request.Recipients.Order().Select(RecipientLabel)));
        AddField(output, "Note de l'analyste", request.AnalystNotes);
        output.AppendLine();
        output.AppendLine("## Avant toute transmission");
        output.AppendLine();
        output.AppendLine("- Vérifier manuellement l'identité, l'adresse et la politique du destinataire.");
        output.AppendLine("- Relire le document qui lui est destiné et retirer toute donnée non nécessaire.");
        output.AppendLine("- Ne joindre les messages sources qu'en cas de besoin explicite et d'autorisation.");
        output.AppendLine("- Conserver une trace du canal, de la date et du contenu réellement transmis.");
        output.AppendLine();
        output.AppendLine("Le fichier `manifest.json` décrit les décisions et empreintes utilisées. " +
            "Les autres fichiers sont des brouillons adaptés à chaque rôle de destinataire.");
        return output.ToString();
    }

    private static string WriteManifest(
        TakedownPackRequest request,
        ValidatedInput input)
    {
        var manifest = new TakedownManifest(
            request.PackId,
            request.PreparedAt.ToUniversalTime(),
            request.CampaignReview.ReviewId,
            request.CampaignReview.DecidedAt.ToUniversalTime(),
            request.CampaignReview.CandidateFingerprint,
            input.Incidents.Select(incident =>
            {
                var review = input.ReviewByIncidentId[incident.IncidentId];
                return new TakedownManifestIncident(
                    incident.IncidentId,
                    incident.Evidence.Sha256!,
                    review.ReviewId,
                    review.DecidedAt.ToUniversalTime(),
                    review.Classification!.Value);
            }).ToArray(),
            request.Recipients
                .Order()
                .Select(recipient => new TakedownManifestDocument(
                    recipient,
                    FileName(recipient)))
                .ToArray());

        return JsonSerializer.Serialize(manifest, ManifestJsonOptions);
    }

    private static string WriteRecipientDocument(
        TakedownPackRequest request,
        ValidatedInput input,
        TakedownRecipientType recipient)
    {
        var output = new StringBuilder();
        output.AppendLine($"# Signalement préparé — {RecipientLabel(recipient)}");
        output.AppendLine();
        output.AppendLine("> Brouillon local non transmis. Le destinataire réel doit être vérifié par l'analyste.");
        output.AppendLine();
        output.AppendLine("## Objet suggéré");
        output.AppendLine();
        output.AppendLine(
            $"Campagne frauduleuse confirmée — {input.Incidents.Count} incidents — " +
            $"référence {request.CampaignReview.CandidateFingerprint[..12]}");
        output.AppendLine();
        output.AppendLine("## Demande suggérée");
        output.AppendLine();
        output.AppendLine(RequestText(recipient));
        output.AppendLine();

        AddValidation(output, request, input);
        AddEvidence(output, input);

        if (recipient == TakedownRecipientType.EmailProvider)
        {
            AddMailTraces(output, input.Incidents);
        }

        AddIndicators(output, RelevantIocs(recipient, input.QualifiedIocs));

        output.AppendLine("## Contrôles avant envoi");
        output.AppendLine();
        output.AppendLine("- Confirmer que le destinataire est compétent pour les éléments cités.");
        output.AppendLine("- Vérifier ses exigences de forme et ses conditions de traitement.");
        output.AppendLine("- Limiter les données personnelles et pièces jointes au strict nécessaire.");
        output.AppendLine("- Adapter la demande aux faits vérifiés ; ne pas présenter le score automatique comme une preuve.");
        return output.ToString();
    }

    private static void AddValidation(
        StringBuilder output,
        TakedownPackRequest request,
        ValidatedInput input)
    {
        output.AppendLine("## Validations humaines");
        output.AppendLine();
        AddField(output, "Campagne confirmée le", ToUtcText(request.CampaignReview.DecidedAt));
        AddField(output, "Revue de campagne", request.CampaignReview.ReviewId.ToString("D"));
        AddField(output, "Empreinte de composition", request.CampaignReview.CandidateFingerprint);
        AddField(output, "Note du pack", request.AnalystNotes);
        output.AppendLine();

        foreach (var incident in input.Incidents)
        {
            var review = input.ReviewByIncidentId[incident.IncidentId];
            output.AppendLine($"- Incident `{incident.IncidentId:D}`");
            output.AppendLine($"  - Revue : `{review.ReviewId:D}`");
            output.AppendLine($"  - Catégorie : {Escape(ClassificationLabel(review.Classification!.Value))}");
            output.AppendLine($"  - Décidée le : {Escape(ToUtcText(review.DecidedAt))}");
        }

        output.AppendLine();
    }

    private static void AddEvidence(
        StringBuilder output,
        ValidatedInput input)
    {
        output.AppendLine("## Traçabilité des preuves");
        output.AppendLine();
        foreach (var incident in input.Incidents)
        {
            output.AppendLine($"- Incident `{incident.IncidentId:D}`");
            output.AppendLine($"  - Fichier source : {Escape(incident.Evidence.FileName)}");
            output.AppendLine($"  - SHA-256 : `{incident.Evidence.Sha256}`");
            output.AppendLine($"  - Importé le : {Escape(
                incident.Evidence.ImportedAt is null
                    ? null
                    : ToUtcText(incident.Evidence.ImportedAt.Value))}");
        }

        output.AppendLine();
    }

    private static void AddMailTraces(
        StringBuilder output,
        IReadOnlyList<FraudIncident> incidents)
    {
        output.AppendLine("## Traces de messagerie");
        output.AppendLine();
        output.AppendLine("> Ces champs sont déclaratifs et peuvent avoir été falsifiés.");
        output.AppendLine();
        foreach (var incident in incidents)
        {
            output.AppendLine($"### Incident `{incident.IncidentId:D}`");
            output.AppendLine();
            AddField(output, "From", incident.Identity.From);
            AddField(output, "Reply-To", incident.Identity.ReplyTo);
            AddField(output, "Return-Path", incident.Identity.ReturnPath);
            AddField(output, "Message-ID", incident.Identity.MessageId);
            AddField(output, "SPF", incident.Authentication.SpfResult);
            AddField(output, "DKIM", incident.Authentication.DkimResult);
            AddField(output, "DMARC", incident.Authentication.DmarcResult);
            output.AppendLine();
        }
    }

    private static void AddIndicators(
        StringBuilder output,
        IReadOnlyList<QualifiedIoc> indicators)
    {
        output.AppendLine("## Indicateurs techniques pertinents");
        output.AppendLine();

        if (indicators.Count == 0)
        {
            output.AppendLine("Aucun IOC structuré supplémentaire pour ce destinataire.");
            output.AppendLine();
            return;
        }

        foreach (var indicator in indicators)
        {
            output.AppendLine(
                $"- **{Escape(indicator.Type.ToString())}** : {Escape(indicator.Value)} " +
                $"— observé dans {indicator.IncidentIds.Count} incident(s)");
        }

        output.AppendLine();
    }

    private static IReadOnlyList<QualifiedIoc> BuildQualifiedIocs(
        IReadOnlyList<FraudIncident> incidents)
    {
        var result = new Dictionary<string, QualifiedIocBuilder>(StringComparer.Ordinal);

        foreach (var incident in incidents)
        {
            foreach (var ioc in incident.Iocs)
            {
                if (ioc is null ||
                    ioc.Type is IocType.Unknown or IocType.FileName ||
                    string.IsNullOrWhiteSpace(ioc.Value) ||
                    !double.IsFinite(ioc.Confidence) ||
                    ioc.Confidence < MinimumIocConfidence ||
                    ioc.Confidence > 1)
                {
                    continue;
                }

                var value = ioc.Value.Trim();
                var key = $"{(int)ioc.Type}\0{DeduplicationValue(ioc.Type, value)}";
                if (!result.TryGetValue(key, out var builder))
                {
                    builder = new QualifiedIocBuilder(ioc.Type, value);
                    result.Add(key, builder);
                }

                builder.IncidentIds.Add(incident.IncidentId);
            }
        }

        return result.Values
            .Select(builder => new QualifiedIoc(
                builder.Type,
                builder.Value,
                builder.IncidentIds.Order().ToArray()))
            .OrderBy(indicator => indicator.Type)
            .ThenBy(indicator => indicator.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string DeduplicationValue(IocType type, string value)
        => type switch
        {
            IocType.Domain => value.TrimEnd('.').ToLowerInvariant(),
            IocType.Hash => value.ToLowerInvariant(),
            IocType.IpAddress when IPAddress.TryParse(value, out var address) =>
                address.ToString(),
            IocType.Email => NormalizeEmailForDeduplication(value),
            _ => value,
        };

    private static string NormalizeEmailForDeduplication(string value)
    {
        var separatorIndex = value.LastIndexOf('@');
        return separatorIndex <= 0 || separatorIndex == value.Length - 1
            ? value
            : $"{value[..separatorIndex]}@{value[(separatorIndex + 1)..].ToLowerInvariant()}";
    }

    private static IReadOnlyList<QualifiedIoc> RelevantIocs(
        TakedownRecipientType recipient,
        IReadOnlyList<QualifiedIoc> indicators)
        => indicators
            .Where(indicator => recipient switch
            {
                TakedownRecipientType.HostingProvider =>
                    indicator.Type is IocType.Url or IocType.Domain or IocType.IpAddress,
                TakedownRecipientType.DomainRegistrar =>
                    indicator.Type == IocType.Domain,
                TakedownRecipientType.EmailProvider =>
                    indicator.Type == IocType.Email,
                TakedownRecipientType.AntiPhishingService =>
                    indicator.Type is
                        IocType.Url or
                        IocType.Domain or
                        IocType.IpAddress or
                        IocType.Email or
                        IocType.Hash,
                _ => false,
            })
            .ToArray();

    private static bool IsApplicable(
        TakedownRecipientType recipient,
        IReadOnlyList<FraudIncident> incidents,
        IReadOnlyList<QualifiedIoc> indicators)
        => recipient switch
        {
            TakedownRecipientType.EmailProvider => incidents.Any(HasMailTrace),
            _ => RelevantIocs(recipient, indicators).Count != 0,
        };

    private static bool HasMailTrace(FraudIncident incident)
        => new[]
        {
            incident.Identity.From,
            incident.Identity.ReplyTo,
            incident.Identity.ReturnPath,
            incident.Identity.MessageId,
            incident.Authentication.SpfResult,
            incident.Authentication.DkimResult,
            incident.Authentication.DmarcResult,
        }.Any(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsSha256(string? value)
        => value is not null &&
           value.Length == 64 &&
           value.All(Uri.IsHexDigit);

    private static string FileName(TakedownRecipientType recipient)
        => recipient switch
        {
            TakedownRecipientType.HostingProvider => "signalement-hebergeur.md",
            TakedownRecipientType.DomainRegistrar => "signalement-registrar.md",
            TakedownRecipientType.EmailProvider => "signalement-fournisseur-messagerie.md",
            TakedownRecipientType.AntiPhishingService => "signalement-anti-phishing.md",
            _ => throw new ArgumentOutOfRangeException(nameof(recipient), recipient, null),
        };

    private static string RecipientLabel(TakedownRecipientType recipient)
        => recipient switch
        {
            TakedownRecipientType.HostingProvider => "Hébergeur ou opérateur d'infrastructure",
            TakedownRecipientType.DomainRegistrar => "Registrar du domaine",
            TakedownRecipientType.EmailProvider => "Fournisseur de messagerie",
            TakedownRecipientType.AntiPhishingService => "Service anti-phishing",
            _ => throw new ArgumentOutOfRangeException(nameof(recipient), recipient, null),
        };

    private static string RequestText(TakedownRecipientType recipient)
        => recipient switch
        {
            TakedownRecipientType.HostingProvider =>
                "Merci d'examiner les ressources et infrastructures citées, de préserver les éléments " +
                "utiles à votre enquête et d'appliquer, si les faits et votre politique le justifient, " +
                "les mesures permettant de faire cesser leur utilisation frauduleuse.",
            TakedownRecipientType.DomainRegistrar =>
                "Merci d'examiner les domaines cités et leurs données d'enregistrement, puis d'appliquer, " +
                "si les faits et votre politique le justifient, les mesures appropriées contre leur usage frauduleux.",
            TakedownRecipientType.EmailProvider =>
                "Merci d'examiner les traces de messagerie citées afin d'identifier un éventuel compte, " +
                "relais ou mécanisme abusif et d'appliquer les mesures prévues par votre politique.",
            TakedownRecipientType.AntiPhishingService =>
                "Merci d'examiner cette campagne et ses indicateurs afin d'alimenter, si votre procédure " +
                "le permet, vos mécanismes de détection, de blocage ou de coordination.",
            _ => throw new ArgumentOutOfRangeException(nameof(recipient), recipient, null),
        };

    private static string ClassificationLabel(FraudClassification classification)
        => classification switch
        {
            FraudClassification.Spam => "Spam",
            FraudClassification.Phishing => "Hameçonnage",
            FraudClassification.Malware => "Logiciel malveillant",
            FraudClassification.Scam => "Escroquerie",
            FraudClassification.BrandImpersonation => "Usurpation de marque",
            FraudClassification.CredentialTheft => "Vol d'identifiants",
            _ => throw new ArgumentOutOfRangeException(nameof(classification), classification, null),
        };

    private static void AddField(StringBuilder output, string label, string? value)
        => output.AppendLine($"- **{label}** : {Escape(value)}");

    private static string ToUtcText(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string Escape(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NonRenseigne;
        }

        var normalized = string.Concat(
            value.Trim().Select(character => char.IsControl(character) ? ' ' : character));
        var encoded = normalized
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
        var output = new StringBuilder(encoded.Length);

        foreach (var character in encoded)
        {
            if (character is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '(' or ')' or '!' or '|')
            {
                output.Append('\\');
            }

            output.Append(character);
        }

        return output.ToString();
    }

    private static JsonSerializerOptions CreateManifestJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));
        return options;
    }

    private sealed record ValidatedInput(
        IReadOnlyList<FraudIncident> Incidents,
        IReadOnlyDictionary<Guid, IncidentReviewDecision> ReviewByIncidentId,
        IReadOnlyList<QualifiedIoc> QualifiedIocs);

    private sealed record QualifiedIoc(
        IocType Type,
        string Value,
        IReadOnlyList<Guid> IncidentIds);

    private sealed class QualifiedIocBuilder(IocType type, string value)
    {
        public IocType Type { get; } = type;

        public string Value { get; } = value;

        public HashSet<Guid> IncidentIds { get; } = [];
    }

    private sealed record TakedownManifest(
        Guid PackId,
        DateTimeOffset PreparedAt,
        Guid CampaignReviewId,
        DateTimeOffset CampaignReviewDecidedAt,
        string CampaignFingerprint,
        IReadOnlyList<TakedownManifestIncident> Incidents,
        IReadOnlyList<TakedownManifestDocument> Documents);

    private sealed record TakedownManifestIncident(
        Guid IncidentId,
        string EvidenceSha256,
        Guid IncidentReviewId,
        DateTimeOffset IncidentReviewDecidedAt,
        FraudClassification Classification);

    private sealed record TakedownManifestDocument(
        TakedownRecipientType Recipient,
        string FileName);
}
