# Mission #008A — Première persistance SQLite des incidents

## Goal

Introduire la première couche de persistance locale de Frelon.

Cette mission doit permettre de :

1. initialiser explicitement un schéma SQLite minimal ;
2. enregistrer un `FraudIncident` complet comme snapshot JSON interne ;
3. relire ce snapshot par `IncidentId` ;
4. conserver quelques métadonnées de l’incident dans des colonnes SQLite dédiées pour préparer les futures requêtes locales.

Le résultat attendu est :

```text
FraudIncident complet
        ↓
IIncidentStore
        ↓
SqliteIncidentStore
        ↓
SQLite local
├── métadonnées requêtables
└── snapshot JSON complet
        ↓
GetByIdAsync
        ↓
FraudIncident
```

Cette mission ne crée encore aucun moteur de recherche, aucune corrélation et aucune interface CLI.

---

## Prerequisites

Les projets suivants doivent déjà exister dans la solution avant l’exécution de cette mission :

```text
src/Frelon.Storage/
tests/Frelon.Storage.Tests/
```

Références de projet attendues :

```text
Frelon.Storage
→ Frelon.Core

Frelon.Storage.Tests
→ Frelon.Storage
→ Frelon.Core
```

Le package NuGet suivant doit déjà être référencé uniquement par `Frelon.Storage` :

```text
Microsoft.Data.Sqlite
```

Ne pas ajouter Entity Framework Core.

Ne pas créer les projets ou modifier la solution dans cette mission.

---

## Scope

Modifier uniquement :

- `src/Frelon.Storage/`
- `tests/Frelon.Storage.Tests/`

Ne modifier aucun autre projet.

En particulier :

- ne pas modifier `Frelon.Core`;
- ne pas modifier `Frelon.Mail`;
- ne pas modifier `Frelon.Reports`;
- ne pas modifier `Frelon.Exporters`;
- ne pas modifier `Frelon.Cli`;
- ne pas modifier le fichier de solution;
- ne pas ajouter de package NuGet.

---

## Architectural rules

`Frelon.Storage` est une couche d’infrastructure locale.

Elle peut dépendre de :

- `Frelon.Core`;
- `Microsoft.Data.Sqlite`;
- `System.Text.Json`;
- les API du framework .NET.

Elle ne doit pas dépendre de :

- `Frelon.Mail`;
- `Frelon.Reports`;
- `Frelon.Exporters`;
- `Frelon.Cli`;
- MimeKit;
- Entity Framework Core.

### Storage owns its internal serialization

Ne pas réutiliser `IIncidentJsonWriter` ou `SystemTextJsonIncidentJsonWriter`.

Le JSON public produit par `Frelon.Reports` et le snapshot interne de persistance sont deux responsabilités différentes.

`Frelon.Storage` doit posséder ses propres options internes `JsonSerializerOptions`.

Ne pas ajouter de référence vers `Frelon.Reports`.

---

## Security and behavior constraints

- Aucun appel réseau.
- Aucun stockage distant.
- Aucun service externe.
- Aucun envoi de données.
- Aucun chiffrement ajouté dans cette mission.
- Aucun mot de passe SQLite ou mécanisme de chiffrement propriétaire.
- Aucune exécution de pièce jointe.
- Aucun stockage des octets bruts de pièces jointes.
- Aucun recalcul de SHA-256.
- Aucun recalcul de score.
- Aucune classification.
- Aucun enrichissement.
- Aucun signalement.
- Aucun SQL construit par concaténation de données provenant de `FraudIncident`.

Toutes les valeurs écrites dans les commandes SQL doivent utiliser des paramètres SQLite.

---

## Important semantic rules

### Snapshot, not alternate domain model

La base SQLite ne doit pas devenir une seconde implémentation de `FraudIncident`.

Le snapshot JSON interne est la représentation complète persistée de l’agrégat.

Les colonnes dédiées sont uniquement des métadonnées dénormalisées destinées à préparer de futures requêtes locales.

Dans cette mission :

```text
payload_json
→ source de reconstruction du FraudIncident

colonnes dédiées
→ métadonnées de recherche futures
```

`GetByIdAsync` doit reconstruire l’incident depuis `payload_json`.

Ne pas reconstruire manuellement un `FraudIncident` depuis les colonnes SQL.

### Save means insert

`SaveAsync` enregistre un nouvel incident.

Il ne doit pas :

- remplacer silencieusement un incident existant ;
- utiliser `INSERT OR REPLACE` ;
- utiliser un UPSERT ;
- mettre à jour un snapshot existant.

Un `IncidentId` déjà présent doit provoquer une `InvalidOperationException`.

Le message exact attendu est :

```text
Un incident avec l'identifiant '{incidentId}' existe déjà.
```

