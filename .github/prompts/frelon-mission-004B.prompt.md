# Mission Copilot #004B — Générer report.md depuis FraudIncident

## Contexte

Les missions précédentes ont mis en place :

* le modèle métier minimal dans `Frelon.Core` ;
* les tests unitaires de base de `Frelon.Core` ;
* le parsing minimal d’un `.eml` local dans `Frelon.Mail` ;
* l’analyse simple des headers email ;
* la construction d’un `FraudIncident` minimal depuis un flux `.eml` ;
* la génération JSON d’un `FraudIncident` avec `IIncidentJsonWriter` et `SystemTextJsonIncidentJsonWriter`.

Cette mission continue le module `Frelon.Reports`.

Frelon reste un outil défensif, local-first, sans réseau, sans interaction offensive et sans base de données à ce stade.

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Créer un générateur Markdown capable de transformer un `FraudIncident` en rapport humain lisible.

Le but est de produire le futur contenu de :

```text
report.md
```

Cette mission ne doit pas encore créer de CLI ni écrire obligatoirement sur disque.

## Périmètre autorisé

Copilot peut modifier uniquement :

```text
src/Frelon.Reports/
tests/Frelon.Reports.Tests/
```

Copilot ne doit pas modifier :

```text
src/Frelon.Core/
src/Frelon.Mail/
src/Frelon.Exporters/
src/Frelon.Cli/
tests/Frelon.Core.Tests/
tests/Frelon.Mail.Tests/
```

## Contraintes

* Ne pas ajouter de package NuGet.
* Ne pas créer de nouveau projet.
* Ne pas modifier la structure de solution.
* Ne pas ajouter de code réseau.
* Ne pas ouvrir d’URL.
* Ne pas exécuter de pièce jointe.
* Ne pas envoyer d’email.
* Ne pas créer de base de données.
* Ne pas créer de CLI.
* Ne pas écrire encore automatiquement sur disque.
* Ne pas modifier les modèles Core.
* Garder le code simple, local, lisible et testable.
* Utiliser uniquement les informations déjà disponibles dans `FraudIncident`.

## Travail demandé

### 1. Créer une interface `IIncidentMarkdownReportWriter`

Créer une interface dans `src/Frelon.Reports/`.

Signature suggérée :

```csharp
using Frelon.Core;

namespace Frelon.Reports;

public interface IIncidentMarkdownReportWriter
{
    string Write(FraudIncident incident);
}
```

### 2. Créer une implémentation `BasicIncidentMarkdownReportWriter`

Créer une classe dans `src/Frelon.Reports/`.

Elle doit :

* prendre un `FraudIncident` ;
* vérifier que l’argument n’est pas null ;
* produire une chaîne Markdown lisible ;
* structurer le rapport par sections ;
* ne pas faire de logique de scoring ;
* ne pas modifier l’incident ;
* ne pas écrire sur disque.

### 3. Structure Markdown attendue

Le rapport doit contenir au minimum les sections suivantes :

```markdown
# Rapport d’incident Frelon

## Résumé

## Preuve source

## Identité déclarée

## Authentification

## Chaîne Received

## URLs détectées

## Pièces jointes détectées

## IOC

## Actions recommandées
```

### 4. Contenu minimal attendu

Le rapport doit afficher, si disponible :

* `IncidentId`
* `CreatedAt`
* nom du fichier source
* classification
* score de risque
* niveau de risque
* sujet
* From
* Reply-To
* Return-Path
* Message-ID
* Authentication-Results brut
* SPF
* DKIM
* DMARC
* headers `Received`
* URLs détectées
* pièces jointes détectées
* IOC
* actions recommandées

Si une collection est vide, afficher une phrase simple :

```text
Aucun élément détecté.
```

ou équivalent.

Si une valeur est absente, afficher :

```text
Non renseigné
```

ou équivalent.

### 5. Tests unitaires

Créer ou compléter un fichier de test, par exemple :

```text
tests/Frelon.Reports.Tests/BasicIncidentMarkdownReportWriterTests.cs
```

Ajouter des tests vérifiant que :

1. `Write` retourne une chaîne Markdown non vide ;
2. le rapport contient le titre `# Rapport d’incident Frelon` ;
3. le rapport contient l’identifiant d’incident ;
4. le rapport contient la classification ;
5. le rapport contient le nom du fichier source ;
6. le rapport contient les informations d’identité disponibles ;
7. le rapport contient les informations d’authentification disponibles ;
8. le rapport contient les headers `Received` si présents ;
9. le rapport affiche une phrase claire lorsque les collections URLs, pièces jointes, IOC ou actions sont vides ;
10. `Write` lève `ArgumentNullException` si l’incident est null.

## Exemple d’incident pour les tests

Créer dans les tests un `FraudIncident` minimal similaire à ceux utilisés dans les tests JSON.

Exemple conceptuel :

```csharp
var incident = new FraudIncident
{
    IncidentId = "INC-TEST-001",
    CreatedAt = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
    Evidence = new EvidenceSource
    {
        FileName = "suspicious.eml",
        ImportedAt = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero)
    },
    Identity = new MailIdentity
    {
        From = "Fake Support <support@example.net>",
        ReplyTo = "reply@example.net",
        ReturnPath = "<bounce@example.net>",
        MessageId = "<abc123@example.net>",
        Subject = "Suspicious login attempt"
    },
    Authentication = new AuthenticationAssessment
    {
        AuthenticationResultsRaw = "spf=pass; dkim=fail; dmarc=none",
        SpfResult = "pass",
        DkimResult = "fail",
        DmarcResult = "none"
    },
    ReceivedChain =
    [
        new ReceivedHop
        {
            Position = 0,
            RawValue = "from first.example by mx.example.org"
        }
    ],
    Classification = FraudClassification.Unknown,
    RiskScore = new RiskScore
    {
        Value = 0,
        Level = RiskLevel.Unknown
    }
};
```

Adapter l’exemple aux propriétés réellement disponibles dans les modèles existants.

## Critères d’acceptation

La mission est terminée si :

* `Frelon.Reports` compile ;
* `Frelon.Reports.Tests` compile ;
* tous les tests existants continuent de passer ;
* les nouveaux tests passent ;
* aucun package NuGet n’a été ajouté ;
* aucun fichier hors périmètre n’a été modifié ;
* aucun modèle Core n’a été modifié ;
* aucun code réseau n’a été ajouté ;
* aucune fonctionnalité hors mission n’a été introduite.

## Important

Ne pas modifier le générateur JSON sauf nécessité de compilation.

Ne pas créer encore `iocs.json`.

Ne pas créer encore de CLI.

Ne pas écrire automatiquement sur disque.

Ne pas introduire de base de données.

Ne pas créer d’exports SpamAssassin, Rspamd ou Sieve.

Cette mission sert uniquement à transformer un `FraudIncident` en rapport Markdown lisible.