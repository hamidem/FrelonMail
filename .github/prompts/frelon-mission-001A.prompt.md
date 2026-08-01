# Mission Copilot #001A — Modèle métier minimal dans Frelon.Core

## Contexte

La solution Frelon existe déjà.

Le projet `Frelon.Core` doit contenir le modèle métier minimal de Frelon.

Frelon est un outil défensif d’analyse de mails frauduleux. Il transforme des emails suspects en preuves, indicateurs, rapports et actions défensives préparées.

Cette mission doit respecter les instructions générales du projet définies dans :

```text
.github/copilot-instructions.md
```

## Objectif

Créer uniquement les types métier minimaux nécessaires au modèle `FraudIncident` dans le projet `Frelon.Core`.

Le but de cette mission n’est pas encore d’analyser un vrai email.

Le but est d’obtenir un socle métier propre, compilable et extensible.

## Périmètre autorisé

Copilot peut modifier uniquement :

```text
src/Frelon.Core/
```

Copilot peut créer ce dossier si nécessaire :

```text
src/Frelon.Core/Models/
```

Copilot peut supprimer le fichier placeholder suivant s’il existe :

```text
src/Frelon.Core/Class1.cs
```

Copilot ne doit pas modifier :

```text
src/Frelon.Mail/
src/Frelon.Reports/
src/Frelon.Exporters/
src/Frelon.Cli/
tests/
```

## Contraintes

* Ne pas créer de nouveau projet.
* Ne pas modifier la structure de solution.
* Ne pas ajouter de package NuGet.
* Ne pas ajouter de code réseau.
* Ne pas ajouter de code d’envoi d’email.
* Ne pas ajouter de parsing `.eml`.
* Ne pas ajouter de CLI.
* Ne pas ajouter de dashboard.
* Ne pas créer de base de données.
* Ne pas anticiper les futures missions.
* Garder `Frelon.Core` indépendant de l’infrastructure.
* Utiliser le namespace `Frelon.Core`.
* Garder les nullable reference types activés.
* Utiliser des records lorsque c’est pertinent.
* Préférer des propriétés explicites avec `required` et `init` plutôt que des records positionnels pour les types qui risquent d’évoluer.
* Les collections publiques doivent être en lecture seule autant que possible.
* Le code doit compiler.

## Types à créer

Créer les types suivants dans `src/Frelon.Core/Models/` :

```text
FraudIncident
EvidenceSource
MailIdentity
AuthenticationAssessment
ReceivedHop
UrlIndicator
AttachmentIndicator
Ioc
FraudClassification
RiskScore
RecommendedAction
```

## Modèle attendu

### FraudIncident

`FraudIncident` est l’agrégat principal.

Il doit contenir au minimum :

* `IncidentId`
* `CreatedAt`
* `Evidence`
* `Identity`
* `Authentication`
* `ReceivedChain`
* `Urls`
* `Attachments`
* `Iocs`
* `Classification`
* `RiskScore`
* `RecommendedActions`

### EvidenceSource

Représente la preuve source.

Champs suggérés :

* chemin du fichier source si disponible ;
* nom du fichier ;
* hash éventuel du fichier source ;
* date d’import ou de réception si disponible.

Ne pas lire le fichier dans cette mission.

### MailIdentity

Représente les identités déclarées dans le mail.

Champs suggérés :

* `From`
* `ReplyTo`
* `ReturnPath`
* `MessageId`
* `Subject`

### AuthenticationAssessment

Représente les résultats SPF, DKIM et DMARC.

Champs suggérés :

* `SpfResult`
* `DkimResult`
* `DmarcResult`
* `AuthenticationResultsRaw`
* booléen ou score indiquant si l’authentification paraît suspecte.

Les valeurs peuvent rester simples pour cette première version.

### ReceivedHop

Représente un relais dans la chaîne `Received`.

Champs suggérés :

