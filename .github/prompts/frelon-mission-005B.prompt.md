# Mission Copilot #005B — Intégrer les URLs extraites dans FraudIncident

## Contexte

Les missions précédentes ont mis en place :

* le modèle métier minimal dans `Frelon.Core` ;
* le parsing minimal d’un `.eml` local dans `Frelon.Mail` ;
* l’analyse simple des headers email ;
* la construction d’un `FraudIncident` minimal ;
* la génération de `incident.json`, `report.md` et `iocs.json` ;
* l’extraction locale des URLs depuis un `ParsedEmail` avec `IEmailUrlExtractor` et `BasicEmailUrlExtractor`.

Cette mission intègre maintenant l’extraction d’URLs dans le flux d’analyse d’incident.

Frelon reste un outil défensif, local-first, sans réseau, sans interaction offensive et sans base de données à ce stade.

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Modifier `BasicEmailIncidentAnalyzer` pour qu’il utilise `IEmailUrlExtractor` et renseigne la propriété `FraudIncident.Urls`.

Cette mission ne doit pas encore créer d’IOC à partir des URLs.

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
* Ne pas créer encore d’IOC depuis les URLs.
* Ne pas implémenter de scoring.
* Garder le code simple, local, lisible et testable.

## Travail demandé

### 1. Modifier `BasicEmailIncidentAnalyzer`

Ajouter une dépendance à :

```csharp
IEmailUrlExtractor
```

Le constructeur principal doit recevoir :

```csharp
IEmailParser parser,
IEmailHeaderAnalyzer headerAnalyzer,
IEmailUrlExtractor urlExtractor
```

### 2. Préserver la compatibilité des tests existants si possible

Si des tests existants instancient déjà `BasicEmailIncidentAnalyzer` avec seulement deux dépendances, adapter les tests.

Ne pas conserver un ancien constructeur si cela complique le design.

Préférer un constructeur clair avec les trois dépendances requises.

### 3. Extraire les URLs pendant l’analyse

Dans `AnalyzeAsync` :

1. parser le flux `.eml` ;
2. extraire l’identité ;
3. extraire l’authentification ;
4. extraire la chaîne `Received` ;
5. extraire les URLs avec `IEmailUrlExtractor`;
6. construire le `FraudIncident` avec la propriété `Urls` renseignée.

### 4. Ne pas créer d’IOC

Même si une URL contient un host, ne pas alimenter encore `FraudIncident.Iocs`.

Les IOC feront l’objet d’une mission séparée.

## Tests à ajouter ou modifier

Modifier ou compléter :

```text
tests/Frelon.Mail.Tests/BasicEmailIncidentAnalyzerTests.cs
```

Ajouter des tests vérifiant que :

1. `AnalyzeAsync` renseigne `FraudIncident.Urls` lorsqu’une URL est présente dans le body texte ;
2. `AnalyzeAsync` renseigne `FraudIncident.Urls` lorsqu’une URL est présente dans le body HTML si le parser actuel permet de le tester ;
3. `AnalyzeAsync` laisse `FraudIncident.Urls` vide lorsqu’aucune URL n’est présente ;
4. `AnalyzeAsync` ne renseigne pas encore `FraudIncident.Iocs` à partir des URLs ;
5. le constructeur lève `ArgumentNullException` si `urlExtractor` est null ;
6. les tests existants continuent de passer.

## Exemple de .eml pour les tests

```text
From: Fake Support <support@example.net>
Subject: Suspicious login attempt

Bonjour,
Veuillez consulter https://evil.example.com/login.
```

Le `FraudIncident` produit doit contenir une URL avec au minimum :

```text
RawValue = https://evil.example.com/login
Host = evil.example.com
Scheme = https
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
* aucun IOC n’est encore créé depuis les URLs ;
* aucune fonctionnalité hors mission n’a été introduite.

## Important

Ne pas modifier `Frelon.Core`.

Ne pas modifier `Frelon.Reports`.

Ne pas créer d’IOC dans cette mission.

Ne pas introduire de scoring.

Ne pas faire de vérification DNS ou HTTP.

Cette mission sert uniquement à intégrer les URLs extraites dans `FraudIncident.Urls`.