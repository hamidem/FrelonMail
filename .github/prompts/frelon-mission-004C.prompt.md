# Mission Copilot #004C — Générer iocs.json depuis FraudIncident

## Contexte

Les missions précédentes ont mis en place :

* le modèle métier minimal dans `Frelon.Core` ;
* le parsing minimal d’un `.eml` local dans `Frelon.Mail` ;
* l’analyse simple des headers email ;
* la construction d’un `FraudIncident` minimal ;
* la génération de `incident.json` ;
* la génération de `report.md`.

Cette mission continue le module `Frelon.Reports`.

Frelon reste un outil défensif, local-first, sans réseau, sans interaction offensive et sans base de données à ce stade.

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Créer un générateur JSON dédié aux IOC contenus dans un `FraudIncident`.

Le but est de produire le futur contenu de :

```text
iocs.json
```

Cette mission ne doit pas encore extraire de nouveaux IOC.

Elle doit seulement sérialiser les IOC déjà présents dans `FraudIncident.Iocs`.

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
* Utiliser `System.Text.Json`.
* Ne pas créer de nouveau projet.
* Ne pas modifier la structure de solution.
* Ne pas ajouter de code réseau.
* Ne pas ouvrir d’URL.
* Ne pas exécuter de pièce jointe.
* Ne pas envoyer d’email.
* Ne pas créer de base de données.
* Ne pas créer de CLI.
* Ne pas écrire automatiquement sur disque.
* Ne pas modifier les modèles Core.
* Ne pas extraire encore les URLs.
* Ne pas extraire encore les pièces jointes.
* Ne pas implémenter de scoring.
* Garder le code simple, local, lisible et testable.

## Travail demandé

### 1. Créer une interface `IIocsJsonWriter`

Créer une interface dans `src/Frelon.Reports/`.

Signature suggérée :

```csharp
using Frelon.Core;

namespace Frelon.Reports;

public interface IIocsJsonWriter
{
    string Write(FraudIncident incident);
}
```

### 2. Créer une implémentation `SystemTextJsonIocsJsonWriter`

Créer une classe dans `src/Frelon.Reports/`.

Elle doit :

* prendre un `FraudIncident` ;
* vérifier que l’argument n’est pas null ;
* sérialiser uniquement les IOC présents dans `incident.Iocs` ;
* produire un JSON indenté ;
* utiliser `JsonNamingPolicy.CamelCase` ;
* ne pas modifier l’incident ;
* ne pas écrire sur disque.

Options suggérées :

```csharp
new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

### 3. Forme JSON attendue

Le JSON peut être soit un tableau direct :

```json
[
  {
    "type": "Domain",
    "value": "evil.example.com",
    "confidence": 0.9
  }
]
```

Soit un objet racine plus explicite :

```json
{
  "incidentId": "INC-TEST-001",
  "generatedAt": "2026-07-03T12:00:00Z",
  "iocs": [
    {
      "type": "Domain",
      "value": "evil.example.com",
      "confidence": 0.9
    }
  ]
}
```

Préférer l’objet racine, car il sera plus extensible pour les futurs exports.

### 4. Type de sortie recommandé

Créer un petit modèle interne à `Frelon.Reports`, par exemple :

```csharp
internal sealed record IocsJsonDocument
{
    public required string IncidentId { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<Ioc> Iocs { get; init; } = [];
}
```

Ce type doit rester interne au module `Reports`.

Ne pas l’ajouter à `Frelon.Core`.

### 5. Tests unitaires

Créer un fichier de test, par exemple :

```text
tests/Frelon.Reports.Tests/SystemTextJsonIocsJsonWriterTests.cs
```

Ajouter des tests vérifiant que :

1. `Write` retourne une chaîne JSON non vide ;
2. le JSON contient `incidentId` ;
3. le JSON contient une propriété `iocs` ;
4. le JSON contient les IOC présents dans l’incident ;
5. le JSON conserve le type de l’IOC ;
6. le JSON conserve la valeur de l’IOC ;
7. le JSON conserve la confiance de l’IOC ;
8. si l’incident ne contient aucun IOC, `iocs` est une liste vide ;
9. `Write` lève `ArgumentNullException` si l’incident est null ;
10. le JSON est indenté.

## Exemple d’incident pour les tests

Créer dans les tests un `FraudIncident` contenant au moins trois IOC :

```csharp
Iocs =
[
    new Ioc
    {
        Type = IocType.Domain,
        Value = "evil.example.com",
        Confidence = 0.9
    },
    new Ioc
    {
        Type = IocType.Url,
        Value = "http://evil.example.com/login",
        Confidence = 0.85
    },
    new Ioc
    {
        Type = IocType.Hash,
        Value = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        Confidence = 1.0
    }
]
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

Ne pas extraire encore les IOC depuis les URLs ou les pièces jointes.

Ne pas créer encore d’export SpamAssassin, Rspamd ou Sieve.

Ne pas créer de CLI.

Ne pas écrire automatiquement sur disque.

Ne pas introduire de base de données.

Cette mission sert uniquement à transformer `FraudIncident.Iocs` en JSON dédié.