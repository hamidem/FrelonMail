# Mission Copilot #005A — Extraire les URLs depuis un ParsedEmail

## Contexte

Les missions précédentes ont mis en place :

* le modèle métier minimal dans `Frelon.Core` ;
* le parsing minimal d’un `.eml` local dans `Frelon.Mail` ;
* l’analyse simple des headers email ;
* la construction d’un `FraudIncident` minimal ;
* la génération de `incident.json` ;
* la génération de `report.md` ;
* la génération de `iocs.json`.

Cette mission démarre l’extraction défensive des URLs.

Frelon reste un outil défensif, local-first, sans réseau, sans interaction offensive et sans base de données à ce stade.

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Créer un extracteur d’URLs capable d’analyser le contenu déjà parsé d’un email et de produire une liste de `UrlIndicator`.

Cette mission ne doit pas encore intégrer ces URLs dans `FraudIncident`.

Elle doit uniquement créer et tester l’extracteur isolé.

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
* Ne jamais ouvrir les URLs.
* Ne jamais faire de requête HTTP.
* Ne jamais résoudre les domaines en DNS.
* Ne jamais vérifier si une URL existe.
* Ne pas exécuter de pièce jointe.
* Ne pas envoyer d’email.
* Ne pas créer de base de données.
* Ne pas créer de CLI.
* Ne pas modifier les modèles Core.
* Ne pas modifier `BasicEmailIncidentAnalyzer` dans cette mission.
* Garder le code simple, local, lisible et testable.

## Travail demandé

### 1. Créer une interface `IEmailUrlExtractor`

Créer une interface dans `src/Frelon.Mail/`.

Signature suggérée :

```csharp
using Frelon.Core;

namespace Frelon.Mail;

public interface IEmailUrlExtractor
{
    IReadOnlyList<UrlIndicator> ExtractUrls(ParsedEmail email);
}
```

Adapter légèrement si nécessaire selon les types existants.

### 2. Créer une implémentation `BasicEmailUrlExtractor`

Créer une classe `BasicEmailUrlExtractor`.

Elle doit :

* prendre un `ParsedEmail` ;
* vérifier que l’argument n’est pas null ;
* extraire les URLs présentes dans `BodyText` ;
* extraire les URLs présentes dans `BodyHtml` si disponible ;
* retourner une liste de `UrlIndicator` ;
* ne pas ouvrir les URLs ;
* ne pas faire de requête réseau.

### 3. URLs à détecter

Détecter au minimum les formes simples :

```text
http://example.com
https://example.com/login
http://sub.example.com/path?x=1
https://example.com/#fragment
```

Pour cette mission, l’extraction peut être basée sur une expression régulière simple.

Ne pas chercher à couvrir tous les cas RFC.

### 4. Normalisation minimale

Pour chaque URL détectée, remplir si possible :

* `RawValue`
* `NormalizedValue`
* `Host`
* `Scheme`
* `IsSuspicious`
* `Reasons`

Règles minimales :

* `RawValue` contient la valeur détectée ;
* `NormalizedValue` peut être identique à `RawValue` pour cette mission ;
* `Host` est extrait avec `Uri.TryCreate` si possible ;
* `Scheme` vaut `http` ou `https` si détecté ;
* `IsSuspicious` peut rester `false` par défaut ;
* `Reasons` peut rester vide.

### 5. Nettoyage simple

L’extracteur doit éviter d’inclure dans l’URL les ponctuations finales courantes :

```text
.
,
;
:
)
]
}
"
'
```

Exemple :

```text
Consultez https://example.com/login.
```

Doit produire :

```text
https://example.com/login
```

et non :

```text
https://example.com/login.
```

### 6. Déduplication

Si la même URL apparaît plusieurs fois dans le corps texte ou HTML, elle ne doit apparaître qu’une fois dans le résultat.

La déduplication peut être faite sur `NormalizedValue`, avec comparaison insensible à la casse.

### 7. Tests unitaires

Créer un fichier de test, par exemple :

```text
tests/Frelon.Mail.Tests/BasicEmailUrlExtractorTests.cs
```

Ajouter des tests vérifiant que :

1. `ExtractUrls` retourne une liste vide si aucun body ne contient d’URL ;
2. `ExtractUrls` extrait une URL `http` depuis `BodyText` ;
3. `ExtractUrls` extrait une URL `https` depuis `BodyText` ;
4. `ExtractUrls` extrait plusieurs URLs ;
5. `ExtractUrls` extrait une URL depuis `BodyHtml` ;
6. `ExtractUrls` retire la ponctuation finale ;
7. `ExtractUrls` déduplique les URLs identiques ;
8. `ExtractUrls` renseigne `Host` ;
9. `ExtractUrls` renseigne `Scheme`;
10. `ExtractUrls` lève `ArgumentNullException` si `ParsedEmail` est null ;
11. aucun test ne doit nécessiter de réseau.

## Exemple de corps pour les tests

```text
Bonjour,
Veuillez consulter https://evil.example.com/login.
Puis vérifiez http://backup.example.net/path?x=1
```

## Critères d’acceptation

La mission est terminée si :

* `Frelon.Mail` compile ;
* `Frelon.Mail.Tests` compile ;
* tous les tests existants continuent de passer ;
* les nouveaux tests passent ;
* aucun package NuGet n’a été ajouté ;
* aucun fichier hors périmètre n’a été modifié ;
* aucun modèle Core n’a été modifié ;
* aucun code réseau n’a été ajouté ;
* aucune URL n’est ouverte ;
* aucune fonctionnalité hors mission n’a été introduite.

## Important

Ne pas intégrer encore l’extracteur dans `BasicEmailIncidentAnalyzer`.

Ne pas créer encore d’IOC depuis les URLs.

Ne pas modifier `FraudIncident`.

Ne pas introduire de scoring.

Ne pas faire de vérification DNS ou HTTP.

Cette mission sert uniquement à extraire localement des URLs depuis un `ParsedEmail`.