où `{incidentId}` utilise le format standard du `Guid`.

### Internal schema version

Le snapshot doit être associé à une version de schéma interne.

Définir :

```csharp
public const int CurrentSchemaVersion = 1;
```

Une ligne dont `schema_version` est différente de `CurrentSchemaVersion` ne doit pas être désérialisée.

`GetByIdAsync` doit lever `NotSupportedException` avec le message exact :

```text
Version de schéma de stockage non supportée : {schemaVersion}.
```

---

## A. Create `IIncidentStore`

Créer :

`src/Frelon.Storage/IIncidentStore.cs`

Contrat attendu :

```csharp
using Frelon.Core;

namespace Frelon.Storage;

public interface IIncidentStore
{
    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        FraudIncident incident,
        CancellationToken cancellationToken = default);

    Task<FraudIncident?> GetByIdAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);
}
```

L’interface ne doit exposer aucun type `Microsoft.Data.Sqlite`.

---

## B. Create `SqliteIncidentStore`

Créer :

`src/Frelon.Storage/SqliteIncidentStore.cs`

Implémenter `IIncidentStore`.

Définir :

```csharp
public const int CurrentSchemaVersion = 1;
```

Le constructeur attendu est :

```csharp
public SqliteIncidentStore(string connectionString)
```

Valider `connectionString`.

Si la valeur est `null`, vide ou composée uniquement d’espaces, lever `ArgumentException`.

Ne pas ouvrir de connexion dans le constructeur.

Conserver uniquement la chaîne de connexion.

---

## C. Internal JSON options

Créer des options `System.Text.Json` internes au store.

Utiliser :

```csharp
PropertyNamingPolicy = JsonNamingPolicy.CamelCase
```

Ajouter :

```csharp
new JsonStringEnumConverter()
```

Le snapshot interne doit donc sérialiser les enums sous forme de chaînes.

Ne pas utiliser les options de `Frelon.Reports`.

Ne pas exposer publiquement les `JsonSerializerOptions`.

---

## D. Explicit schema initialization

`InitializeAsync` doit :

1. vérifier immédiatement `cancellationToken`;
2. créer une `SqliteConnection` à partir de la chaîne de connexion;
3. ouvrir la connexion avec `OpenAsync(cancellationToken)`;
4. exécuter une commande `CREATE TABLE IF NOT EXISTS`.

Créer exactement la table :

```sql
CREATE TABLE IF NOT EXISTS incidents
(
    incident_id      TEXT PRIMARY KEY NOT NULL,
    schema_version   INTEGER NOT NULL,
    created_at       TEXT NOT NULL,
    imported_at      TEXT NOT NULL,
    source_file_name TEXT NOT NULL,
    risk_value       REAL NOT NULL,
    risk_level       TEXT NOT NULL,
    classification   TEXT NOT NULL,
    payload_json     TEXT NOT NULL
);
```

Ne créer aucune autre table dans cette mission.

Ne créer aucun index supplémentaire.

Ne pas utiliser de migration framework.

`SaveAsync` et `GetByIdAsync` ne doivent pas créer implicitement le schéma.

L’appel à `InitializeAsync` est une étape explicite du cycle de vie du store.

---

## E. Save an incident

`SaveAsync` doit :

1. vérifier immédiatement `cancellationToken`;
2. vérifier `incident` avec `ArgumentNullException.ThrowIfNull`;
3. sérialiser l’incident complet en JSON interne;
4. ouvrir une nouvelle `SqliteConnection`;
5. exécuter un `INSERT` paramétré.

Commande attendue :

```sql
INSERT INTO incidents
(
    incident_id,
    schema_version,
    created_at,
    imported_at,
    source_file_name,
    risk_value,
    risk_level,
    classification,
    payload_json
)
VALUES
(
    $incidentId,
    $schemaVersion,
    $createdAt,
    $importedAt,
    $sourceFileName,
    $riskValue,
    $riskLevel,
    $classification,
    $payloadJson
);
```

Valeurs attendues :

```text
incident_id
→ incident.IncidentId.ToString("D")

schema_version
→ CurrentSchemaVersion

created_at
→ incident.CreatedAt.ToString("O")

imported_at
→ incident.Evidence.ImportedAt.ToString("O")

source_file_name
→ incident.Evidence.SourceFileName

risk_value
→ incident.RiskScore.Value

risk_level
→ incident.RiskScore.Level.ToString()

classification
→ incident.Classification.ToString()

payload_json
→ snapshot JSON complet
```

Toutes les valeurs doivent être transmises via paramètres SQLite.

Ne pas concaténer les valeurs dans la chaîne SQL.

### Duplicate incident handling

Si l’`INSERT` échoue parce que `incident_id` existe déjà, transformer uniquement ce conflit de clé primaire / contrainte unique en :

