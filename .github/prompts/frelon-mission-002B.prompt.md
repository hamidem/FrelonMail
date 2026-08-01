# Mission Copilot #002B — Durcir BasicEmailParser sans parser MIME externe

## Contexte

La Mission #002A a créé un premier parser minimal dans `Frelon.Mail`.

Les tests actuels valident déjà :

* la lecture d’un flux `.eml` minimal ;
* l’extraction des headers `From`, `To`, `Subject` ;
* l’extraction du corps texte après la ligne vide ;
* la conservation du contenu brut ;
* `BodyHtml == null` pour un email texte simple.

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Améliorer légèrement `BasicEmailParser` pour qu’il gère des cas `.eml` simples mais plus réalistes, tout en restant volontairement limité.

Cette mission ne doit pas transformer `BasicEmailParser` en vrai parser MIME complet.

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
* Ne pas utiliser MimeKit dans cette mission.
* Ne pas créer de nouveau projet.
* Ne pas modifier la structure de solution.
* Ne pas ajouter de code réseau.
* Ne pas ouvrir d’URL.
* Ne pas exécuter de pièce jointe.
* Ne pas envoyer d’email.
* Ne pas implémenter IMAP.
* Ne pas créer de dashboard.
* Ne pas construire encore de `FraudIncident`.
* Ne pas extraire encore les URLs.
* Ne pas hasher encore les pièces jointes.
* Garder le code simple, local, lisible et testable.

## Travail demandé

Améliorer `BasicEmailParser` pour gérer les cas suivants.

### 1. Séparateurs de lignes CRLF et LF

Le parser doit fonctionner avec :

```text
\r\n
```

mais aussi avec :

```text
\n
```

Certains `.eml` exportés ou reconstruits peuvent ne pas respecter strictement CRLF.

### 2. Ligne vide entre headers et body

Le parser doit détecter correctement la séparation headers/body avec :

```text
\r\n\r\n
```

ou :

```text
\n\n
```

### 3. Headers repliés sur plusieurs lignes

Le parser doit gérer les headers repliés selon la forme classique :

```text
Subject: Votre compte nécessite
 une vérification urgente
```

ou :

```text
Subject: Votre compte nécessite
\tune vérification urgente
```

Les lignes commençant par un espace ou une tabulation doivent être rattachées au header précédent.

Résultat attendu :

```text
Subject = "Votre compte nécessite une vérification urgente"
```

### 4. Headers dupliqués

Le parser doit accepter plusieurs headers avec le même nom.

Exemple :

```text
Received: from first.example
Received: from second.example
```

Le résultat doit conserver les deux entrées dans `ParsedEmail.Headers`.

Ne pas écraser les headers dupliqués.

### 5. Headers malformés

Une ligne de header sans `:` ne doit pas faire planter le parser.

Pour cette mission, elle peut être ignorée.

Exemple :

```text
This is not a valid header
```

### 6. Flux vide

Un flux vide ne doit pas faire planter le parser.

Il doit retourner un `ParsedEmail` avec :

* `RawContent` vide ;
* aucun header ;
* `BodyText` vide ou null selon la convention déjà utilisée dans le projet.

Choisir la convention la plus simple et la documenter par un test.

## Tests à ajouter

Ajouter ou compléter les tests dans :

```text
tests/Frelon.Mail.Tests/BasicEmailParserTests.cs
```

Créer des tests vérifiant que :

1. `ParseAsync` gère les fichiers utilisant seulement `\n` comme séparateur de lignes ;
2. `ParseAsync` gère les headers repliés avec espace ;
3. `ParseAsync` gère les headers repliés avec tabulation ;
4. `ParseAsync` conserve les headers dupliqués ;
5. `ParseAsync` ignore une ligne de header malformée sans exception ;
6. `ParseAsync` gère un flux vide sans exception.

## Critères d’acceptation

La mission est terminée si :

* `Frelon.Mail` compile ;
* `Frelon.Mail.Tests` compile ;
* tous les tests existants continuent de passer ;
* les nouveaux tests passent ;
* aucun package NuGet n’a été ajouté ;
* aucun fichier hors périmètre n’a été modifié ;
* aucun comportement réseau n’a été introduit ;
* aucune fonctionnalité hors mission n’a été ajoutée.

## Important

Ne pas anticiper la Mission #002C.

Ne pas ajouter MimeKit.

Ne pas implémenter l’analyse MIME complète.

Ne pas extraire les URLs.

Ne pas traiter les pièces jointes.

Cette mission sert uniquement à rendre le parser minimal plus robuste.
