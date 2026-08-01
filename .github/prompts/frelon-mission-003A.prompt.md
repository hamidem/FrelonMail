# Mission Copilot #003A — Construire un FraudIncident minimal depuis un email parsé

## Contexte

Les missions précédentes ont mis en place :

* le modèle métier minimal dans `Frelon.Core` ;
* les tests unitaires de base de `Frelon.Core` ;
* `ParsedEmail`, `ParsedEmailHeader`, `IEmailParser` et `BasicEmailParser` dans `Frelon.Mail` ;
* `IEmailHeaderAnalyzer` et `BasicEmailHeaderAnalyzer` dans `Frelon.Mail` ;
* des tests validant l’extraction des headers d’identité, d’authentification et `Received`.

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Créer un premier service d’analyse capable de construire un `FraudIncident` minimal à partir d’un flux `.eml`.

Cette mission doit relier proprement :

```text
IEmailParser
→ ParsedEmail
→ IEmailHeaderAnalyzer
→ FraudIncident
```

Le but n’est pas encore de faire une classification intelligente.

Le but est d’obtenir un incident structuré, minimal, testable et exploitable par les futures missions.

## Périmètre autorisé

Copilot peut modifier uniquement :

```text
src/Frelon.Mail/
tests/Frelon.Mail.Tests/
```

Copilot ne doit pas modifier :

```text
src/Frelon.Core/
src/Frelon.Reports/
src/Frelon.Exporters/
src/Frelon.Cli/
tests/Frelon.Core.Tests/
tests/Frelon.Reports.Tests/
```

## Contraintes

* Ne pas ajouter de package NuGet.
* Ne pas utiliser MimeKit.
* Ne pas créer de nouveau projet.
* Ne pas modifier la structure de solution.
* Ne pas ajouter de code réseau.
* Ne pas ouvrir d’URL.
* Ne pas exécuter de pièce jointe.
* Ne pas envoyer d’email.
* Ne pas implémenter IMAP.
* Ne pas créer de dashboard.
* Ne pas extraire encore les URLs du body.
* Ne pas hasher encore les pièces jointes.
* Ne pas produire encore de rapport Markdown.
* Ne pas produire encore de JSON.
* Garder le code simple, local, lisible et testable.

## Travail demandé

### 1. Créer une interface `IEmailIncidentAnalyzer`

Créer une interface dans `src/Frelon.Mail/`.

Signature suggérée :

```csharp
using Frelon.Core;

namespace Frelon.Mail;

public interface IEmailIncidentAnalyzer
{
    Task<FraudIncident> AnalyzeAsync(
        Stream emlStream,
        string? sourceFileName = null,
        CancellationToken cancellationToken = default);
}
```

Adapter légèrement si nécessaire selon les types existants.

### 2. Créer une implémentation `BasicEmailIncidentAnalyzer`

Créer une classe `BasicEmailIncidentAnalyzer`.

Elle doit dépendre de :

```text
IEmailParser
IEmailHeaderAnalyzer
```

Elle doit :

1. parser le flux `.eml` avec `IEmailParser` ;
2. extraire `MailIdentity` avec `IEmailHeaderAnalyzer` ;
3. extraire `AuthenticationAssessment` avec `IEmailHeaderAnalyzer` ;
4. extraire la chaîne `Received` avec `IEmailHeaderAnalyzer` ;
5. construire un `FraudIncident` minimal.

### 3. Valeurs minimales du FraudIncident

Le `FraudIncident` généré doit contenir :

* un `IncidentId` non vide ;
* une date `CreatedAt` ;
* une `EvidenceSource` minimale ;
* une `MailIdentity` extraite des headers ;
* une `AuthenticationAssessment` extraite des headers ;
* une `ReceivedChain` extraite des headers ;
* une `Classification` par défaut ;
* un `RiskScore` par défaut ;
* des collections vides pour les URLs, pièces jointes, IOC et actions recommandées.

### 4. IncidentId

Pour cette mission, l’identifiant peut être simple.

Exemples acceptables :

```text
FRL-20260703-000001
```

ou :

```text
Guid.NewGuid().ToString("N")
```

Choisir l’option la plus simple.

Ne pas créer de générateur complexe dans cette mission.

### 5. EvidenceSource

Remplir `EvidenceSource` avec les informations disponibles.

Si `sourceFileName` est fourni, le placer dans le champ adapté du modèle existant.

Ne pas calculer encore de hash du fichier source.

Ne pas lire de chemin disque dans cette mission.

### 6. Classification par défaut

Utiliser une classification prudente.

Exemples possibles selon l’enum existante :

```text
FraudClassification.Unknown
```

ou :

```text
FraudClassification.Suspicious
```

Préférer `Unknown` si disponible.

### 7. RiskScore par défaut

Créer un score de risque neutre ou inconnu.

Exemples possibles selon le modèle existant :

```text
Value = 0
Level = RiskLevel.Unknown
```

Ne pas implémenter encore de scoring réel.

## Tests à ajouter

Créer un fichier de test, par exemple :

```text
tests/Frelon.Mail.Tests/BasicEmailIncidentAnalyzerTests.cs
```

Ajouter des tests vérifiant que :

1. `AnalyzeAsync` retourne un `FraudIncident` non null depuis un `.eml` minimal ;
2. l’incident contient un `IncidentId` non vide ;
3. l’incident contient une date `CreatedAt` renseignée ;
4. l’identité du mail est correctement reportée dans `FraudIncident.Identity` ;
5. les informations `Authentication-Results` sont reportées dans `FraudIncident.Authentication` ;
6. les headers `Received` sont reportés dans `FraudIncident.ReceivedChain` ;
7. les collections `Urls`, `Attachments`, `Iocs` et `RecommendedActions` sont vides ;
8. la classification par défaut est `Unknown` ou équivalent ;
9. le score de risque par défaut est neutre ou inconnu ;
10. aucun appel réseau n’est effectué ;
11. aucune URL n’est ouverte ;
12. aucune pièce jointe n’est exécutée.

## Exemple de .eml pour les tests

```text
Return-Path: <bounce@example.net>
Received: from first.example by mx.example.org
Received: from second.example by first.example
Authentication-Results: mx.example.org; spf=pass smtp.mailfrom=example.net; dkim=fail; dmarc=none
From: Fake Support <support@example.net>
Reply-To: reply@example.net
Message-ID: <abc123@example.net>
Subject: Suspicious login attempt

Hello.
```

## Critères d’acceptation

La mission est terminée si :

* `Frelon.Mail` compile ;
* `Frelon.Mail.Tests` compile ;
* tous les tests existants continuent de passer ;
* les nouveaux tests passent ;
* aucun package NuGet n’a été ajouté ;
* aucun fichier hors périmètre n’a été modifié ;
* aucun code réseau n’a été ajouté ;
* aucune fonctionnalité hors mission n’a été introduite.

## Important

Ne pas implémenter encore le scoring.

Ne pas extraire les URLs.

Ne pas traiter les pièces jointes.

Ne pas produire de JSON.

Ne pas produire de Markdown.

Ne pas créer de CLI.

Cette mission sert uniquement à assembler un premier `FraudIncident` minimal à partir des briques déjà créées.