```csharp
InvalidOperationException
```

avec le message exact :

```text
Un incident avec l'identifiant '{incident.IncidentId}' existe déjà.
```

Conserver l’exception SQLite originale comme `InnerException`.

Ne pas transformer les autres erreurs SQLite en `InvalidOperationException`.

Ne pas utiliser `INSERT OR IGNORE`.

Ne pas utiliser `INSERT OR REPLACE`.

Ne pas utiliser UPSERT.

---

## F. Read an incident by id

`GetByIdAsync` doit :

1. vérifier immédiatement `cancellationToken`;
2. ouvrir une nouvelle `SqliteConnection`;
3. exécuter une requête paramétrée :

```sql
SELECT schema_version, payload_json
FROM incidents
WHERE incident_id = $incidentId;
```

Utiliser :

```text
incidentId.ToString("D")
```

comme valeur du paramètre.

### Incident not found

Si aucune ligne n’est trouvée :

```csharp
return null;
```

Ne pas lever d’exception.

### Schema version

Lire `schema_version`.

Si la valeur diffère de `CurrentSchemaVersion`, lever :

```csharp
NotSupportedException
```

avec le message exact :

```text
Version de schéma de stockage non supportée : {schemaVersion}.
```

Ne pas tenter de désérialiser `payload_json`.

### JSON deserialization

Désérialiser `payload_json` en :

```csharp
FraudIncident
```

avec les mêmes options internes utilisées pour la sérialisation.

Si la désérialisation retourne `null`, lever :

```csharp
InvalidDataException
```

avec le message exact :

```text
Le snapshot de l'incident '{incidentId}' est invalide.
```

### Incident id consistency

Après désérialisation, vérifier :

```csharp
incident.IncidentId == incidentId
```

Sinon lever :

```csharp
InvalidDataException
```

avec le message exact :

```text
L'identifiant du snapshot ne correspond pas à l'identifiant stocké '{incidentId}'.
```

Ne pas corriger silencieusement l’identifiant.

---

## G. Tests

Créer :

`tests/Frelon.Storage.Tests/SqliteIncidentStoreTests.cs`

Utiliser une vraie base SQLite temporaire par test.

Le test porte précisément sur la persistance locale ; l’écriture d’un fichier SQLite temporaire est donc intentionnelle.

Créer un helper de test qui :

1. construit un chemin unique sous `Path.GetTempPath()`;
2. construit une chaîne de connexion SQLite pour ce fichier;
3. supprime le fichier de base à la fin du test dans un `finally` ou via un helper `IDisposable` / `IAsyncDisposable`.

Ne pas utiliser une base de données partagée entre tests.

Ne pas dépendre de l’ordre d’exécution des tests.

### Rich incident fixture

Créer une fixture `FraudIncident` suffisamment riche pour vérifier le round-trip.

Elle doit contenir au minimum :

- un `IncidentId` fixe dans le test;
- `CreatedAt`;
- `EvidenceSource` avec `SourceFileName` et `ImportedAt`;
- `MailIdentity`;
- `AuthenticationAssessment`;
- au moins un `ReceivedHop`;
- au moins une `UrlIndicator`;
- au moins un `AttachmentIndicator`;
- au moins trois IOC dans un ordre connu : URL, Domain, Hash;
- un `RiskScore` non neutre;
- plusieurs `RiskScore.Reasons` dans un ordre connu;
- une `FraudClassification`;
- au moins une `RecommendedAction`.

Utiliser des valeurs littérales dans les assertions.

### Minimum scenarios

Couvrir au minimum :

1. `InitializeAsync` crée la table `incidents`;
2. `InitializeAsync` peut être appelée deux fois sans exception;
3. `SaveAsync` avec incident null lève `ArgumentNullException`;
4. constructeur avec connection string null lève `ArgumentException`;
5. constructeur avec connection string vide lève `ArgumentException`;
6. constructeur avec connection string composée d’espaces lève `ArgumentException`;
7. un incident enregistré peut être relu par `IncidentId`;
8. un `IncidentId` absent retourne `null`;
9. le round-trip conserve exactement `IncidentId`;
10. le round-trip conserve exactement `CreatedAt`;
11. le round-trip conserve exactement `Evidence.ImportedAt`;
12. le round-trip conserve `Evidence.SourceFileName`;
13. le round-trip conserve l’identité déclarée;
14. le round-trip conserve l’authentification;
15. le round-trip conserve la chaîne `Received` et son ordre;
16. le round-trip conserve les URLs;
17. le round-trip conserve les pièces jointes;
18. le round-trip conserve les IOC et leur ordre URL, Domain, Hash;
19. le round-trip conserve `RiskScore.Value`;
20. le round-trip conserve `RiskScore.Level`;
21. le round-trip conserve les `RiskScore.Reasons` et leur ordre;
22. le round-trip conserve `Classification`;
23. le round-trip conserve les `RecommendedActions`;
24. deux appels `SaveAsync` avec le même `IncidentId` lèvent `InvalidOperationException`;
25. le message exact du conflit d’`IncidentId` est vérifié;
26. l’incident enregistré lors du premier `SaveAsync` reste inchangé après l’échec du second;
27. le conflit conserve l’exception SQLite comme `InnerException`;
28. une valeur `SourceFileName` contenant des apostrophes et du texte ressemblant à du SQL est enregistrée et relue exactement;
29. après cette valeur hostile, la table `incidents` existe toujours et un second incident distinct peut être enregistré;
30. les métadonnées SQL stockées correspondent à l’incident :
    - `schema_version == 1`;
    - `created_at` correspond au format `"O"`;
    - `imported_at` correspond au format `"O"`;
    - `source_file_name` correspond exactement;
    - `risk_value` correspond exactement;
    - `risk_level` correspond au nom de l’enum;
    - `classification` correspond au nom de l’enum;
