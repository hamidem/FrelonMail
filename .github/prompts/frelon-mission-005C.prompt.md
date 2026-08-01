# Mission Copilot #005C — Générer des IOC depuis les URLs extraites

## Contexte

Les missions précédentes ont mis en place :

* le modèle métier minimal dans `Frelon.Core` ;
* le parsing local d’un fichier `.eml` ;
* l’analyse simple des headers email ;
* la construction d’un `FraudIncident` minimal ;
* l’extraction locale des URLs depuis `BodyText` et `BodyHtml` ;
* l’intégration des `UrlIndicator` dans `FraudIncident.Urls` ;
* la génération de `incident.json`, `report.md` et `iocs.json`.

À ce stade, les URLs sont extraites mais elles ne produisent encore aucun IOC.

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Créer une couche dédiée capable de transformer les URLs extraites en indicateurs de compromission (`Ioc`).

Pour chaque URL exploitable, Frelon doit pouvoir produire :

* un IOC de type `Url` ;
* un IOC de type `Domain` lorsque le host est disponible.

Cette mission doit ensuite intégrer cette génération dans `BasicEmailIncidentAnalyzer`.

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
* Ne pas créer de nouveau projet.
* Ne pas modifier la structure de solution.
* Ne pas modifier les modèles `Frelon.Core`.
* Ne pas ajouter de code réseau.
* Ne jamais ouvrir une URL.
* Ne jamais faire de requête HTTP.
* Ne jamais résoudre un domaine en DNS.
* Ne jamais vérifier l’existence d’un domaine.
* Ne pas introduire de scoring.
* Ne pas classifier automatiquement l’incident.
* Ne pas traiter encore les pièces jointes.
* Ne pas créer de base de données.
* Ne pas créer de CLI.
* Garder la transformation URL → IOC isolée et testable.

## Travail demandé

### 1. Créer une interface `IUrlIocExtractor`

Créer une interface dans `src/Frelon.Mail/`.

Signature suggérée :

```csharp
using Frelon.Core;

namespace Frelon.Mail;

public interface IUrlIocExtractor
{
    IReadOnlyList<Ioc> ExtractIocs(
        IReadOnlyList<UrlIndicator> urls,
        DateTimeOffset observedAt);
}
```

Le nom peut être légèrement adapté si nécessaire, mais la responsabilité doit rester explicite :

> transformer des `UrlIndicator` déjà extraits en `Ioc`.

L'interface ne doit pas recevoir un `ParsedEmail`.

Elle ne doit pas reparcourir le body du mail.

Elle ne doit pas recevoir un `FraudIncident`.

### 2. Créer `BasicUrlIocExtractor`

Créer une implémentation `BasicUrlIocExtractor`.

Pour chaque `UrlIndicator`, créer si possible :

#### IOC URL

Créer un IOC :

```text
Type = IocType.Url
Value = url.NormalizedValue
Confidence = valeur par défaut définie dans cette mission
FirstSeen = observedAt
```

Si `NormalizedValue` est null ou vide, utiliser `RawValue` si celui-ci est exploitable.

Ne pas créer d’IOC URL si aucune valeur exploitable n’existe.

#### IOC Domain

Si `url.Host` est renseigné, créer également :

```text
Type = IocType.Domain
Value = url.Host
Confidence = valeur par défaut définie dans cette mission
FirstSeen = observedAt
```

Ne pas faire de résolution DNS.

Ne pas modifier le host.

Une normalisation minimale par `Trim()` et casse cohérente est acceptable.

### 3. Confidence par défaut

Pour cette mission, utiliser une confiance prudente et fixe.

Valeur recommandée :

```text
0.5
```

La présence d’une URL dans un mail analysé n’implique pas encore qu’elle soit frauduleuse.

Ne pas utiliser `IsSuspicious` pour modifier la confiance dans cette mission.

Le scoring et la qualification feront l’objet de missions ultérieures.

### 4. Source

Si le modèle `Ioc` contient une propriété `Source`, utiliser une valeur simple et explicite, par exemple :

```text
email-url
```

Ne pas utiliser de nom de fournisseur externe.

Ne pas inventer de source de renseignement.

### 5. Déduplication

Les IOC doivent être dédupliqués.

Exemple :

```text
https://evil.example.com/login
https://evil.example.com/reset
```

Doivent produire :

```text
IOC Url : https://evil.example.com/login
IOC Domain : evil.example.com

IOC Url : https://evil.example.com/reset
```

Le domaine :

```text
evil.example.com
```

ne doit apparaître qu’une seule fois comme IOC `Domain`.

La déduplication doit considérer séparément :

```text
IocType + Value
```

