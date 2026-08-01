# 04 — Interfaces

## 1. Interface HTTP locale

### 1.1 Base et conventions

L'API est servie en HTTP sur `localhost`, port `5127` par défaut. Il n'y a ni HTTPS local, ni authentification utilisateur, ni CORS. La sécurité repose sur la boucle locale et les contrôles stricts de requête décrits dans [05 — Sécurité et exploitation](05-security-and-operations.md).

Conventions JSON :

- propriétés camelCase ;
- enums sous forme de chaînes sensibles à la casse (`"High"`, `"ConfirmedFraud"`) ;
- valeurs entières pour enums refusées ;
- erreurs métier courantes : `{ "message": "..." }` ;
- exceptions contrôlées : objet RFC Problem Details générique ;
- `Cache-Control: no-store` et en-têtes de sécurité sur toutes les réponses.

Toutes les routes peuvent d'abord répondre `403` si l'adresse distante, `Host`, `Origin` ou `Sec-Fetch-Site` ne respecte pas la politique locale.

### 1.2 Inventaire des routes

| Méthode | Route | Réponse principale |
|---|---|---|
| GET | `/api/application/info` | identité et version |
| GET | `/api/application/session` | jeton éphémère d'arrêt |
| POST | `/api/application/shutdown` | arrêt propre de l'instance |
| GET | `/api/incidents` | 25 résumés récents |
| POST | `/api/incidents/analyze` | analyse et persistance d'un EML/MSG |
| GET | `/api/incidents/{id}` | projection détaillée d'incident |
| GET | `/api/incidents/{id}/exports/{format}` | téléchargement d'un export |
| GET | `/api/incidents/{id}/reviews/latest` | dernière revue ou 204 |
| GET | `/api/incidents/{id}/reviews?limit=50` | historique de revues |
| POST | `/api/incidents/{id}/reviews` | nouvelle décision append-only |
| GET | `/api/campaigns?incidentLimit=100` | campagnes courantes |
| GET | `/api/campaigns/{fingerprint}` | campagne courante/historique et revues |
| POST | `/api/campaigns/{fingerprint}/reviews` | nouvelle décision sur snapshot courant |

## 2. Cycle de vie de l'application

### `GET /api/application/info`

Réponse `200` :

```json
{
  "productName": "Frelon",
  "version": "0.1.0-beta.1"
}
```

La version provient de l'assembly réellement exécutée ; les métadonnées après `+` sont supprimées.

### `GET /api/application/session`

Réponse `200` :

```json
{
  "shutdownToken": "A0F4...64_CARACTERES_HEXADECIMAUX..."
}
```

Le jeton représente 32 octets aléatoires et change à chaque démarrage.

### `POST /api/application/shutdown`

En-tête obligatoire :

```http
X-Frelon-Shutdown-Token: <jeton retourné par /api/application/session>
```

| Statut | Condition |
|---:|---|
| `200` | jeton valide ; arrêt déclenché après l'envoi complet de la réponse |
| `401` | jeton absent, mal formé ou différent |

La comparaison utilise `CryptographicOperations.FixedTimeEquals`.

## 3. Incidents

### 3.1 Analyser un message

`POST /api/incidents/analyze`

Le corps est le fichier brut, pas un multipart. Le nom est transmis séparément, encodé pour URI.

```http
POST /api/incidents/analyze HTTP/1.1
Host: localhost:5127
Content-Type: application/octet-stream
X-Frelon-Filename: suspicious%20message.eml
Content-Length: 2048

<octets EML ou MSG>
```

Exemple JavaScript correspondant à l'interface embarquée :

```javascript
const response = await fetch('/api/incidents/analyze', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/octet-stream',
    'X-Frelon-Filename': encodeURIComponent(file.name)
  },
  body: file
});
```

Validation du nom :

