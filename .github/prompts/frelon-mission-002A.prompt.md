# Mission Copilot #002A — Préparer la lecture locale d’un fichier .eml dans Frelon.Mail

## Contexte

Les missions précédentes ont mis en place :

* le modèle métier minimal dans `Frelon.Core` ;
* les tests unitaires minimaux de `Frelon.Core`.

Cette mission démarre le module `Frelon.Mail`.

Frelon reste un outil défensif, local-first, sans interaction offensive ni réseau dans le MVP.

Cette mission doit respecter :

```text id="qzfx9s"
.github/copilot-instructions.md
```

## Objectif

Créer les contrats et types techniques minimaux permettant à `Frelon.Mail` de représenter le résultat brut de la lecture d’un fichier `.eml`.

Cette mission ne doit pas encore implémenter une analyse complète.

Le but est de préparer proprement le terrain pour la future lecture MIME.

## Périmètre autorisé

Copilot peut modifier uniquement :

```text id="19un0n"
src/Frelon.Mail/
tests/Frelon.Mail.Tests/
```

Copilot ne doit pas modifier :

```text id="b1l8ee"
src/Frelon.Core/
src/Frelon.Reports/
src/Frelon.Exporters/
src/Frelon.Cli/
tests/Frelon.Core.Tests/
tests/Frelon.Reports.Tests/
```

## Contraintes

* Ne pas ajouter de package NuGet dans cette mission.
* Ne pas créer de nouveau projet.
* Ne pas modifier la structure de solution.
* Ne pas ajouter de code réseau.
* Ne pas ouvrir d’URL.
* Ne pas exécuter de pièce jointe.
* Ne pas envoyer d’email.
* Ne pas faire de parsing MIME avancé pour l’instant.
* Ne pas implémenter IMAP.
* Ne pas créer de dashboard.
* Garder le code simple, local et testable.
* Utiliser des types immuables lorsque pertinent.
* Garder les nullable reference types activés.

## Travail demandé

Créer dans `src/Frelon.Mail/` les éléments suivants.

### 1. ParsedEmail

Créer un type `ParsedEmail` représentant une lecture brute minimale d’un email.

Champs suggérés :

* `RawContent`
* `Headers`
* `BodyText`
* `BodyHtml`

`Headers` peut être une collection en lecture seule de paires clé/valeur.

### 2. ParsedEmailHeader

Créer un type `ParsedEmailHeader`.

Champs suggérés :

* `Name`
* `Value`

### 3. IEmailParser

Créer une interface `IEmailParser`.

Signature suggérée :

```csharp id="0oy3gr"
public interface IEmailParser
{
    Task<ParsedEmail> ParseAsync(
        Stream emlStream,
        CancellationToken cancellationToken = default);
}
```

### 4. BasicEmailParser

Créer une première implémentation `BasicEmailParser`.

Pour cette mission, elle peut rester très simple :

* lire le flux texte ;
* séparer grossièrement les headers du corps ;
* gérer la séparation standard entre headers et body par ligne vide ;
* retourner `ParsedEmail`.

Cette implémentation n’a pas besoin d’être parfaite.

Elle sert seulement de base testable avant l’intégration éventuelle d’un vrai parser MIME.

### 5. Tests unitaires

Créer des tests dans `tests/Frelon.Mail.Tests/`.

Vérifier que :

1. `BasicEmailParser` peut lire un `.eml` minimal depuis un `MemoryStream` ;
2. les headers simples `From`, `To`, `Subject` sont extraits ;
3. le corps texte est extrait après la ligne vide ;
4. aucun appel réseau n’est effectué ;
5. aucune URL n’est ouverte ;
6. aucune pièce jointe n’est exécutée.

## Exemple de .eml minimal pour les tests

```text id="l7p8sh"
From: sender@example.com
To: victim@example.com
Subject: Test suspicious mail

Hello,
This is a suspicious email.
```

## Critères d’acceptation

La mission est terminée si :

* `Frelon.Mail` compile ;
* `Frelon.Mail.Tests` compile ;
* les tests passent ;
* aucun package NuGet n’a été ajouté ;
* aucun fichier hors périmètre n’a été modifié ;
* aucun code réseau n’a été ajouté ;
* aucune analyse avancée non demandée n’a été introduite.

## Important

Ne pas utiliser MimeKit dans cette mission.

Une future mission décidera explicitement si l’on ajoute MimeKit ou un autre parser MIME.

Ne pas construire encore de `FraudIncident`.

Ne pas extraire encore les URLs.

Ne pas hasher encore les pièces jointes.

Ne pas produire encore `incident.json`.

Ne pas produire encore `report.md`.