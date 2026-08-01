# Mission Copilot #004A — Générer incident.json depuis FraudIncident

## Contexte

Les missions précédentes ont mis en place :

* le modèle métier minimal dans `Frelon.Core` ;
* les tests unitaires de base de `Frelon.Core` ;
* le parsing minimal d’un `.eml` local dans `Frelon.Mail` ;
* l’analyse simple des headers email ;
* la construction d’un `FraudIncident` minimal depuis un flux `.eml`.

Cette mission démarre le module `Frelon.Reports`.

Frelon reste un outil défensif, local-first, sans réseau, sans interaction offensive et sans base de données à ce stade.

Cette mission doit respecter :

```text id="mxrljx"
.github/copilot-instructions.md
```

## Objectif

Créer un générateur JSON capable de transformer un `FraudIncident` en JSON lisible.

Le but est de produire le futur contenu de :

```text id="g8bqjo"
incident.json
```

Cette mission ne doit pas encore créer de CLI ni écrire obligatoirement sur disque.

## Périmètre autorisé

Copilot peut modifier uniquement :

```text id="359ti4"
src/Frelon.Reports/
tests/Frelon.Reports.Tests/
```

Copilot ne doit pas modifier :

```text id="9qaum4"
src/Frelon.Core/
src/Frelon.Mail/
src/Frelon.Exporters/
src/Frelon.Cli/
tests/Frelon.Core.Tests/
tests/Frelon.Mail.Tests/
```

## Contraintes

* Ne pas ajouter de package NuGet.
* Utiliser `System.Text.Json`.
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

## Travail demandé

### 1. Créer une interface `IIncidentJsonWriter`

Créer une interface dans `src/Frelon.Reports/`.

Signature suggérée :

```csharp id="0b2wjs"
using Frelon.Core;

namespace Frelon.Reports;

public interface IIncidentJsonWriter
{
    string Write(FraudIncident incident);
}
```

### 2. Créer une implémentation `SystemTextJsonIncidentJsonWriter`

Créer une classe dans `src/Frelon.Reports/`.

Elle doit :

* prendre un `FraudIncident` ;
* vérifier que l’argument n’est pas null ;
* sérialiser l’incident avec `System.Text.Json` ;
* produire un JSON indenté ;
* utiliser une politique de nommage claire et stable.

Options suggérées :

```csharp id="4aavmc"
new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
}
```

Ne pas ajouter de convertisseurs complexes dans cette mission sauf si nécessaire à la compilation.

### 3. Tests unitaires

Créer un fichier de test, par exemple :

```text id="xppp6c"
tests/Frelon.Reports.Tests/SystemTextJsonIncidentJsonWriterTests.cs
```

Ajouter des tests vérifiant que :

1. `Write` retourne une chaîne JSON non vide ;
2. le JSON contient `incidentId` ;
3. le JSON contient `createdAt` ;
4. le JSON contient `evidence` ;
5. le JSON contient `identity` ;
6. le JSON contient `authentication` ;
7. le JSON contient `classification` ;
8. le JSON contient `riskScore` ;
9. le JSON est indenté ;
10. `Write` lève une exception claire si l’incident est null.

## Exemple d’incident pour les tests

Créer dans les tests un `FraudIncident` minimal similaire à ceux utilisés dans `Frelon.Core.Tests`.

Exemple conceptuel :

```csharp id="bca4k4"
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
        Subject = "Suspicious login attempt"
    },
    Authentication = new AuthenticationAssessment
    {
        AuthenticationResultsRaw = "spf=pass; dkim=fail; dmarc=none",
        SpfResult = "pass",
        DkimResult = "fail",
        DmarcResult = "none"
    },
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

Ne pas créer encore de rapport Markdown.

Ne pas créer encore `iocs.json`.

Ne pas créer encore de CLI.

Ne pas écrire automatiquement sur disque.

Ne pas introduire de base de données.

Cette mission sert uniquement à transformer un `FraudIncident` en JSON lisible.