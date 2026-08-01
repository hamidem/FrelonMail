# Mission #008B — Stabiliser le contrat d'identité des incidents

## Goal

Remplacer l'identifiant textuel ambigu de `FraudIncident` par un `Guid` fortement typé dans toute la solution.

Cette mission corrige notamment l'incompatibilité actuelle suivante :

```text
BasicEmailIncidentAnalyzer -> Guid au format N
SqliteIncidentStore        -> clé SQL au format D
GetByIdAsync               -> comparaison textuelle N / D impossible
```

Après cette mission, le format texte ne doit plus faire partie du contrat métier. Le format `D` reste utilisé uniquement aux frontières texte (SQLite, JSON et affichage).

Cette mission est un prérequis bloquant de la vague parallèle #009. Elle doit être fusionnée avant de créer les branches #009A, #009B et #009C.

## Scope

Modifier uniquement les fichiers nécessaires dans :

- `src/Frelon.Core/`;
- `src/Frelon.Mail/`;
- `src/Frelon.Reports/`;
- `src/Frelon.Storage/`;
- les quatre projets de tests existants.

Ne pas modifier `Frelon.Cli`, `Frelon.Exporters`, le README, la CI ou la solution.

## Required changes

1. Dans `FraudIncident`, remplacer :

```csharp
public required string IncidentId { get; init; }
```

par :

```csharp
public required Guid IncidentId { get; init; }
```

2. Dans `BasicEmailIncidentAnalyzer`, générer directement :

```csharp
IncidentId = Guid.NewGuid()
```

3. Adapter les documents internes de reporting, dont `IocsJsonDocument`, pour conserver le type `Guid` jusqu'à la sérialisation.

4. Dans `SqliteIncidentStore` :

- écrire la clé avec `incident.IncidentId.ToString("D")`;
- supprimer toute normalisation par parsing d'une chaîne métier;
- comparer l'identifiant désérialisé et l'identifiant demandé comme deux `Guid`;
- conserver les messages d'erreur existants et leur sens;
- ne pas changer le schéma SQLite ni `CurrentSchemaVersion`.

5. Adapter toutes les fixtures et assertions de tests sans affaiblir leur précision.

## Compatibility rules

- Le JSON public continue de représenter l'identifiant comme une chaîne JSON au format GUID standard.
- Le snapshot SQLite continue d'utiliser une clé `TEXT` au format `D`.
- Aucun mécanisme de migration d'anciens snapshots n'est demandé : la persistance vient d'être introduite et le schéma reste en version 1.
- Ne pas introduire de wrapper `IncidentId`, de converter JSON personnalisé ou de nouveau package.
- Ne modifier ni le scoring, ni la classification, ni les IOC.

## Tests

Couvrir au minimum :

1. l'analyse d'un `.eml` produit un `Guid` non vide;
2. le JSON incident contient l'identifiant au format standard attendu;
3. le JSON IOC conserve le même identifiant;
4. un incident produit par le vrai `BasicEmailIncidentAnalyzer` peut être enregistré puis relu par le vrai `SqliteIncidentStore`;
5. le round-trip conserve exactement le `Guid`;
6. un identifiant absent retourne toujours `null`;
7. un snapshot contenant un autre GUID est toujours refusé;
8. les 252 tests existants restent verts après adaptation.

Le scénario 4 est essentiel : il doit tester l'intégration réelle qui échappe actuellement aux fixtures de stockage isolées.

## Completion criteria

- `FraudIncident.IncidentId` est un `Guid`;
- aucune comparaison fonctionnelle d'identifiants ne dépend d'un format de chaîne;
- le scénario analyse -> sauvegarde -> relecture fonctionne;
- aucun changement fonctionnel hors identité;
- `dotnet test Frelon.slnx` passe intégralement.

