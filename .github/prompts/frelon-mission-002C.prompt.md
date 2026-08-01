# Mission Copilot #002C — Analyse métier minimale des headers email

## Contexte

Les missions précédentes ont permis de créer :

* le modèle métier minimal dans `Frelon.Core` ;
* les tests de base du modèle ;
* un `BasicEmailParser` dans `Frelon.Mail` ;
* des tests pour lire un `.eml` local minimal ;
* un durcissement léger du parser minimal si la Mission #002B a été exécutée.

Cette mission démarre la transformation des headers bruts en informations métier simples.

Frelon reste un outil défensif, local-first, sans réseau, sans interaction avec des URLs et sans exécution de pièce jointe.

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Créer une première couche d’analyse des headers email.

Le but est de convertir un `ParsedEmail` en objets métier déjà présents dans `Frelon.Core`, notamment :

* `MailIdentity`
* `AuthenticationAssessment`
* `ReceivedHop`

Cette mission ne doit pas encore créer de `FraudIncident` complet.

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
* Ne pas construire encore de `FraudIncident`.
* Ne pas extraire encore les URLs du body.
* Ne pas hasher encore les pièces jointes.
* Garder le code simple, local, lisible et testable.

## Travail demandé

Créer dans `src/Frelon.Mail/` une couche d’analyse simple des headers.

### 1. Créer une interface `IEmailHeaderAnalyzer`

Signature suggérée :

```csharp
using Frelon.Core;

namespace Frelon.Mail;

public interface IEmailHeaderAnalyzer
{
    MailIdentity ExtractIdentity(ParsedEmail email);

    AuthenticationAssessment ExtractAuthentication(ParsedEmail email);

    IReadOnlyList<ReceivedHop> ExtractReceivedChain(ParsedEmail email);
}
```

Adapter légèrement la signature si nécessaire selon les types réellement générés dans `Frelon.Core`.

### 2. Créer une implémentation `BasicEmailHeaderAnalyzer`

Cette classe doit analyser uniquement les headers déjà présents dans `ParsedEmail`.

Elle ne doit pas relire le fichier `.eml`.

Elle ne doit pas faire d’appel réseau.

Elle ne doit pas modifier `ParsedEmail`.

### 3. Extraction de `MailIdentity`

Extraire au minimum les headers suivants si présents :

```text
From
Reply-To
Return-Path
Message-ID
Subject
```

Règles :

* si un header est absent, laisser la propriété correspondante à `null` ou valeur par défaut selon le modèle existant ;
* ne pas tenter de valider les adresses email dans cette mission ;
* ne pas tenter de résoudre les domaines ;
* ne pas interpréter encore les usurpations.

### 4. Extraction de `AuthenticationAssessment`

Extraire au minimum :

```text
Authentication-Results
```

Si le modèle `AuthenticationAssessment` contient des champs SPF, DKIM ou DMARC, remplir ces champs de manière très simple en détectant des fragments textuels dans `Authentication-Results`.

Exemples possibles :

```text
spf=pass
dkim=fail
dmarc=none
```

Règles :

* ne pas faire de requête DNS ;
* ne pas vérifier réellement SPF/DKIM/DMARC ;
* ne pas implémenter un parseur complet RFC ;
* conserver le header brut si le modèle le permet.

### 5. Extraction de `ReceivedHop`

Extraire tous les headers :

```text
Received
```

Chaque header `Received` doit devenir un `ReceivedHop`.

Pour cette mission, le parsing peut rester minimal.

Remplir au moins :

* `Position`
* `RawValue`

Si le modèle contient d’autres champs comme `From`, `By`, `With`, `IpAddress`, `Timestamp`, ils peuvent rester `null` ou valeur par défaut.

Règles :

* conserver tous les headers `Received` ;
* ne pas écraser les doublons ;
* respecter leur ordre d’apparition dans le mail ;
* ne pas tenter d’identifier encore l’origine réelle.

## Tests à ajouter

Créer ou compléter un fichier de test, par exemple :

```text
tests/Frelon.Mail.Tests/BasicEmailHeaderAnalyzerTests.cs
```

Ajouter des tests vérifiant que :

1. `ExtractIdentity` extrait `From`, `Reply-To`, `Return-Path`, `Message-ID` et `Subject` ;
2. `ExtractIdentity` ne plante pas si certains headers sont absents ;
3. `ExtractAuthentication` conserve le header brut `Authentication-Results` si présent ;
4. `ExtractAuthentication` détecte simplement `spf=pass`, `dkim=fail`, `dmarc=none` si le modèle permet de stocker ces résultats ;
5. `ExtractReceivedChain` conserve plusieurs headers `Received` ;
6. `ExtractReceivedChain` conserve l’ordre des headers `Received` ;
7. aucune méthode ne fait d’appel réseau ;
8. aucune méthode n’ouvre d’URL ;
9. aucune méthode n’exécute de pièce jointe.

## Exemple de headers pour les tests

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

Ne pas créer encore de `FraudIncident`.

Ne pas introduire MimeKit.

Ne pas faire de parsing MIME complet.

Ne pas extraire les URLs.

Ne pas traiter les pièces jointes.

Ne pas produire de rapport Markdown.

Ne pas produire de JSON.

Cette mission sert uniquement à transformer les headers bruts en éléments métier simples.