Ainsi une même chaîne peut théoriquement exister sous deux types différents.

Utiliser une comparaison insensible à la casse pour cette mission.

### 6. Intégrer `IUrlIocExtractor` dans `BasicEmailIncidentAnalyzer`

Ajouter une dépendance :

```csharp
IUrlIocExtractor
```

Le constructeur doit recevoir désormais :

```csharp
IEmailParser parser,
IEmailHeaderAnalyzer headerAnalyzer,
IEmailUrlExtractor urlExtractor,
IUrlIocExtractor urlIocExtractor
```

Vérifier chaque dépendance avec :

```csharp
ArgumentNullException.ThrowIfNull(...)
```

Dans `AnalyzeAsync` :

1. parser le mail ;
2. extraire l’identité ;
3. extraire l’authentification ;
4. extraire la chaîne `Received` ;
5. extraire les URLs ;
6. capturer l’horodatage de l’incident ;
7. générer les IOC depuis les URLs ;
8. construire le `FraudIncident`.

Renseigner :

```text
Urls
Iocs
```

dans le `FraudIncident`.

### 7. Horodatage cohérent

Utiliser le même instant logique pour :

```text
FraudIncident.CreatedAt
EvidenceSource.ImportedAt
Ioc.FirstSeen
```

Capturer une seule valeur :

```csharp
var now = DateTimeOffset.UtcNow;
```

et la réutiliser.

Ne pas appeler plusieurs fois `DateTimeOffset.UtcNow` pour ces valeurs dans une même analyse.

## Tests de `BasicUrlIocExtractor`

Créer un fichier :

```text
tests/Frelon.Mail.Tests/BasicUrlIocExtractorTests.cs
```

Ajouter des tests vérifiant que :

1. une liste vide d’URLs produit une liste vide d’IOC ;
2. une URL produit un IOC de type `Url` ;
3. une URL possédant un host produit un IOC de type `Domain` ;
4. la valeur de l’IOC URL correspond à `NormalizedValue` ;
5. l’IOC URL utilise `RawValue` si `NormalizedValue` n’est pas exploitable ;
6. `FirstSeen` correspond exactement à `observedAt` ;
7. la confiance par défaut vaut `0.5` ;
8. plusieurs URLs du même domaine ne produisent qu’un seul IOC `Domain` ;
9. deux URLs différentes produisent deux IOC `Url` ;
10. la déduplication est insensible à la casse ;
11. aucun appel réseau n’est nécessaire.

## Tests de `BasicEmailIncidentAnalyzer`

Modifier ou compléter :

```text
tests/Frelon.Mail.Tests/BasicEmailIncidentAnalyzerTests.cs
```

Ajouter des tests vérifiant que :

1. `AnalyzeAsync` renseigne maintenant `FraudIncident.Iocs` lorsqu’une URL est détectée ;
2. une URL produit un IOC `Url` ;
3. le host de l’URL produit un IOC `Domain` ;
4. plusieurs URLs du même domaine ne produisent qu’un seul IOC `Domain` ;
5. les IOC restent vides lorsqu’aucune URL n’est détectée ;
6. le constructeur lève `ArgumentNullException` si `urlIocExtractor` est null ;
7. `CreatedAt`, `Evidence.ImportedAt` et `Ioc.FirstSeen` utilisent le même horodatage logique.

Adapter les tests existants qui vérifiaient explicitement que les IOC restaient vides en présence d’URLs.

Ce comportement appartient à la Mission #005B et devient obsolète avec cette mission.

## Critères d’acceptation

La mission est terminée si :

* `Frelon.Mail` compile ;
* `Frelon.Mail.Tests` compile ;
* tous les tests encore pertinents continuent de passer ;
* les nouveaux tests passent ;
* les anciens tests rendus obsolètes par la mission sont adaptés proprement ;
* aucun package NuGet n’a été ajouté ;
* aucun fichier hors périmètre n’a été modifié ;
* aucun modèle Core n’a été modifié ;
* aucun appel réseau n’a été ajouté ;
* aucune URL n’est ouverte ;
* les IOC `Url` et `Domain` sont correctement générés ;
* les IOC sont correctement dédupliqués ;
* aucun scoring n’a été introduit ;
* aucune fonctionnalité hors mission n’a été ajoutée.

## Important

Ne pas qualifier encore une URL de frauduleuse.

Ne pas augmenter la confiance selon `IsSuspicious`.

Ne pas créer de règles antispam.

Ne pas produire de signalement.

Ne pas traiter les pièces jointes.

Ne pas introduire de base de données.

Cette mission sert uniquement à transformer localement les URLs déjà extraites en IOC défensifs structurés.