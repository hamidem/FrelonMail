# Mission #009C — Première commande CLI `analyze`

## Goal

Livrer le premier flux utilisateur complet du MVP :

```text
frelon analyze suspicious.eml --output ./out
```

La commande lit un `.eml` local, construit un `FraudIncident` avec le pipeline réel et écrit :

```text
out/incident.json
out/report.md
out/iocs.json
```

Cette mission démarre après fusion de #008B et peut être exécutée en parallèle de #009A et #009B. Elle ne doit pas dépendre du résultat de ces deux missions.

## Ownership exclusif

Modifier uniquement :

- `src/Frelon.Cli/`;
- créer `tests/Frelon.Cli.Tests/`;
- `Frelon.slnx` pour référencer le nouveau projet de tests.

Cette mission est la seule de la vague #009 autorisée à modifier la solution.

## Project setup

- Transformer `Frelon.Cli` en exécutable avec `OutputType` égal à `Exe`.
- Ajouter les références nécessaires vers `Frelon.Core`, `Frelon.Mail` et `Frelon.Reports`.
- Ne pas référencer `Frelon.Storage` ni `Frelon.Exporters` dans cette mission.
- Créer un projet xUnit `Frelon.Cli.Tests` aligné sur les versions déjà utilisées dans la solution.
- Ne pas ajouter de package de parsing de ligne de commande : le contrat est volontairement petit.

## Supported syntax

Accepter uniquement :

```text
frelon analyze <eml-path> --output <directory>
frelon analyze <eml-path> -o <directory>
```

L'ordre de `--output` après le chemin est fixe dans cette première mission. Tout argument supplémentaire ou manquant est une erreur d'usage.

## Real pipeline

Composer explicitement :

- `MimeKitEmailParser`;
- `BasicEmailHeaderAnalyzer`;
- `BasicEmailUrlExtractor`;
- `BasicUrlIocExtractor`;
- `BasicEmailAttachmentAnalyzer`;
- `BasicAttachmentIocExtractor`;
- `BasicIncidentRiskScorer`;
- `BasicEmailIncidentAnalyzer`;
- les trois writers existants de `Frelon.Reports`.

Ne pas dupliquer la logique métier dans le CLI et ne pas introduire de conteneur d'injection de dépendances.

## File behavior

- Vérifier que le fichier source existe et est un fichier `.eml` sans sensibilité à la casse.
- Ouvrir le fichier en lecture seule.
- Créer le répertoire de sortie s'il n'existe pas.
- Refuser d'écraser un des trois fichiers de sortie existants; ne produire aucune sortie partielle dans ce cas.
- Générer les trois contenus en mémoire avant les écritures.
- Écrire d'abord dans trois fichiers temporaires situés dans le répertoire de sortie, puis les renommer seulement lorsque les trois écritures ont réussi.
- Nettoyer au mieux les fichiers temporaires après erreur.
- Ne jamais supprimer ni modifier un fichier préexistant.
- Utiliser UTF-8 sans BOM.

## Exit codes and messages

- `0` : succès;
- `2` : usage invalide, fichier source invalide ou conflit de sortie;
- `1` : erreur d'analyse ou d'entrée/sortie inattendue.

Écrire les erreurs sur stderr et le message de succès concis sur stdout. Ne jamais afficher le contenu du mail, une pièce jointe ou le snapshot JSON dans la console.

Le point d'entrée doit rester mince. Extraire une classe interne ou publique testable qui retourne le code de sortie et accepte des abstractions simples pour stdout/stderr si nécessaire; ne pas lancer un sous-processus dans tous les tests.

## Safety constraints

- Aucun réseau.
- Aucune ouverture d'URL.
- Aucune exécution ou écriture de pièce jointe.
- Aucun stockage SQLite.
- Aucun envoi ou signalement.
- Aucun overwrite implicite.
- Ne pas accepter de répertoire de sortie égal au chemin du fichier source.

## Tests

Couvrir au minimum :

1. arguments manquants ou supplémentaires -> code 2;
2. commande inconnue -> code 2;
3. source absente -> code 2;
4. extension autre que `.eml` -> code 2;
5. `--output` et `-o` acceptés;
6. création du répertoire de sortie;
7. analyse d'un vrai `.eml` minimal avec le pipeline réel;
8. présence des trois fichiers;
9. `incident.json` est un JSON valide et porte le bon nom de source;
10. `iocs.json` est un JSON valide;
11. `report.md` contient le titre attendu;
12. conflit sur chacun des trois noms -> aucun fichier préexistant modifié et aucune sortie partielle;
13. erreur d'analyse -> code 1 et temporaires nettoyés;
14. aucun contenu sensible écrit sur stdout/stderr;
15. chemins comportant espaces et caractères non ASCII;
16. exécution de l'assembly CLI dans au moins un test d'intégration léger.

## Completion criteria

- la commande cible du README fonctionne réellement;
- les trois artefacts sont produits par les implémentations existantes;
- les erreurs sont sûres et ne laissent pas d'état partiel;
- aucune logique réseau ou offensive;
- `dotnet test Frelon.slnx` passe intégralement.