- obligatoire ;
- aucun `/`, `\` ou caractère de contrôle ;
- base non vide ;
- extension `.eml` ou `.msg`, sans distinction de casse.

La route refuse une taille déclarée de `0` ou supérieure à 25 Mio. Le buffer du moteur vérifie aussi la taille réelle et les flux chunked.

Réponse `200`, projection destinée à l'UI :

```json
{
  "incidentId": "8c904d68-8a9a-4fb3-b799-3018c930829c",
  "createdAt": "2026-07-31T12:00:00+00:00",
  "sourceFileName": "suspicious message.eml",
  "sourceSha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "subject": "Vérifiez votre compte",
  "from": "Support <support@example.test>",
  "replyTo": null,
  "riskValue": 65,
  "riskLevel": "High",
  "classification": "Unknown",
  "guidance": {
    "headline": "Traitez ce message comme suspect jusqu'à vérification",
    "explanation": "Plusieurs vérifications n'ont pas permis de confirmer la fiabilité apparente du message.",
    "keyObservations": ["Au moins un lien présente une caractéristique habituellement risquée."],
    "recommendedActions": ["Ne cliquez sur aucun lien et n'ouvrez aucune pièce jointe."],
    "boundary": "Frelon observe le message sans ouvrir ses liens ni exécuter ses pièces jointes. Cette analyse automatique doit rester soumise à une validation humaine."
  },
  "classificationAssessment": {
    "classification": "Phishing",
    "confidence": "Medium",
    "reasons": ["Une URL est explicitement signalée comme suspecte", "Au moins un mécanisme d'authentification est en échec"]
  },
  "riskReasons": ["Échec d'authentification SPF", "Échec d'authentification DMARC", "URL suspecte détectée"],
  "authentication": { "spf": "fail", "dkim": "pass", "dmarc": "fail", "isSuspicious": false },
  "urlCount": 1,
  "attachmentCount": 0,
  "defensiveFindings": [
    {
      "kind": "Url",
      "value": "http://192.0.2.10/login",
      "reasons": ["L'URL utilise directement une adresse IP", "Un chemin sensible est exposé sans HTTPS"]
    }
  ],
  "iocs": [
    { "type": "Url", "value": "http://192.0.2.10/login", "confidence": 0.5, "source": "email-url" },
    { "type": "IpAddress", "value": "192.0.2.10", "confidence": 0.5, "source": "email-url" }
  ]
}
```

Le contenu exact dépend du fichier ; cet exemple illustre le contrat.

| Statut | Cause principale |
|---:|---|
| `200` | incident analysé **et** inséré dans SQLite |
| `400` | nom/format/taille invalide, parsing refusé, quota ou délai du worker |
| `500` | échec interne ou persistance impossible |

L'analyse est retournée uniquement après insertion réussie. Il n'existe donc pas de succès HTTP non persisté.

### 3.2 Lister les incidents

`GET /api/incidents` retourne au plus 25 `IncidentSummary`, triés par `createdAt DESC`, puis `incidentId ASC`.

```json
[
  {
    "incidentId": "8c904d68-8a9a-4fb3-b799-3018c930829c",
    "createdAt": "2026-07-31T12:00:00+00:00",
    "importedAt": "2026-07-31T12:00:00+00:00",
    "sourceFileName": "suspicious.eml",
    "riskValue": 65,
    "riskLevel": "High",
    "classification": "Unknown",
    "latestReviewVerdict": "ConfirmedFraud",
    "latestReviewClassification": "Phishing",
    "latestReviewAt": "2026-07-31T12:10:00+00:00"
  }
]
```

### 3.3 Lire un incident

`GET /api/incidents/{incidentId}` retourne la même `IncidentPresentation` que l'analyse ou `404`.

Cette projection n'expose pas la chaîne `Received`, le détail complet des URL/pièces jointes ou les actions recommandées. Ceux-ci restent disponibles dans `incident-json` et `report-markdown`.

### 3.4 Exporter

`GET /api/incidents/{id}/exports/{format}`

| Format | Fichier | Type | Précondition |
|---|---|---|---|
| `incident-json` | `incident.json` | JSON | aucune |
| `report-markdown` | `report.md` | Markdown | aucune |
| `iocs-json` | `iocs.json` | JSON | aucune |
| `iocs-csv` | `iocs.csv` | CSV | aucune |
| `review-json` | `review.json` | JSON | au moins une revue |
| `validated-report-markdown` | `signalement.md` | Markdown | dernière revue = fraude confirmée et catégorisée |
| `bundle` | `frelon-{idN}.zip` | ZIP | aucune ; ajoute revue/signalement si disponibles |

Statuts particuliers : `404` incident ou revue absent, `409` signalement non autorisé par la dernière décision, `400` format inconnu.

## 4. Revues d'incident

### 4.1 Créer une décision

`POST /api/incidents/{incidentId}/reviews`

Exemple fraude confirmée :

```json
{
  "verdict": "ConfirmedFraud",
  "classification": "Phishing",
  "notes": "Demande confirmée comme frauduleuse par l'équipe sécurité."
}
```

Exemples cohérents supplémentaires :

```json
{ "verdict": "Benign", "classification": null, "notes": null }
```

```json
{ "verdict": "Suspicious", "classification": "Suspicious", "notes": "À approfondir" }
```

La date et les identifiants de revue sont générés par le serveur. Réponse `201 Created`, avec `Location: /api/incidents/{incidentId}/reviews/{reviewId}`. Cette route `Location` n'a pas de route GET dédiée ; l'objet est consulté via la collection.

Erreurs : `404` incident absent, `400` verdict manquant ou combinaison invalide.

### 4.2 Consulter

- `GET .../reviews/latest` : `200` avec l'objet, `204` si aucune décision, `404` si incident absent.
- `GET .../reviews?limit=N` : liste de la plus récente à la plus ancienne ; défaut `50`, plage API `1..100`, `404` si incident absent.

## 5. Campagnes

### 5.1 Lister

`GET /api/campaigns?incidentLimit=100`

`incidentLimit` contrôle la fenêtre d'incidents recalculée, défaut `100`, plage `1..500`. La réponse contient pour chaque campagne courante :

```json
{
  "candidate": {
    "incidentIds": ["...", "..."],
    "fingerprint": "64-caracteres-hexadecimaux",
    "firstObservedAt": "2026-07-30T10:00:00+00:00",
    "lastObservedAt": "2026-07-31T11:00:00+00:00",
    "links": []
  },
  "latestReview": null,
  "isReviewed": false
}
```

### 5.2 Lire le détail

`GET /api/campaigns/{fingerprint}?incidentLimit=100&reviewLimit=50`

Les deux limites acceptent `1..500`. L'empreinte est un SHA-256 hexadécimal de 64 caractères. La réponse peut représenter :

- une campagne courante avec ou sans historique ;
- une campagne devenue historique, reconstruite depuis sa dernière revue.

Elle expose `fingerprint`, `currentCandidate`, `candidateSnapshot`, `reviewHistory`, `latestReview` et `isCurrent`. Réponse `404` si ni calcul courant ni historique ne correspond.

### 5.3 Revoir une campagne

`POST /api/campaigns/{fingerprint}/reviews`

```json
{
  "candidateSnapshot": {
    "incidentIds": ["...", "..."],
    "fingerprint": "...",
    "firstObservedAt": "...",
    "lastObservedAt": "...",
    "links": ["..."]
  },
  "verdict": "Confirmed",
  "notes": "IOC communs et temporalité cohérente."
}
```

Le client doit renvoyer le `currentCandidate` exact reçu. Le serveur recalcule la fenêtre de 100 incidents avant l'écriture.

| Statut | Condition |
|---:|---|
| `201` | décision enregistrée |
| `400` | empreinte, snapshot ou verdict absent/incohérent |
| `409` | campagne disparue ou modifiée depuis sa consultation |

## 6. Interface navigateur

L'UI est une application HTML/CSS/JavaScript sans framework ni build frontend. Elle est servie depuis `wwwroot`.

Fonctions visibles :

- glisser-déposer/sélection d'un EML ou MSG ;
- validation client de l'extension et de la taille ;
- lecture guidée et vue analyste ;
- résumé de risque, authentification, raisons, IOC et règles déclenchées ;
- historique des 25 incidents récents ;
- revue humaine et historique append-only ;
- téléchargement des exports individuels et du bundle ;
- liste/détail/revue des campagnes candidates ;
- arrêt propre de l'application.

Le client échappe les données en utilisant `textContent` et en créant les nœuds DOM, sans insertion d'HTML issu du message observée dans `app.js`.

Au chargement, il appelle l'identité, l'historique et les campagnes. `MapFallbackToFile("index.html")` supporte la navigation côté client ; **déduit** : une route HTTP inconnue, y compris sous `/api`, peut recevoir `index.html` plutôt qu'un `404` si aucun endpoint ne la capture.

## 7. CLI

### 7.1 Analyse

```text
frelon analyze <message-path> --output <directory> [--csv] [--database <sqlite-file>]
```

Alias : `-o` pour `--output`, `-d` pour `--database`.

Exemples :

```powershell
dotnet run --project src/Frelon.Cli -- analyze samples/suspicious-demo.eml --output .\out
dotnet run --project src/Frelon.Cli -- analyze message.msg -o .\out --csv -d .\data\frelon.db
```

Sorties :

```text
out/
├── incident.json
├── report.md
├── iocs.json
└── iocs.csv       # seulement avec --csv
```

Garanties de publication :

- la CLI n'écrase jamais un rapport existant ;
- elle écrit d'abord des fichiers `.frelon-{guid}.tmp` ;
- elle revérifie les conflits juste avant les déplacements finaux ;
- en cas d'échec partiel ou de persistance, elle supprime au mieux les fichiers créés par l'opération ;
- la preuve source n'est jamais modifiée.

### 7.2 Consultation

```powershell
frelon incidents list --database .\data\frelon.db
frelon incidents list -d .\data\frelon.db --limit 20
frelon incidents show 8c904d68-8a9a-4fb3-b799-3018c930829c -d .\data\frelon.db
```

La liste est tabulée et neutralise les caractères de contrôle du nom de source. `show` écrit le snapshot complet en JSON. La consultation exige une base existante et ne la crée pas.

### 7.3 Codes de sortie

| Code | Sémantique |
|---:|---|
| `0` | succès, y compris liste vide |
| `1` | annulation, analyse/IO/consultation échouée, incident absent |
| `2` | syntaxe, chemin, format, taille ou conflit de sortie invalide |

Les messages CLI sont actuellement en anglais, contrairement à l'interface Web et au domaine majoritairement francophones.

## 8. Références de code

- [`Program.cs` Web](../../src/Frelon.Web/Program.cs)
- [`IncidentPresentation.cs`](../../src/Frelon.Web/IncidentPresentation.cs)
- [`app.js`](../../src/Frelon.Web/wwwroot/app.js)
- [`CliApplication.cs`](../../src/Frelon.Cli/CliApplication.cs)
- [`IncidentConsultationRunner.cs`](../../src/Frelon.Cli/IncidentConsultationRunner.cs)
