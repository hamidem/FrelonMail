# Mission #009B — Consultation locale bornée des incidents SQLite

## Goal

Ajouter une première consultation locale des incidents persistés, sans désérialiser les snapshots complets.

Le résultat attendu est une liste récente de métadonnées, destinée aux futures interfaces CLI et à la corrélation locale :

```text
colonnes incidents
-> requête SQL paramétrée et bornée
-> IReadOnlyList<IncidentSummary>
```

Cette mission démarre après fusion de #008B et peut être exécutée en parallèle de #009A et #009C.

## Ownership exclusif

Modifier uniquement :

- `src/Frelon.Storage/`;
- `tests/Frelon.Storage.Tests/`.

Ne modifier aucun autre projet, fichier projet, schéma ou fichier de solution.

## Required API

Créer dans `Frelon.Storage` un record immuable `IncidentSummary` contenant :

```csharp
Guid IncidentId
DateTimeOffset CreatedAt
DateTimeOffset ImportedAt
string SourceFileName
double RiskValue
RiskLevel RiskLevel
FraudClassification Classification
```

Ajouter à `IIncidentStore` :

```csharp
Task<IReadOnlyList<IncidentSummary>> ListRecentAsync(
    int limit = 100,
    CancellationToken cancellationToken = default);
```

## Query rules

- Accepter `limit` entre 1 et 500 inclus.
- Sinon lever `ArgumentOutOfRangeException` avant toute ouverture de connexion.
- Vérifier immédiatement le token d'annulation.
- Exécuter exactement une requête sur les colonnes dédiées; ne pas sélectionner `payload_json`.
- Ordonner par `created_at DESC`, puis `incident_id ASC` pour rendre les égalités déterministes.
- Passer `limit` via un paramètre SQLite.
- Retourner une liste vide si la table ne contient aucun incident.
- Ne pas initialiser implicitement le schéma.

La requête ne doit effectuer ni filtre métier, ni pagination, ni recherche plein texte dans cette mission.

## Mapping rules

- Parser `incident_id` en `Guid` et les dates en `DateTimeOffset` avec une culture indépendante.
- Parser `risk_level` et `classification` strictement, sans ignorer une valeur inconnue.
- Une donnée stockée invalide doit produire `InvalidDataException` avec un message indiquant la colonne concernée; ne pas substituer silencieusement `Unknown`.
- Conserver `ConfigureAwait(false)` et libérer connexion, commande et reader.

## Tests

Utiliser de vraies bases SQLite temporaires et couvrir au minimum :

1. table vide -> liste vide;
2. un incident -> résumé exact;
3. plusieurs incidents -> ordre décroissant de création;
4. égalité de date -> ordre déterministe par identifiant;
5. `limit` borne réellement le résultat;
6. limites 1 et 500 acceptées;
7. limites 0 et 501 refusées avant connexion;
8. token annulé;
9. `payload_json` invalide n'empêche pas la consultation, preuve que le snapshot n'est pas lu;
10. GUID, date, niveau ou classification SQL invalide -> `InvalidDataException`;
11. les paramètres protègent toujours les valeurs persistées hostiles;
12. `SaveAsync`, `GetByIdAsync` et l'initialisation existants restent inchangés.

## Explicitly forbidden

- modification du schéma ou nouvelle table;
- index supplémentaire;
- pagination par offset ou curseur;
- filtres;
- suppression ou mise à jour;
- désérialisation de `payload_json`;
- Entity Framework, Dapper ou nouveau package;
- CLI, reporting ou corrélation.

## Completion criteria

- la consultation est bornée, déterministe et locale;
- elle repose uniquement sur les métadonnées SQL;
- le contrat ne fuit aucun type SQLite;
- tous les tests Storage et la solution complète passent.