* `Position`
* `From`
* `By`
* `With`
* `IpAddress`
* `Timestamp`
* `RawValue`

### UrlIndicator

Représente une URL extraite.

Champs suggérés :

* `RawValue`
* `NormalizedValue`
* `Host`
* `Scheme`
* `IsSuspicious`
* `Reasons`

Ne pas ouvrir l’URL.

Ne pas faire d’appel réseau.

### AttachmentIndicator

Représente une pièce jointe détectée.

Champs suggérés :

* `FileName`
* `ContentType`
* `SizeBytes`
* `Sha256`
* `IsSuspicious`
* `Reasons`

Ne pas exécuter la pièce jointe.

Ne pas écrire la pièce jointe sur disque.

### Ioc

Représente un indicateur de compromission.

Champs suggérés :

* `Type`
* `Value`
* `Confidence`
* `Source`
* `FirstSeen`

Les types d’IOC peuvent être représentés par une enum.

### FraudClassification

Représente la classification de l’incident.

Valeurs suggérées :

* `Unknown`
* `Spam`
* `Phishing`
* `Malware`
* `Scam`
* `BrandImpersonation`
* `CredentialTheft`
* `Suspicious`

### RiskScore

Représente le score de risque.

Champs suggérés :

* `Value`
* `Level`
* `Reasons`

Le score peut être simple pour cette mission.

Valeurs possibles de niveau :

* `Unknown`
* `Low`
* `Medium`
* `High`
* `Critical`

### RecommendedAction

Représente une action défensive recommandée.

Champs suggérés :

* `Type`
* `Label`
* `Description`
* `RequiresHumanValidation`

Valeurs possibles de type :

* `None`
* `ReviewManually`
* `GenerateReport`
* `ExportIocs`
* `CreateLocalRule`
* `PrepareAbuseReport`
* `PrepareSignalSpamReport`
* `PreparePhishingInitiativeReport`

Aucune action ne doit être exécutée dans cette mission.

## Exemple de forme attendue

Utiliser une forme proche de celle-ci lorsque pertinent :

```csharp
namespace Frelon.Core;

public sealed record MailIdentity
{
    public string? From { get; init; }
    public string? ReplyTo { get; init; }
    public string? ReturnPath { get; init; }
    public string? MessageId { get; init; }
    public string? Subject { get; init; }
}
```

Et pour l’agrégat principal :

```csharp
namespace Frelon.Core;

public sealed record FraudIncident
{
    public required string IncidentId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public required EvidenceSource Evidence { get; init; }
    public required MailIdentity Identity { get; init; }
    public required AuthenticationAssessment Authentication { get; init; }

    public IReadOnlyList<ReceivedHop> ReceivedChain { get; init; } = [];
    public IReadOnlyList<UrlIndicator> Urls { get; init; } = [];
    public IReadOnlyList<AttachmentIndicator> Attachments { get; init; } = [];
    public IReadOnlyList<Ioc> Iocs { get; init; } = [];

    public required FraudClassification Classification { get; init; }
    public required RiskScore RiskScore { get; init; }

    public IReadOnlyList<RecommendedAction> RecommendedActions { get; init; } = [];
}
```

## Critères d’acceptation

La mission est terminée si :

* `Frelon.Core` compile ;
* les 11 types demandés existent ;
* `Class1.cs` a été supprimé s’il existait ;
* aucun package NuGet n’a été ajouté ;
* aucun code réseau n’a été ajouté ;
* aucun code d’envoi d’email n’a été ajouté ;
* aucune fonctionnalité hors modèle métier n’a été ajoutée ;
* le modèle `FraudIncident` peut être instancié par un futur test unitaire.

## Important

Ne pas créer les tests dans cette mission.

Les tests feront l’objet de la Mission Copilot #001B.

Ne pas implémenter l’analyse `.eml`.

Ne pas implémenter le scoring réel.

Ne pas implémenter les exports.

Ne pas implémenter les signalements.