31. `payload_json` est un objet JSON valide;
32. dans `payload_json`, les enums sont représentés sous forme de chaînes;
33. une ligne dont `schema_version` vaut `2` provoque `NotSupportedException`;
34. le message exact de version non supportée est vérifié;
35. un snapshot JSON dont l’`IncidentId` ne correspond pas à la clé SQL provoque `InvalidDataException`;
36. le message exact de non-correspondance d’identifiant est vérifié;
37. un `CancellationToken` déjà annulé provoque `OperationCanceledException` dans `InitializeAsync`;
38. un `CancellationToken` déjà annulé provoque `OperationCanceledException` dans `SaveAsync`;
39. un `CancellationToken` déjà annulé provoque `OperationCanceledException` dans `GetByIdAsync`.

### Test the real property

Quand un test vérifie la structure SQL, interroger réellement SQLite.

Quand un test vérifie `payload_json`, parser réellement le JSON avec `JsonDocument`.

Quand un test vérifie la protection contre une valeur ressemblant à du SQL, persister réellement cette valeur et vérifier ensuite que la table et les opérations suivantes fonctionnent.

Ne pas créer de test du type :

```text
un chemin aléatoire n'existe toujours pas
→ donc aucune écriture imprévue
```

Le commissariat a fermé ce dossier.

---

## H. Production code quality

Utiliser les API asynchrones disponibles de `Microsoft.Data.Sqlite` pour :

- `OpenAsync`;
- `ExecuteNonQueryAsync`;
- `ExecuteReaderAsync`;
- `ReadAsync`.

Propager `CancellationToken`.

Utiliser `ConfigureAwait(false)` dans le code de production asynchrone de `Frelon.Storage`.

Libérer correctement :

- `SqliteConnection`;
- `SqliteCommand`;
- `SqliteDataReader`.

Ne pas exposer une connexion SQLite ouverte comme état du store.

Ne pas conserver une connexion dans un champ.

Chaque opération ouvre et ferme sa propre connexion.

---

## Explicitly forbidden

Ne pas ajouter dans cette mission :

- Entity Framework Core;
- `DbContext`;
- migrations EF;
- Dapper;
- une autre bibliothèque SQLite;
- une table par sous-entité de `FraudIncident`;
- table IOC;
- table URL;
- table Attachment;
- table Received;
- recherche d’incidents;
- listing d’incidents;
- pagination;
- tri;
- corrélation;
- agrégation de campagnes;
- modification d’un incident existant;
- suppression d’incident;
- archivage;
- chiffrement;
- compression du snapshot;
- stockage des octets de pièce jointe;
- stockage du `.eml` brut;
- reporting;
- CLI;
- réseau;
- nouvelle règle de score;
- classification automatique;
- action recommandée automatique.

---

## Completion criteria

La mission est terminée lorsque :

- `IIncidentStore` existe;
- `SqliteIncidentStore` existe;
- le schéma SQLite est initialisé explicitement;
- la table `incidents` correspond exactement au schéma demandé;
- un `FraudIncident` complet peut être enregistré;
- le snapshot JSON interne permet de reconstruire l’incident;
- les métadonnées utiles sont stockées dans des colonnes dédiées;
- toutes les commandes d’écriture et de lecture utilisent des paramètres;
- un doublon d’`IncidentId` n’écrase jamais l’incident existant;
- une version de schéma inconnue est refusée;
- une incohérence d’identifiant entre SQL et snapshot est refusée;
- le round-trip conserve les données et l’ordre des collections;
- aucun projet hors `Frelon.Storage` et `Frelon.Storage.Tests` n’est modifié;
- `Frelon.Storage` compile;
- `Frelon.Storage.Tests` passe intégralement.
